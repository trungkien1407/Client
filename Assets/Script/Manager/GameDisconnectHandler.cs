using Assets.Script.Constants;
using Assets.Script.Map;
using Assets.Script.Network;
using Assets.Script.UI;
using UnityEngine;

namespace Assets.Script.Manager
{
    public class GameDisconnectHandler : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject inGameUI;

        void Start()
        {
            // 1. Lắng nghe sự kiện đứt kết nối (socket rớt, server sập)
            NetworkManager.Instance.OnDisconnected += HandleServerDisconnect;

            // 2. Lắng nghe gói tin xác nhận Đăng xuất từ Server
            NetworkEventDispatcher.Instance.AddHandler(Cmd.LOGOUT, OnLogoutResponse);
        }

        private void OnLogoutResponse(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            short status = reader.ReadShort();
            reader.Cleanup();

            if (status == 0) // Server cho phép đăng xuất thành công
            {
                Debug.Log("[Client] Đăng xuất thành công. Đóng kết nối TCP và quay về Login.");
                DoCleanupAndShowLogin("Đã đăng xuất an toàn!");
            }
            else
            {
                if (PopupAndLoad.Instance != null)
                {
                    PopupAndLoad.Instance.HideLoading();
                    PopupAndLoad.Instance.ShowPopup("Lỗi đăng xuất! Vui lòng thử lại.");
                }
            }
        }

        // ==========================================
        // LUỒNG 2: MẤT KẾT NỐI BỊ ĐỘNG (RỚT MẠNG)
        // ==========================================
        private void HandleServerDisconnect()
        {
            Debug.Log("[Client] Đã mất kết nối với máy chủ! Bắt đầu dọn dẹp Scene...");
            DoCleanupAndShowLogin("Mất kết nối với máy chủ. Vui lòng đăng nhập lại!");
        }

        // ==========================================
        // HÀM DÙNG CHUNG: DỌN DẸP, CẮT SOCKET VÀ CHUYỂN SCENE
        // ==========================================
        private void DoCleanupAndShowLogin(string popupMessage)
        {
            // 0. ĐÓNG HOÀN TOÀN SOCKET CŨ VÀ DỌN DẸP NETWORK
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.Disconnect();
            }

            // 1. DỌN DẸP RAM VÀ SCENE
            if (MapManager.Instance != null) MapManager.Instance.ClearMap();
            if (NetworkPlayerManager.Instance != null) NetworkPlayerManager.Instance.ClearAllPlayers();
            if (SkillBarManager.Instance != null) SkillBarManager.Instance.ClearSkills();
            if (NetworkMobManager.Instance != null) NetworkMobManager.Instance.ClearAllMobs();
            if (NetworkNpcManager.Instance != null) NetworkNpcManager.Instance.ClearAllNpcs();
            if (UISetup.Instance != null) UISetup.Instance.ClearUI();

            // 2. XỬ LÝ GIAO DIỆN
            if (inGameUI != null) inGameUI.SetActive(false);
            Instantiate(loginPanel);

            // 3. HIỂN THỊ THÔNG BÁO VÀ TẮT LOADING
            if (PopupAndLoad.Instance != null)
            {
                PopupAndLoad.Instance.HideLoading();
                if (!string.IsNullOrEmpty(popupMessage))
                {
                    PopupAndLoad.Instance.ShowPopup(popupMessage);
                }
            }
        }

        void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnDisconnected -= HandleServerDisconnect;
            }

            if (NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.LOGOUT, OnLogoutResponse);
            }
        }
    }
}