using Assets.Script.Core;
using Assets.Script.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// GẮN ICON KHO ẢNH vào 1 ô / dòng đã có chữ (icon = ImageBank img{iconId} do server gửi).
    ///   Left — icon bên trái, chữ dịch sang phải (dòng danh sách: cửa hàng, chợ...)
    ///   Top  — icon trên, chữ dồn xuống dưới (ô lưới nhỏ: túi đồ, rương, trang bị)
    /// Chưa có ảnh (iconId 0 / chưa nhập kho) → không đổi gì, ô vẫn hiện chữ như cũ.
    /// </summary>
    public static class ItemIcon
    {
        public enum Place { Left, Top }

        /// <summary>Icon của mẫu vật phẩm {tplId}.</summary>
        public static Image ForItem(Component cell, int tplId, Place place, float size = 40) =>
            GameData.Items.TryGetValue(tplId, out var t) ? Add(cell, t.iconId, place, size) : null;

        /// <summary>Icon ảnh số {imgId} (VD icon kỹ năng).</summary>
        public static Image Add(Component cell, int imgId, Place place, float size = 40)
        {
            if (cell == null || imgId <= 0) return null;
            var img = UIKit.Rect("Icon", cell.transform).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false;
            var rt = img.rectTransform;
            if (place == Place.Left) rt.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(size, size), new Vector2(0, 0.5f));
            else rt.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(size, size), new Vector2(0.5f, 1));

            var label = cell.GetComponentInChildren<TextMeshProUGUI>(true);
            ImageBank.Get(imgId, sprite =>
            {
                if (img == null || sprite == null) return;
                img.sprite = sprite;
                img.enabled = true;
                if (label == null) return;
                var lr = label.rectTransform;
                if (place == Place.Left) lr.offsetMin = new Vector2(lr.offsetMin.x + size + 6, lr.offsetMin.y);
                else
                {
                    lr.offsetMax = new Vector2(lr.offsetMax.x, lr.offsetMax.y - size);
                    label.alignment = TextAlignmentOptions.Bottom;
                    label.enableAutoSizing = true;          // tên dài tự thu nhỏ, không đè lên icon
                    label.fontSizeMin = 8;
                    label.fontSizeMax = Mathf.Min(label.fontSize, 12);
                }
            });
            return img;
        }
    }
}
