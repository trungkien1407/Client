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
            npcNameText.text = config.defaultName;
            this.npcId = npcId;

            // Lấy ảnh tức thì từ Atlas. (Lưu ý: Không tốn chi phí I/O vì Atlas đã ở sẵn trong RAM)
            if (globalAtlas != null)
            {
                headRenderer.sprite = globalAtlas.GetSprite(config.headSpriteName);
                legRenderer.sprite = globalAtlas.GetSprite(config.legSpriteName);
            }
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