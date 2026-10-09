using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// KHUNG CỬA SỔ (prefab mẫu UI/Kit/Window): thanh tiêu đề kéo được, chữ tiêu đề, nút đóng, vùng nội dung Body.
    /// Mọi prefab cửa sổ là bản biến thể (variant) của khung này → sửa khung 1 lần, mọi cửa sổ đổi theo.
    /// </summary>
    public class WindowFrame : MonoBehaviour
    {
        public RectTransform header;
        public TextMeshProUGUI title;
        public Button close;
        [Tooltip("Vùng nội dung — cửa sổ vẽ mọi thứ vào đây")] public RectTransform body;
    }
}
