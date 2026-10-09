using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D; // Bắt buộc phải có để dùng SpriteAtlas
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using Assets.Script.Database;
using Assets.Script.Models;

namespace Assets.Script.Skill
{
    public class SkillSlotUI : MonoBehaviour
    {
        public int slotIndex; // Đánh số 0, 1, 2, 3, 4 (tương ứng phím 1-5)
        public Image iconImg;
        public GameObject highlightObj; // Viền sáng lên khi được chọn

        [HideInInspector]
        public int assignedSkillId = -1; // -1 nghĩa là ô trống

        // Gọi hàm này để gán skill vào ô
        // Chữ tên kỹ năng (tạo bằng code) — hiện khi kỹ năng chưa có icon trong SkillDatabase
        private TMPro.TextMeshProUGUI _nameText;

        private void SetNameText(string text)
        {
            if (_nameText == null)
            {
                var go = new GameObject("SkillName", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                _nameText = go.AddComponent<TMPro.TextMeshProUGUI>();
                _nameText.fontSize = 14;
                _nameText.alignment = TMPro.TextAlignmentOptions.Center;
                _nameText.enableWordWrapping = true;
                _nameText.raycastTarget = false;
            }
            _nameText.text = text;
        }

        public void AssignSkill(int templateId, SkillDatabaseSO database)
        {
            assignedSkillId = templateId;

            // GĐ12: icon = ảnh kho số skill_template.icon_id (ImageBank); không có ảnh → cách cũ (atlas trong SkillDatabase) → tên chữ
            if (templateId != -1 && Assets.Script.Data.GameData.Skills.TryGetValue(templateId, out var st) && st.iconId > 0)
            {
                int forSkill = templateId;
                Assets.Script.Core.ImageBank.Get(st.iconId, sprite =>
                {
                    if (iconImg == null || assignedSkillId != forSkill) return;
                    if (sprite != null) { iconImg.sprite = sprite; iconImg.enabled = true; SetNameText(""); }
                    else AssignFromDatabase(forSkill, database);
                });
                return;
            }
            AssignFromDatabase(templateId, database);
        }

        private void AssignFromDatabase(int templateId, SkillDatabaseSO database)
        {
            SkillVisualData visualData = templateId == -1 || database == null ? null : database.GetSkillVisual(templateId);

            // Chưa có icon → ghi tên kỹ năng (lấy từ dữ liệu server). [CẦN ĐIỀN] thêm icon vào Assets/SO/SkillDatabase.asset
            bool noIcon = templateId != -1 && (visualData == null || visualData.iconSprite == null);
            SetNameText(noIcon && Assets.Script.Data.GameData.Skills.TryGetValue(templateId, out var tpl) ? tpl.name
                        : noIcon ? $"Skill {templateId}" : "");

            if (visualData != null && visualData.iconSprite != null && visualData.iconSprite.RuntimeKeyIsValid())
            {
                int forSkill = templateId;
                LoadAtlas(visualData.iconSprite).Completed += handle =>
                {
                    // UI đã huỷ, hoặc ô đã được gán chiêu khác trong lúc chờ tải → bỏ
                    if (iconImg == null || assignedSkillId != forSkill || handle.Status != AsyncOperationStatus.Succeeded) return;
                    string spriteName = "img" + visualData.iconId;
                    Sprite icon = handle.Result.GetSprite(spriteName);
                    if (icon == null) Debug.LogWarning($"[SkillSlotUI] Không tìm thấy ảnh '{spriteName}' trong Atlas!");
                    iconImg.sprite = icon;
                    iconImg.enabled = icon != null;
                };
            }
            else
            {
                iconImg.enabled = false;
            }
        }

        // Atlas icon tải 1 lần cho mỗi tham chiếu, dùng chung mọi ô, giữ suốt phiên chơi (vài trăm KB).
        // Gọi LoadAssetAsync() lần 2 trên cùng AssetReference là lỗi Addressables → không dùng hàm của AssetReference.
        private static readonly Dictionary<object, AsyncOperationHandle<SpriteAtlas>> AtlasCache = new Dictionary<object, AsyncOperationHandle<SpriteAtlas>>();

        private static AsyncOperationHandle<SpriteAtlas> LoadAtlas(AssetReference reference)
        {
            object key = reference.RuntimeKey;
            if (!AtlasCache.TryGetValue(key, out var h) || !h.IsValid())
            {
                h = Addressables.LoadAssetAsync<SpriteAtlas>(key);
                AtlasCache[key] = h;
            }
            return h;
        }

        // Bật/tắt viền sáng
        public void SetSelected(bool isSelected)
        {
            if (highlightObj != null)
                highlightObj.SetActive(isSelected);
        }

    }
}
