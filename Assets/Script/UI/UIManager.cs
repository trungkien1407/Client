using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Manager
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Canvas Layers (Kéo các Canvas cha vào đây)")]
        [SerializeField] private GameObject hudCanvas;     // Chứa nút đánh, máu, mana
     //   [SerializeField] private GameObject windowCanvas;  // Chứa túi đồ, nhân vật
        [SerializeField] private GameObject systemCanvas;  // Chứa popup, loading

        [Header("In-Game Windows (Các Panel trong Window Canvas)")]
        // [CẦN ĐIỀN khi làm UI] kéo các panel cửa sổ vào đây. Chưa có thì để trống (game vẫn chạy).
        [Assets.Script.Core.Optional, SerializeField] private GameObject inventoryPanel; // Hành trang
        [Assets.Script.Core.Optional, SerializeField] private GameObject characterPanel; // Bảng chỉ số nhân vật
        [Assets.Script.Core.Optional, SerializeField] private GameObject questPanel;     // Nhiệm vụ

        // Danh sách để quản lý các cửa sổ dễ dàng hơn
        private List<GameObject> allWindows = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Gom tất cả các cửa sổ vào list để tiện việc dùng vòng lặp tắt hết
            if (inventoryPanel != null) allWindows.Add(inventoryPanel);
            if (characterPanel != null) allWindows.Add(characterPanel);
            if (questPanel != null) allWindows.Add(questPanel);

            // Khởi tạo trạng thái ban đầu: Chỉ hiện HUD và System, tắt các cửa sổ
            CloseAllWindows();
        }

        // ==========================================
        // QUẢN LÝ TẦNG CANVAS (CANVAS LAYERS)
        // ==========================================
        public void ShowHUD(bool isShow)
        {
            if (hudCanvas != null) hudCanvas.SetActive(isShow);
        }

        // ==========================================
        // QUẢN LÝ CỬA SỔ IN-GAME (WINDOWS)
        // ==========================================

        // Hàm này tắt tất cả Hành trang, Nhân vật...
        public void CloseAllWindows()
        {
            foreach (var window in allWindows)
            {
                window.SetActive(false);
            }
        }

        // ==========================================
        // TÍCH HỢP GỌI NHANH POPUP (Tiện ích)
        // ==========================================
        // Bạn có thể viết thêm hàm này để gọi Popup thông qua UIManager cho đồng bộ, 
        // hoặc gọi trực tiếp PopupAndLoad.Instance.ShowPopup đều được.
        public void ShowSystemMessage(string msg, System.Action onClose = null)
        {
            // Gọi sang class PopupAndLoad của bạn
            PopupAndLoad.Instance.ShowPopup(msg, onClose);
        }
    }
}