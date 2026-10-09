using UnityEngine;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// BẢNG MÀU CHUNG của UI (asset Assets/Resources/UI/UITheme.asset — sửa màu trong Inspector, không cần sửa code).
    /// Hình dạng (sprite, font, kích thước) nằm ở prefab mẫu Assets/Resources/UI/Kit/*.prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Naruto/UI Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        [Tooltip("Nền cửa sổ / khung")] public Color panel = new Color(0.08f, 0.10f, 0.14f, 0.94f);
        [Tooltip("Thanh tiêu đề cửa sổ")] public Color header = new Color(0.85f, 0.45f, 0.10f, 1f);
        [Tooltip("Nút thường")] public Color button = new Color(0.22f, 0.27f, 0.36f, 1f);
        [Tooltip("Nút / ô đang chọn, chữ nổi bật")] public Color buttonHot = new Color(0.90f, 0.55f, 0.15f, 1f);
        [Tooltip("Ô đồ / ô danh sách")] public Color slot = new Color(0.16f, 0.19f, 0.25f, 1f);
        [Tooltip("Chữ thường")] public Color text = new Color(0.95f, 0.95f, 0.95f, 1f);
        [Tooltip("Chữ phụ (mờ)")] public Color dimText = new Color(0.70f, 0.74f, 0.80f, 1f);
        [Tooltip("Chữ tốt (thành công, đủ điều kiện)")] public Color goodText = new Color(0.45f, 1f, 0.45f, 1f);
        [Tooltip("Chữ xấu (lỗi, thiếu điều kiện)")] public Color badText = new Color(1f, 0.45f, 0.45f, 1f);

        private static UITheme _current;

        /// <summary>Asset trong Resources/UI; chưa tạo thì dùng màu mặc định ở trên.</summary>
        public static UITheme Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<UITheme>(UIPrefabs.ThemePath);
                if (_current == null) _current = CreateInstance<UITheme>();
                return _current;
            }
        }
    }
}
