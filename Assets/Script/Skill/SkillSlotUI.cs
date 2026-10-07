using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D; // Bắt buộc phải có để dùng SpriteAtlas
using UnityEngine.AddressableAssets;
using Assets.Script.Database;
using Assets.Script.Models;

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
        SkillVisualData visualData = templateId == -1 ? null : database.GetSkillVisual(templateId);

        // Chưa có icon → ghi tên kỹ năng (lấy từ dữ liệu server). [CẦN ĐIỀN] thêm icon vào Assets/SO/SkillDatabase.asset
        bool noIcon = templateId != -1 && (visualData == null || visualData.iconSprite == null);
        SetNameText(noIcon && Assets.Script.Data.GameData.Skills.TryGetValue(templateId, out var tpl) ? tpl.name
                    : noIcon ? $"Skill {templateId}" : "");

        if (visualData != null && visualData.iconSprite != null)
        {
            // Tải SpriteAtlas từ Addressables
            visualData.iconSprite.LoadAssetAsync().Completed += (handle) =>
            {
                // Kiểm tra xem UI có bị tắt/hủy trước khi tải xong không
                if (iconImg != null && handle.Result != null)
                {
                    SpriteAtlas atlas = handle.Result;

                    // Lấy Sprite con từ trong Atlas ra. 
                    // LƯU Ý: Đổi "icon_" thành tiền tố mà bạn đặt tên cho các file ảnh (VD: "skill_101")
                    string spriteName = "img" + visualData.iconId;
                    Sprite icon = atlas.GetSprite(spriteName);

                    if (icon != null)
                    {
                        iconImg.sprite = icon;
                        iconImg.enabled = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[SkillSlotUI] Không tìm thấy ảnh tên '{spriteName}' trong Atlas!");
                        iconImg.enabled = false;
                    }
                }
            };
        }
        else
        {
            iconImg.enabled = false;
        }
    }

    // Bật/tắt viền sáng
    public void SetSelected(bool isSelected)
    {
        if (highlightObj != null)
            highlightObj.SetActive(isSelected);
    }

    private void OnDestroy()
    {
        // Giải phóng RAM khi ô UI này bị hủy (Chuyển scene)
        if (assignedSkillId != -1)
        {
            // Tùy theo cách bạn setup Database, Addressables sẽ tự động quản lý Ref-Count
            // và nhả RAM Atlas ra nếu không còn ô UI nào dùng nó nữa.
        }
    }
}