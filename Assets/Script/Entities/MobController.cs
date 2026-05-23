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
    public enum MobState { Idle, Walk, Attack, Dead }

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

        private void Awake()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (hpBarContainer != null) hpBarContainer.SetActive(false);
        }

        public void Initialize(int id, short tempId, int hp, int max)
        {
            this.mobId = id;
            this.templateId = tempId;
            this.currentHp = hp;
            this.maxHp = max;
            _targetPosition = transform.position;
            UpdateUI();
        }

        public void SetVisual(MobVisualData visualData)
        {
            this._visualData = visualData;
            _isReady = false;

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
                    _animationCache[MobState.Dead] = ExtractSprites(atlas, "dead");

                    _isReady = true;

                    // 1. Gán frame đầu tiên (lúc này _spriteRenderer mới có ảnh)
                    UpdateSpriteFrame();

                    // 2. Tính toán lại vị trí thanh máu dựa trên ảnh vừa gán
                    AdjustHpBarPosition();
                }
            };
        }

        private Sprite[] ExtractSprites(SpriteAtlas atlas, string action)
        {
            List<Sprite> frames = new List<Sprite>();

            // Xử lý riêng cho trạng thái Walk: cần swap giữa ảnh 0 và 1
            if (action == "walk")
            {
                Sprite frame0 = atlas.GetSprite($"{templateId}_0");
                Sprite frame1 = atlas.GetSprite($"{templateId}_1");

                if (frame0 != null) frames.Add(frame0);
                if (frame1 != null) frames.Add(frame1);
            }
            else
            {
                // Các trạng thái khác vẫn giữ nguyên logic cũ (chỉ 1 ảnh)
                int actionIdx = action switch
                {
                    "idle" => 0,
                    "attack" => 2,
                    "dead" => 3,
                    _ => 0
                };

                string spriteName = $"{templateId}_{actionIdx}";
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

            Debug.LogWarning($"[Mob] Không tìm thấy ảnh cho hành động '{action}' của Mob {templateId} trong Atlas!");
            return new Sprite[0];
        }
        private void Update()
        {
            if (!_isReady || _currentState == MobState.Dead) return;

            // 1. Animation logic
            _frameTimer += Time.deltaTime;
            float animSpeed = (_visualData != null) ? _visualData.frameRate : 0.15f;
            if (_frameTimer >= animSpeed)
            {
                _frameTimer = 0f;
                _currentFrameIndex++;
                UpdateSpriteFrame();
            }

            // 2. Di chuyển mượt (Lerp/MoveTowards) theo Server
            float distance = Vector3.Distance(transform.position, _targetPosition);
            if (distance > 0.05f)
            {
                float requiredSpeed = distance / 0.2f; 
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, requiredSpeed * 1.15f * Time.deltaTime);
                if (_currentState != MobState.Walk) ChangeState(MobState.Walk);
            }
            else
            {
                transform.position = _targetPosition;
                if (_currentState == MobState.Walk) ChangeState(MobState.Idle);
            }
        }

        private void UpdateSpriteFrame()
        {
            if (!_isReady || !_animationCache.ContainsKey(_currentState)) return;

            Sprite[] frames = _animationCache[_currentState];
            if (frames == null || frames.Length == 0) return;

            _currentFrameIndex %= frames.Length;
            _spriteRenderer.sprite = frames[_currentFrameIndex];
        }

        public void ChangeState(MobState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            _currentFrameIndex = 0;
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

            currentHp = newHp;
            UpdateUI(); // Cập nhật thanh máu trên đỉnh đầu

         
            OnHpChanged?.Invoke(currentHp, maxHp);

          //  if (currentHp <= 0) Die();
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
            if (_spriteRenderer.sprite != null && hpBarContainer != null)
            {
                // Lấy điểm Y cao nhất của Sprite hiện tại (tính theo Local Space)
                float highestPointY = _spriteRenderer.sprite.bounds.max.y;

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

        public string GetTargetName() => _visualData.mobName;
        public int GetCurrentHp() => currentHp;
        public int GetMaxHp() => maxHp;


    }
}