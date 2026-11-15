using Assets.Script.Database;
using Assets.Script.Interfaces;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Assets.Script.Entities
{
    public class NpcEntity : MonoBehaviour, ITargetable
    {
        [Header("Ghi chú: Kéo object con vào đây")]
        public SpriteRenderer headRenderer;
        public SpriteRenderer legRenderer;

        [Header("Targeting UI")]
        private int npcId;
        /// <summary>Loại NPC (npc_template.id): 1 Hokage, 2 Thợ Rèn... — cùng 1 loại có thể đứng ở nhiều làng (GĐ8).</summary>
        public int TemplateId { get; private set; }
        [SerializeField] private GameObject targetArrow;
        [SerializeField] private TextMeshPro npcNameText;
        [SerializeField] private float namePushUpDistance = 0.2f;

        private Vector3 _originalNamePosition;
        private Color _originalNameColor;
        private Fx.PointerArrow _hintArrow;   // mũi tên nhỏ luôn hiện trên tên (2026-11-15) — ẩn khi đang chọn (mũi tên chọn hiện thay)

        /// <summary>Chỗ mũi tên nhỏ: ngay trên tên NPC.</summary>
        private Vector3 HintPos => _originalNamePosition + new Vector3(0f, 0.42f, 0f);

        private void Awake()
        {
            if (targetArrow != null) targetArrow.SetActive(false);
            if (headRenderer != null) _bodyBase = headRenderer.transform.localScale;
            if (npcNameText != null)
            {
                _originalNamePosition = npcNameText.transform.localPosition;
                _originalNameColor = npcNameText.color;
            }
            _hintArrow = Fx.PointerArrow.Attach(transform, HintPos, 0.55f, 20);
        }

        // TRUYỀN THÊM SPRITE ATLAS VÀO HÀM SETUP
        public void Setup(NpcDatabaseSO.NpcConfig config, int npcId, SpriteAtlas globalAtlas)
        {
            gameObject.name = $"NPC_{config.defaultName}_{config.templateId}";
            TemplateId = config.templateId;
            npcNameText.text = config.defaultName;
            // Tên NPC trắng: thêm viền + bóng → không chìm vào trời / tuyết trắng. Gọi ở đây (không ở Awake): Awake của NpcEntity
            // có thể chạy TRƯỚC Awake của chữ TextMeshPro con → chữ khởi tạo xong lại vẽ bằng vật liệu cũ (đã gặp: không thấy viền)
            Assets.Script.UI.Kit.UIStyle.Readable(npcNameText);
            this.npcId = npcId;

            // Lấy ảnh tức thì từ Atlas. (Lưu ý: Không tốn chi phí I/O vì Atlas đã ở sẵn trong RAM)
            if (globalAtlas != null)
            {
                headRenderer.sprite = globalAtlas.GetSprite(config.headSpriteName);
                legRenderer.sprite = globalAtlas.GetSprite(config.legSpriteName);
            }
        }

        /// <summary>Số bộ hình đang dùng (-1 = hình mặc định trong atlas).</summary>
        public int AppliedArt { get; private set; } = -1;
        private float _appliedScale = 1f;

        /// <summary>Cỡ vẽ đang dùng (npc_template.scale) — AutoTestRunner kiểm.</summary>
        public float AppliedScale => _appliedScale;

        /// <summary>
        /// Hình theo server (npc_template.art → NpcArtSO "NpcArt_{art}"): thay thân + chân, dời tên / mũi tên chọn lên trên đầu.
        /// art &lt; 0 hoặc chưa có hình trong kho → giữ hình mặc định (atlas). Gọi lại được khi GAME_DATA_NPCS tới muộn.
        /// scale = npc_template.scale (GĐ26): ảnh NPC mỗi bộ vẽ một tỉ lệ → nhân cỡ thân + chân (chân vẫn chạm đất) cho cao ngang người chơi.
        /// </summary>
        public void ApplyArt(int art, float scale = 1f)
        {
            if (art < 0 || (art == AppliedArt && Mathf.Approximately(scale, _appliedScale))) return;
            AppliedArt = art;
            _appliedScale = scale;
            Core.AssetSource.Load<Models.NpcArtSO>(Models.NpcArtSO.Address(art), "NpcArt/" + Models.NpcArtSO.Address(art), so =>
            {
                if (this == null || so == null || so.body == null || AppliedArt != art) return;
                legRenderer.sprite = so.legs;
                var bp = so.BodyPos * scale;   // thân lệch theo eo (đo sẵn), không đặt thẳng tâm ảnh
                float cx = so.CenterX * scale;  // giữa khối hình (thân + chân) → dời cả hình cho nằm đúng dưới tên
                legRenderer.transform.localPosition = new Vector3(-cx, 0f, 0f);
                legRenderer.transform.localScale = new Vector3(scale, scale, 1f);   // gốc chân (pivot giữa-đáy) giữ độ cao → vẫn đứng trên đất
                headRenderer.sprite = so.body;
                headRenderer.transform.localPosition = new Vector3(bp.x - cx, bp.y, 0f);
                headRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                _bodyBase = new Vector3(scale, scale, 1f);
                float top = so.TopY * scale;
                if (npcNameText != null)
                {
                    _originalNamePosition = new Vector3(_originalNamePosition.x, top + 0.35f, _originalNamePosition.z);
                    npcNameText.transform.localPosition = _originalNamePosition;
                }
                if (_hintArrow != null) _hintArrow.SetBase(HintPos);
                if (targetArrow != null)
                {
                    var p = targetArrow.transform.localPosition;
                    targetArrow.transform.localPosition = new Vector3(p.x, top + 0.75f, p.z);
                }
            });
        }

        private Vector3 _bodyBase = Vector3.one;   // cỡ thân khi đứng yên (thở quanh cỡ này)

        /// <summary>
        /// NPC "thở" khi đứng: ảnh THÂN (pivot giữa-đáy = eo) phồng lên / xẹp xuống rất nhẹ theo hình sin, chân đứng yên.
        /// Lệch pha theo id để các NPC không thở cùng nhịp.
        /// </summary>
        private void Update()
        {
            if (headRenderer == null) return;
            float t = Time.time * 2.2f + npcId * 1.7f;     // ~0,35 nhịp / giây
            float s = Mathf.Sin(t);
            headRenderer.transform.localScale = new Vector3(_bodyBase.x * (1f - 0.006f * s), _bodyBase.y * (1f + 0.018f * s), _bodyBase.z);
        }

        // --- Hiện thực Interface ITargetable ---
     
        public string GetTargetName() => npcNameText.text;

        public int GetId() => npcId;
        public TargetType GetTargetType() => TargetType.NPC;
        public Transform GetTransform() => transform;

        public void OnTargeted()
        {
            if (targetArrow != null) targetArrow.SetActive(true);
            if (_hintArrow != null && targetArrow != null) _hintArrow.gameObject.SetActive(false);

            if (npcNameText != null)
            {
                npcNameText.transform.localPosition = _originalNamePosition + new Vector3(0, namePushUpDistance, 0);

                ColorUtility.TryParseHtmlString("#00FF00", out Color targetColor);
                npcNameText.color = targetColor;
            }
        }

        public void OnDeselected()
        {
            if (targetArrow != null) targetArrow.SetActive(false);
            if (_hintArrow != null) _hintArrow.gameObject.SetActive(true);

            if (npcNameText != null)
            {
                npcNameText.transform.localPosition = _originalNamePosition;
                npcNameText.color = _originalNameColor;
            }
        }
    }
}