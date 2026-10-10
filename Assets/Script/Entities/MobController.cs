using Assets.Script.Interfaces;
using Assets.Script.Models;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Assets.Script.Entities
{
    public enum MobState { Idle, Walk, Attack, Dead, Hurt }

    [RequireComponent(typeof(SpriteRenderer))]
    public class MobController : MonoBehaviour, ITargetable, IHasHealth
    {
        [Header("Thông tin cơ bản")]
        private int mobId;
        public short templateId;
        public int currentHp;
        public int maxHp;

        [Header("UI & Render")]
        [SerializeField] private GameObject hpBarContainer;
        [SerializeField] private Transform hpFillTransform;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private MobState _currentState = MobState.Idle;
        private int _currentFrameIndex = 0;
        private float _frameTimer = 0f;
        private Vector3 _targetPosition;
        private const float TELEPORT_DISTANCE = 5.0f;

        private MobVisualData _visualData;
        private bool _isReady = false;

        // Cache lại mảng Sprite sau khi bóc từ Atlas ra
        private Dictionary<MobState, Sprite[]> _animationCache = new Dictionary<MobState, Sprite[]>();
        private AsyncOperationHandle<SpriteAtlas> _atlasHandle;

        // GĐ12 — bộ hình ghép mảnh 5 trạng thái (monster_template.art → MobAnimSO). Có thì dùng thay atlas.
        private MobAnimSO _anim;
        private Fx.PartRenderer _parts;
        private int _loadToken;
        private bool _attackAlt;   // xen kẽ 2 kiểu đánh (nhóm 2 / 3)
        private int _shownFrame = -1;

        private void Awake()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (hpBarContainer != null) hpBarContainer.SetActive(false);
        }

        /// <summary>Hạng thật của con này (server gửi): 0 thường · 1 tinh anh · 2 thủ lĩnh — có thể cao hơn hạng của mẫu.</summary>
        public int Rank { get; private set; }

        /// <summary>Tinh anh / thủ lĩnh sinh ngẫu nhiên từ quái thường → to hơn cho dễ nhận ra.</summary>
        private float RankScale => Data.GameData.Mobs.TryGetValue(templateId, out var t) && Rank > t.rank ? (Rank >= 2 ? 1.5f : 1.25f) : 1f;

        public void Initialize(int id, short tempId, int hp, int max, int rank = 0)
        {
            Rank = rank;
            this.mobId = id;
            this.templateId = tempId;
            this.currentHp = hp;
            this.maxHp = max;
            _targetPosition = transform.position;
            // Quái lấy lại từ Pool có thể còn trạng thái Dead của lần chết trước -> reset
            _currentState = MobState.Idle;
            _currentFrameIndex = 0;
            _attackAnimUntil = 0f;
            _hurtUntil = 0f;
            _loadToken++;   // hình đang tải dở của lần dùng trước (pool) bỏ đi
            AppliedArt = -1;
            UseAnim(null);
            ApplyColor(BaseColor);
            UpdateUI();
        }

        public bool IsDead => _currentState == MobState.Dead;

        /// <summary>Khoảng cách từ {p} tới mép khung va chạm (gốc ở chân, kích thước monster_template.hit_w / hit_h) — khớp server Mob.sqrDistanceTo.</summary>
        public float DistanceFrom(Vector2 p)
        {
            Data.GameData.Mobs.TryGetValue(templateId, out var t);
            float hw = t != null ? t.HalfWidth : 0.5f, h = t != null ? t.Height : 32f / ArtUnits.PointsPerUnit;
            Vector3 o = transform.position;
            float dx = Mathf.Max(Mathf.Abs(p.x - o.x) - hw, 0f);
            float dy = p.y < o.y ? o.y - p.y : Mathf.Max(p.y - (o.y + h), 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Giữ frame đánh / bị đánh trong 1 khoảng ngắn
        private float _attackAnimUntil, _hurtUntil;

        /// <summary>Server báo quái vừa đánh (MOB_ATTACK) -> quay mặt về mục tiêu + hiện frame tấn công.</summary>
        public void PlayAttack(Vector3 targetPos)
        {
            if (_currentState == MobState.Dead) return;
            _spriteRenderer.flipX = targetPos.x < transform.position.x;
            _attackAlt = !_attackAlt;
            ChangeState(MobState.Attack);
            _attackAnimUntil = Time.time + 0.4f;
        }

        /// <summary>Trúng đòn → hiện frame bị đánh 0,25 giây.</summary>
        private void PlayHurt()
        {
            ChangeState(MobState.Hurt);
            _hurtUntil = Time.time + 0.25f;
        }

        /// <summary>Server báo quái chết (MOB_DIE): hiện frame bị đánh, mờ dần. Manager sẽ thu hồi về Pool sau.</summary>
        public void PlayDeath()
        {
            currentHp = 0;
            UpdateUI();
            OnHpChanged?.Invoke(currentHp, maxHp);
            ChangeState(MobState.Dead);
            var c = BaseColor; c.a = 0.6f; ApplyColor(c);
        }

        private void ApplyColor(Color c)
        {
            if (_spriteRenderer != null) _spriteRenderer.color = c;
            if (_parts != null) _parts.SetColor(c);
        }

        /// <summary>Đỉnh đầu quái (đơn vị local): bộ hình mảnh → khung va chạm; atlas → khung ảnh.</summary>
        private float TopY
        {
            get
            {
                if (_anim != null) return Data.GameData.Mobs.TryGetValue(templateId, out var t) && t.hitH > 0 ? t.Height : ArtUnits.ToUnits(_anim.hitH);
                return _spriteRenderer != null && _spriteRenderer.sprite != null ? _spriteRenderer.sprite.bounds.max.y : 1f;
            }
        }

        /// <summary>
        /// GĐ12 — chọn hình theo server: monster_template.art ≥ 0 → tải MobAnim_{art} (Addressables → Resources demo);
        /// không có → {fallback} (dòng MobDatabase, atlas cũ) → không có nữa thì ô màu. Dòng MobDatabase vẫn dùng cho tint / scale.
        /// </summary>
        /// <summary>Bộ hình đang dùng / đang tải (-1 = atlas cũ hoặc ô màu).</summary>
        public int AppliedArt { get; private set; } = -1;

        public void SetArt(int art, MobVisualData fallback)
        {
            AppliedArt = art;
            int token = ++_loadToken;
            _visualData = fallback;
            _isReady = false;
            float s = (fallback != null && fallback.scale > 0 ? fallback.scale : 1f) * RankScale;
            transform.localScale = new Vector3(s, s, 1f);
            Core.AssetSource.Load<MobAnimSO>(MobAnimSO.Address(art), "MobAnim/" + MobAnimSO.Address(art), anim =>
            {
                if (this == null || token != _loadToken) return;
                if (anim != null) { UnloadAtlas(); UseAnim(anim); return; }
                if (fallback != null && fallback.mobAtlas != null && fallback.mobAtlas.RuntimeKeyIsValid()) SetVisual(fallback);
                else SetPlaceholder();
            });
        }

        private void UseAnim(MobAnimSO anim)
        {
            _anim = anim;
            if (anim == null) { if (_parts != null) _parts.Hide(); return; }
            if (_parts == null)
            {
                _parts = gameObject.AddComponent<Fx.PartRenderer>();
                _parts.SetSorting(_spriteRenderer.sortingLayerName, _spriteRenderer.sortingOrder);
            }
            _spriteRenderer.sprite = null;   // hình do các mảnh vẽ
            ApplyColor(BaseColor);
            _currentFrameIndex = 0;
            _shownFrame = -1;
            _isReady = true;
            UpdateSpriteFrame();
            AdjustHpBarPosition();
            UpdateNameLabel();
        }

        private int AnimState => _currentState switch
        {
            MobState.Walk => MobAnimSO.Walk,
            MobState.Attack => _attackAlt && _anim.Clip(MobAnimSO.Attack2) != _anim.Clip(MobAnimSO.Idle) ? MobAnimSO.Attack2 : MobAnimSO.Attack,
            MobState.Hurt => MobAnimSO.Hurt,
            MobState.Dead => MobAnimSO.Hurt,
            _ => MobAnimSO.Idle,
        };

        /// <summary>Màu nhuộm của loại quái này (MobVisualData.tint, GĐ8); chưa đặt → trắng.</summary>
        private Color BaseColor => _visualData != null && _visualData.tint.a > 0f ? _visualData.tint : Color.white;

        private string SpriteKey => _visualData != null && !string.IsNullOrEmpty(_visualData.spriteKey) ? _visualData.spriteKey : templateId.ToString();

        // ==========================================
        // NHÃN TÊN + CẤP TRÊN ĐẦU (màu theo hạng) — tạo bằng code, không cần sửa prefab
        // ==========================================
        private TMPro.TextMeshPro _nameLabel;

        private void UpdateNameLabel()
        {
            if (_nameLabel == null)
            {
                var go = new GameObject("NameLabel");
                go.transform.SetParent(transform, false);
                _nameLabel = go.AddComponent<TMPro.TextMeshPro>();
                _nameLabel.fontSize = 2.6f;
                _nameLabel.alignment = TMPro.TextAlignmentOptions.Center;
                _nameLabel.sortingOrder = 20;
                _nameLabel.outlineWidth = 0.2f;
                _nameLabel.outlineColor = Color.black;
            }
            Data.GameData.Mobs.TryGetValue(templateId, out var tpl);
            string name = tpl != null ? tpl.name : (_visualData != null ? _visualData.mobName : $"Quái {templateId}");
            int rank = Mathf.Max(Rank, tpl != null ? tpl.rank : 0);
            string rankTag = rank == 2 ? " [Thủ lĩnh]" : rank == 1 ? " [Tinh anh]" : "";
            _nameLabel.text = tpl != null ? $"{name} Lv{tpl.level}{rankTag}" : name;
            _nameLabel.color = rank == 2 ? new Color(1f, 0.35f, 0.3f) : rank == 1 ? new Color(1f, 0.85f, 0.3f) : Color.white;

            _nameLabel.transform.localPosition = new Vector3(0, TopY + 0.35f, 0);
        }

        /// <summary>
        /// Quái CHƯA CÓ HÌNH (không có trong MobDatabase hoặc atlas thiếu sprite): vẽ ô vuông màu + tên
        /// để vẫn chơi/test được. [CẦN ĐIỀN] thêm dòng vào Assets/SO/MobDatabase.asset cho templateId này.
        /// </summary>
        private static Sprite _placeholderSprite;

        public void SetPlaceholder()
        {
            _visualData = null;
            UseAnim(null);
            UnloadAtlas();
            _animationCache.Clear();
            if (_placeholderSprite == null)
                // ô 4x4 px, 4 px/đơn vị → đúng 1x1 đơn vị world, gốc ở giữa đáy (đứng trên mặt đất)
                _placeholderSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
            transform.localScale = Vector3.one * RankScale;
            _spriteRenderer.sprite = _placeholderSprite;
            _spriteRenderer.color = new Color(0.6f, 0.3f, 0.8f, 0.85f);
            _isReady = true;
            UpdateNameLabel();
        }

        public void SetVisual(MobVisualData visualData)
        {
            this._visualData = visualData;
            _isReady = false;
            UseAnim(null);
            if (_spriteRenderer != null) _spriteRenderer.color = BaseColor;
            float s = (visualData != null && visualData.scale > 0 ? visualData.scale : 1f) * RankScale;
            transform.localScale = new Vector3(s, s, 1f);

            // Xóa dữ liệu cũ
            UnloadAtlas();

            _atlasHandle = Addressables.LoadAssetAsync<SpriteAtlas>(visualData.mobAtlas);
            _atlasHandle.Completed += (op) => {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    SpriteAtlas atlas = op.Result;

                    _animationCache[MobState.Idle] = ExtractSprites(atlas, "idle");
                    _animationCache[MobState.Walk] = ExtractSprites(atlas, "walk");
                    _animationCache[MobState.Attack] = ExtractSprites(atlas, "attack");
                    _animationCache[MobState.Hurt] = ExtractSprites(atlas, "hurt");
                    _animationCache[MobState.Dead] = ExtractSprites(atlas, "dead");

                    _isReady = true;

                    // 1. Gán frame đầu tiên (lúc này _spriteRenderer mới có ảnh)
                    UpdateSpriteFrame();

                    // 2. Tính toán lại vị trí thanh máu dựa trên ảnh vừa gán
                    AdjustHpBarPosition();
                    UpdateNameLabel();
                }
            };
        }

        private Sprite[] ExtractSprites(SpriteAtlas atlas, string action)
        {
            List<Sprite> frames = new List<Sprite>();

            // Xử lý riêng cho trạng thái Walk: cần swap giữa ảnh 0 và 1
            if (action == "walk")
            {
                Sprite frame0 = atlas.GetSprite($"{SpriteKey}_0");
                Sprite frame1 = atlas.GetSprite($"{SpriteKey}_1");

                if (frame0 != null) frames.Add(frame0);
                if (frame1 != null) frames.Add(frame1);
            }
            else
            {
                // Bộ hình quái (kiểu NSO): 0–1 đứng/đi · 2 bị đánh · 3 tấn công. Chết dùng frame bị đánh.
                int actionIdx = action switch
                {
                    "idle" => 0,
                    "hurt" => 2,
                    "dead" => 2,
                    "attack" => 3,
                    _ => 0
                };

                string spriteName = $"{SpriteKey}_{actionIdx}";
                Sprite s = atlas.GetSprite(spriteName);

                if (s != null)
                {
                    frames.Add(s);
                }
            }

            // Trả về mảng các frame tìm được
            if (frames.Count > 0)
            {
                return frames.ToArray();
            }

            // Thiếu frame đánh/chết → dùng tạm frame đứng (không báo lỗi lặp lại). Thiếu cả frame đứng mới cảnh báo.
            if (action != "idle") return ExtractSprites(atlas, "idle");
            Debug.LogWarning($"[Mob] Không tìm thấy ảnh '{SpriteKey}_0' của quái {templateId} trong Atlas! [CẦN ĐIỀN] sửa spriteKey trong MobDatabase.");
            return new Sprite[0];
        }
        private void Update()
        {
            if (!_isReady || _currentState == MobState.Dead) return;

            // 1. Animation logic
            _frameTimer += Time.deltaTime;
            float animSpeed = _anim != null ? _anim.frameSeconds : (_visualData != null) ? _visualData.frameRate : 0.15f;
            if (_frameTimer >= animSpeed)
            {
                _frameTimer = 0f;
                _currentFrameIndex++;
                UpdateSpriteFrame();
            }

            // 2. Di chuyển mượt (Lerp/MoveTowards) theo Server
            float distance = Vector3.Distance(transform.position, _targetPosition);
            bool attacking = Time.time < _attackAnimUntil;
            bool hurt = Time.time < _hurtUntil;
            if (!attacking && _currentState == MobState.Attack) ChangeState(MobState.Idle);
            if (!hurt && _currentState == MobState.Hurt) ChangeState(MobState.Idle);
            bool busy = attacking || hurt;

            if (distance > 0.05f)
            {
                float requiredSpeed = distance / 0.2f;
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, requiredSpeed * 1.15f * Time.deltaTime);
                if (!busy && _currentState != MobState.Walk) ChangeState(MobState.Walk);
            }
            else
            {
                transform.position = _targetPosition;
                if (_currentState == MobState.Walk) ChangeState(MobState.Idle);
            }
        }

        private void UpdateSpriteFrame()
        {
            if (_anim != null) { ShowAnimFrame(); return; }
            if (!_isReady || !_animationCache.ContainsKey(_currentState)) return;

            Sprite[] frames = _animationCache[_currentState];
            if (frames == null || frames.Length == 0) return;

            _currentFrameIndex %= frames.Length;
            _spriteRenderer.sprite = frames[_currentFrameIndex];
        }

        private void ShowAnimFrame()
        {
            var clip = _anim.Clip(AnimState);
            if (clip == null || clip.Count == 0) return;
            if (_currentState == MobState.Dead) _currentFrameIndex = 0;   // chết: đứng yên khung đầu, mờ dần
            int i = _currentFrameIndex % clip.Count;
            bool entered = i != _shownFrame;
            _shownFrame = i;
            var f = clip.frames[i];
            bool flip = _spriteRenderer.flipX;
            _parts.Show(f, _anim.Image, flip);
            // Khung có hiệu ứng kèm (VD tia lửa lúc cắn) → bật 1 lần khi vào khung
            if (entered && f.fx >= 0)
            {
                float x = ArtUnits.ToUnits(f.fxX) * transform.localScale.x;
                Fx.EffectPlayer.Play(f.fx, transform.position + new Vector3(flip ? -x : x, -ArtUnits.ToUnits(f.fxY), 0f), flip);
            }
        }

        public void ChangeState(MobState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            _currentFrameIndex = 0;
            _shownFrame = -1;
            UpdateSpriteFrame();
        }

        public void MoveTo(float x, float y)
        {
            if (_currentState == MobState.Dead) return;
            if (Mathf.Abs(x - transform.position.x) > 0.05f)
                _spriteRenderer.flipX = x < transform.position.x;

            Vector3 newTarget = new Vector3(x, y, transform.position.z);
            if (Vector3.Distance(transform.position, newTarget) > TELEPORT_DISTANCE)
                transform.position = newTarget;

            _targetPosition = newTarget;
        }
        public void UpdateHp(int newHp)
        {
            if (_currentState == MobState.Dead) return;

            if (newHp < currentHp && newHp > 0) PlayHurt();
            currentHp = newHp;
            UpdateUI();
            OnHpChanged?.Invoke(currentHp, maxHp);
        }

        private void UpdateUI()
        {
            if (hpFillTransform != null && maxHp > 0)
                hpFillTransform.localScale = new Vector3(Mathf.Clamp01((float)currentHp / maxHp), 1, 1);
        }

        private void UnloadAtlas()
        {
            if (_atlasHandle.IsValid()) Addressables.Release(_atlasHandle);
            _animationCache.Clear();
        }
        private void AdjustHpBarPosition()
        {
            if ((_spriteRenderer.sprite != null || _anim != null) && hpBarContainer != null)
            {
                // Đỉnh đầu (local): khung va chạm với bộ hình mảnh, khung ảnh với atlas
                float highestPointY = TopY;

                // Cập nhật vị trí Local của thanh HP (cao hơn đỉnh đầu 1 đơn vị)
                hpBarContainer.transform.localPosition = new Vector3(-0.8f, highestPointY, 0f);
            }
        }
        private void OnDestroy() => UnloadAtlas();

        public event Action<int, int> OnHpChanged;
        public int GetId() => mobId;
        public TargetType GetTargetType() => TargetType.Mob;
        public Transform GetTransform() => transform;
        public void OnTargeted() { if (hpBarContainer != null) hpBarContainer.SetActive(true); }
        public void OnDeselected() { if (hpBarContainer != null) hpBarContainer.SetActive(false); }

        public string GetTargetName() => Data.GameData.Mobs.TryGetValue(templateId, out var t) ? t.name : _visualData != null ? _visualData.mobName : $"Quái {templateId}";
        public int GetCurrentHp() => currentHp;
        public int GetMaxHp() => maxHp;


    }
}