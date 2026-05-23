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
    public void AssignSkill(int templateId, SkillDatabaseSO database)
    {
        assignedSkillId = templateId;
        SkillVisualData visualData = database.GetSkillVisual(templateId);

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

    // Hàm này được gọi khi User CLICK thẳng vào nút UI của ô này
    public void OnClickSlot()
    {
        if (assignedSkillId == -1) return;

        // Báo cho Manager biết ô này vừa được chọn
        SkillBarManager.Instance.SelectSlotAndUse(slotIndex);
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