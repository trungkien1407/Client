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

        // ---------------------------------------------------------------- HỒI CHIÊU
        // Lớp tối quay ngược kim đồng hồ + số giây còn lại (tạo bằng code, không cần sửa prefab). Đếm ở Combat.SkillCooldowns.
        private Image _cdMask;
        private TMPro.TextMeshProUGUI _cdText;
        private static Sprite _white;   // Image kiểu Filled cần có sprite mới vẽ được phần quạt

        /// <summary>Ô đang hiện lớp hồi chiêu (AutoTestRunner đọc).</summary>
        public bool ShowingCooldown => _cdMask != null && _cdMask.enabled;

        private void Update()
        {
            float left = assignedSkillId >= 0 ? Combat.SkillCooldowns.Remaining(assignedSkillId) : 0f;
            if (left <= 0f)
            {
                if (ShowingCooldown) { _cdMask.enabled = false; _cdText.text = ""; }
                return;
            }
            EnsureCooldownMask();
            _cdMask.enabled = true;
            _cdMask.fillAmount = Combat.SkillCooldowns.Fraction(assignedSkillId);
            _cdText.text = left >= 1f ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0");
        }

        private void EnsureCooldownMask()
        {
            if (_cdMask != null) return;
            if (_white == null) _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            var go = new GameObject("CooldownMask", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _cdMask = go.AddComponent<Image>();
            _cdMask.sprite = _white;
            _cdMask.color = new Color(0f, 0f, 0f, 0.6f);
            _cdMask.type = Image.Type.Filled;
            _cdMask.fillMethod = Image.FillMethod.Radial360;
            _cdMask.fillOrigin = (int)Image.Origin360.Top;
            _cdMask.fillClockwise = false;   // phần tối thu dần theo chiều kim đồng hồ
            _cdMask.raycastTarget = false;   // vẫn bấm được ô (bấm khi chưa hồi → nhắc trong chat)

            var tgo = new GameObject("CooldownText", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            _cdText = tgo.AddComponent<TMPro.TextMeshProUGUI>();
            _cdText.fontSize = 20;
            _cdText.fontStyle = TMPro.FontStyles.Bold;
            _cdText.alignment = TMPro.TextAlignmentOptions.Center;
            _cdText.color = Color.white;
            _cdText.raycastTarget = false;
        }

        // Bật/tắt viền sáng
        public void SetSelected(bool isSelected)
        {
            if (highlightObj != null)
                highlightObj.SetActive(isSelected);
        }

    }
}
