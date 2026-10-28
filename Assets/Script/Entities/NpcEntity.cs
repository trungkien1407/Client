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

        private void Awake()
        {
            if (targetArrow != null) targetArrow.SetActive(false);
            if (npcNameText != null)
            {
                _originalNamePosition = npcNameText.transform.localPosition;
                _originalNameColor = npcNameText.color;
            }
        }

        // TRUYỀN THÊM SPRITE ATLAS VÀO HÀM SETUP
        public void Setup(NpcDatabaseSO.NpcConfig config, int npcId, SpriteAtlas globalAtlas)
        {
            gameObject.name = $"NPC_{config.defaultName}_{config.templateId}";
            TemplateId = config.templateId;
            npcNameText.text = config.defaultName;
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

        /// <summary>
        /// Hình theo server (npc_template.art → NpcArtSO "NpcArt_{art}"): thay thân + chân, dời tên / mũi tên chọn lên trên đầu.
        /// art &lt; 0 hoặc chưa có hình trong kho → giữ hình mặc định (atlas). Gọi lại được khi GAME_DATA_NPCS tới muộn.
        /// </summary>
        public void ApplyArt(int art)
        {
            if (art < 0 || art == AppliedArt) return;
            AppliedArt = art;
            Core.AssetSource.Load<Models.NpcArtSO>(Models.NpcArtSO.Address(art), "NpcArt/" + Models.NpcArtSO.Address(art), so =>
            {
                if (this == null || so == null || so.body == null || AppliedArt != art) return;
                legRenderer.sprite = so.legs;
                legRenderer.transform.localPosition = Vector3.zero;
                headRenderer.sprite = so.body;
                var bp = so.BodyPos;   // thân lệch theo eo (đo sẵn), không đặt thẳng tâm ảnh
                headRenderer.transform.localPosition = new Vector3(bp.x, bp.y, 0f);
                float top = so.TopY;
                if (npcNameText != null)
                {
                    _originalNamePosition = new Vector3(_originalNamePosition.x, top + 0.35f, _originalNamePosition.z);
                    npcNameText.transform.localPosition = _originalNamePosition;
                }
                if (targetArrow != null)
                {
                    var p = targetArrow.transform.localPosition;
                    targetArrow.transform.localPosition = new Vector3(p.x, top + 0.75f, p.z);
                }
            });
        }

        // --- Hiện thực Interface ITargetable ---
     
        public string GetTargetName() => npcNameText.text;

        public int GetId() => npcId;
        public TargetType GetTargetType() => TargetType.NPC;
        public Transform GetTransform() => transform;

        public void OnTargeted()
        {
            if (targetArrow != null) targetArrow.SetActive(true);

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

            if (npcNameText != null)
            {
                npcNameText.transform.localPosition = _originalNamePosition;
                npcNameText.color = _originalNameColor;
            }
        }
    }
}