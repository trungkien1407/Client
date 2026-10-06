using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
// (đã bỏ "using UnityEditor;" — namespace chỉ có trong Editor, để lại sẽ làm build game lỗi)

namespace Assets.Script.Manager
{
    public class PopupAndLoad : MonoBehaviour
    {
        public static PopupAndLoad Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;      // Nơi chứa toàn bộ khung UI của Popup
        [SerializeField] private TextMeshProUGUI txtMessage; // Dùng để hiển thị chữ (Lỗi, Thành công, Đang tải...)
        [SerializeField] private Button closePopup;          // Nút Đóng / OK

        [SerializeField] private GameObject loaddingPanel;      // Nơi chứa toàn bộ khung UI của Popup

        // Biến lưu trữ hành động (callback) sẽ chạy khi ấn nút Đóng
        private Action currentCloseCallback;

        // DÙNG AWAKE ĐỂ LÀM SINGLETON
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Nếu Popup này dùng chung cho toàn game (chuyển Scene không mất), hãy bật dòng dưới lên:
                // DontDestroyOnLoad(gameObject); 
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Gắn sự kiện cho nút Đóng
            if (closePopup != null)
            {
                closePopup.onClick.AddListener(OnCloseButtonClicked);
            }

            // Tắt popup lúc mới khởi tạo
            if (popupPanel != null) popupPanel.SetActive(false);
        }
        public void ShowLoading()
        {
            loaddingPanel.SetActive(true);
        }
        public void HideLoading()
        {
            loaddingPanel.SetActive(false);
        }

        // ==========================================
        // HÀM HIỂN THỊ POPUP (HỖ TRỢ CALLBACK VÀ ẨN NÚT)
        // Tham số showButton mặc định là true. Truyền false để biến thành Loading Screen.
        // ==========================================
        public void ShowPopup(string message, Action onClose = null, bool showButton = true)
        {
            // 1. Cập nhật nội dung thông báo
            if (txtMessage != null) txtMessage.text = message;

            // 2. Lưu lại cái callback này
            currentCloseCallback = onClose;

            // 3. Ẩn/Hiện nút đóng dựa vào tham số truyền vào
            if (closePopup != null)
            {
                closePopup.gameObject.SetActive(showButton);
            }

            // 4. Hiển thị Popup lên màn hình
            if (popupPanel != null) popupPanel.SetActive(true);
        }

        // ==========================================
        // HÀM ẨN POPUP QUA CODE (Dùng khi xử lý ngầm xong)
        // ==========================================
        public void HidePopup()
        {
            // 1. Ẩn Popup đi
            if (popupPanel != null) popupPanel.SetActive(false);

            // 2. Xóa sạch callback cũ để tránh rác hoặc gọi nhầm cho lần ShowPopup tiếp theo
            currentCloseCallback = null;
        }

        // ==========================================
        // XỬ LÝ KHI NGƯỜI DÙNG BẤM NÚT ĐÓNG
        // ==========================================
        private void OnCloseButtonClicked()
        {
            // 1. Lưu tạm callback ra một biến khác (Bắt buộc phải làm vậy vì HidePopup sẽ xóa callback)
            Action tempCallback = currentCloseCallback;

            // 2. Gọi hàm ẩn UI và dọn dẹp
            HidePopup();

            // 3. Thực thi Callback (Dấu ? giúp kiểm tra an toàn: Nếu khác null thì mới Invoke)
            tempCallback?.Invoke();
        }

        // Dọn dẹp sự kiện để tránh Memory Leak khi object bị hủy
        private void OnDestroy()
        {
            if (closePopup != null)
            {
                closePopup.onClick.RemoveListener(OnCloseButtonClicked);
            }
        }
    }
}