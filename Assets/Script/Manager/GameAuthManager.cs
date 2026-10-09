using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.UI;
using Assets.Script.Core;
using Assets.Script.UI.Kit;

namespace Assets.Script.Manager
{
    /// <summary>
    /// MÀN ĐĂNG NHẬP: form đăng nhập / đăng ký / tạo nhân vật / "Tiếp tục" bằng tài khoản đã lưu.
    ///   Gửi: LOGIN, REGISTER (CREATE_CHARACTER do CharacterCreationManager gửi) — tự kết nối lại nếu đang rớt mạng.
    ///   Nhận: kết quả → lời báo (AuthMessages) hoặc vào game (EnterWorld), rồi tự huỷ màn này.
    /// </summary>
    public class GameAuthManager : NetworkListener
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject registerPanel;
        [SerializeField] private GameObject createCharPanel;
        [SerializeField] private GameObject quickLoginPanel;
        [SerializeField] private GameObject HUDCanvas;

        /// <summary>Màn đăng nhập tạo lại sau khi mất kết nối (GameDisconnectHandler) — prefab không giữ được tham chiếu tới HUD của scene.</summary>
        public void SetHud(GameObject hud) { if (hud != null) HUDCanvas = hud; }

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField inputLoginUser;
        [SerializeField] private TMP_InputField inputLoginPass;
        [SerializeField] private TMP_InputField inputRegEmail;
        [SerializeField] private TMP_InputField inputRegUser;
        [SerializeField] private TMP_InputField inputRegPass;

        [Header("Buttons")]
        [SerializeField] private Button btnLoginSend;
        [SerializeField] private Button btnRegisterSend;
        [SerializeField] private Button btnQuickLogin;
        [SerializeField] private Button btnOpenRegister;
        [SerializeField] private Button btnCloseRegister;
        [SerializeField] private Button btnSwitchAccount;

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI txtQuickLogin;

        private string _pendingUser;
        private string _pendingPass;
        private bool _pendingFromSaved; // đang đăng nhập bằng mật khẩu đã lưu (nút "Tiếp tục")
        private Button _rememberBtn;

        void Start()
        {
            // UI Listeners
            if (btnLoginSend != null) btnLoginSend.onClick.AddListener(() => DoSocketLogin(inputLoginUser.text, inputLoginPass.text));
            if (btnRegisterSend != null) btnRegisterSend.onClick.AddListener(() => DoSocketRegister(inputRegUser.text, inputRegPass.text, inputRegEmail.text));
            if (btnOpenRegister != null) btnOpenRegister.onClick.AddListener(() => SwitchPanel(registerPanel));
            if (btnCloseRegister != null) btnCloseRegister.onClick.AddListener(() => SwitchPanel(loginPanel));
            if (btnQuickLogin != null) btnQuickLogin.onClick.AddListener(OnQuickLoginClick);
            if (btnSwitchAccount != null) btnSwitchAccount.onClick.AddListener(OnSwitchAccountClick);
            BuildRememberToggle();

            InitUI();
        }

        protected override void RegisterHandlers()
        {
            Listen(Cmd.LOGIN, OnLoginResponse);
            Listen(Cmd.REGISTER, OnRegisterResponse);
            Listen(Cmd.CREATE_CHARACTER, OnCreateCharResponse);
        }

        private void InitUI()
        {
            string savedUser = SavedLogin.User;
            if (SavedLogin.HasSaved)
            {
                if (txtQuickLogin != null) txtQuickLogin.text = "Tiếp tục: " + savedUser;
                SwitchPanel(quickLoginPanel);
            }
            else
            {
                ShowLoginForm(savedUser);
            }
        }

        /// <summary>Hiện form đăng nhập, điền sẵn tên (nếu có) và xoá ô mật khẩu.</summary>
        public void ShowLoginForm(string user = "")
        {
            if (inputLoginUser != null && !string.IsNullOrEmpty(user)) inputLoginUser.text = user;
            if (inputLoginPass != null) inputLoginPass.text = "";
            SwitchPanel(loginPanel);
        }

        /// <summary>
        /// Nút "Ghi nhớ đăng nhập" (mặc định BẬT) ngay dưới ô mật khẩu — tạo bằng code, không cần sửa prefab.
        /// Tắt → không lưu tài khoản trên máy (nên tắt khi chơi ở quán net / máy người khác).
        /// </summary>
        private void BuildRememberToggle()
        {
            if (inputLoginPass == null || _rememberBtn != null) return;
            var passRt = (RectTransform)inputLoginPass.transform;
            _rememberBtn = UIKit.Button("RememberLogin", passRt, "", () =>
            {
                SavedLogin.Remember = !SavedLogin.Remember;
                RefreshRememberToggle();
            }, 22);
            var rt = (RectTransform)_rememberBtn.transform;
            // neo vào mép dưới-trái ô mật khẩu, nằm ngay bên dưới
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -2f);
            rt.sizeDelta = new Vector2(Mathf.Max(240f, passRt.rect.width), 36f); // đủ to để bấm trên điện thoại
            var img = _rememberBtn.GetComponent<Image>();
            if (img != null) img.color = new Color(0, 0, 0, 0); // chỉ chữ, nền trong suốt
            RefreshRememberToggle();
        }

        private void RefreshRememberToggle()
        {
            if (_rememberBtn == null) return;
            _rememberBtn.SetLabel(SavedLogin.Remember ? "[X] Ghi nhớ đăng nhập" : "[   ] Ghi nhớ đăng nhập");
            var label = _rememberBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = new Color(0.35f, 0.22f, 0.14f, 1f); // nâu đậm: khung đăng nhập nền sáng
            }
        }

        private void SwitchPanel(GameObject panel)
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (createCharPanel != null) createCharPanel.SetActive(false);
            if (quickLoginPanel != null) quickLoginPanel.SetActive(false);
            if (panel != null) panel.SetActive(true);
        }

        // ==========================================
        // CƠ CHẾ ĐẢM BẢO KẾT NỐI TRƯỚC KHI GỬI
        // ==========================================
        private IEnumerator EnsureConnected(Action onReady)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                PopupAndLoad.Instance.ShowPopup("Đang kết nối lại máy chủ...", null, false);
                NetworkManager.Instance.Connect(GameConfig.SocketHost, GameConfig.SocketPort);

                float timeout = 5f;
                while (timeout > 0 && !NetworkManager.Instance.IsConnected)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                if (!NetworkManager.Instance.IsConnected)
                {
                    PopupAndLoad.Instance.HidePopup();
                    PopupAndLoad.Instance.ShowPopup("Không thể kết nối tới máy chủ!");
                    yield break;
                }
            }
            onReady?.Invoke();
        }

        // ==========================================
        // SEND REQUESTS
        // ==========================================

        /// <summary>Đăng nhập không qua ô nhập (dùng cho AutoTestRunner / công cụ kiểm thử).</summary>
        public void AutoLogin(string user, string pass) => DoSocketLogin(user, pass);

        private void DoSocketLogin(string user, string pass, bool fromSaved = false)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                PopupAndLoad.Instance.ShowPopup("Vui lòng nhập tài khoản và mật khẩu!");
                return;
            }

            StartCoroutine(EnsureConnected(() =>
            {
                _pendingUser = user;
                _pendingPass = pass;
                _pendingFromSaved = fromSaved;
                PopupAndLoad.Instance.ShowPopup("Đang đăng nhập...");

                Data.GameActions.Login(user, pass);
            }));
        }

        private void DoSocketRegister(string user, string pass, string email)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                PopupAndLoad.Instance.ShowPopup("Vui lòng điền đầy đủ thông tin!");
                return;
            }

            StartCoroutine(EnsureConnected(() =>
            {
                PopupAndLoad.Instance.ShowPopup("Đang tạo tài khoản...");
                Data.GameActions.Register(user, pass, email);
            }));
        }

        private void OnQuickLoginClick()
        {
            string u = SavedLogin.User;
            string p = SavedLogin.LoadPassword();
            if (!string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(p)) DoSocketLogin(u, p, true);
            else ShowLoginForm(u); // mật khẩu lưu không đọc được (máy khác / hỏng) → gõ lại
        }

        private void OnSwitchAccountClick()
        {
          //  if (NetworkManager.Instance != null) NetworkManager.Instance.Disconnect();
            SavedLogin.Clear();
            ShowLoginForm();
        }

        // ==========================================
        // NHẬN KẾT QUẢ
        // ==========================================

        /// <summary>LOGIN: short status (0 vào game + EnterWorld · 1 chưa có nhân vật · khác = lỗi, xem AuthMessages)</summary>
        private void OnLoginResponse(byte[] data)
        {
            PopupAndLoad.Instance.HidePopup();
            var r = new MessageReader(data);
            short status = r.ReadShort();
            if (status == 0) { Enter(r); return; }
            if (status == 1) { r.Cleanup(); SwitchPanel(createCharPanel); return; }

            // Mã 12 kèm lời nhắn bảo trì; mã 3, 8, 9, 10 kèm số lần thử còn lại / số giây phải đợi (server cũ không gửi → -1)
            string note = status == 12 && r.Available() >= 2 ? r.ReadUTF() : null;
            int extra = status != 12 && r.Available() >= 4 ? r.ReadInt() : -1;
            r.Cleanup();
            // Mật khẩu đã lưu không còn đúng (đổi ở máy khác) → quên mật khẩu, điền sẵn tên để gõ lại
            if (status == 3 && _pendingFromSaved)
            {
                SavedLogin.ForgetPassword();
                ShowLoginForm(_pendingUser);
            }
            PopupAndLoad.Instance.ShowPopup(AuthMessages.Login(status, extra, note));
        }

        /// <summary>CREATE_CHARACTER: short status (0 = vào game, cùng định dạng LOGIN)</summary>
        private void OnCreateCharResponse(byte[] data)
        {
            PopupAndLoad.Instance.HidePopup();
            var r = new MessageReader(data);
            short status = r.ReadShort();
            if (status == 0) { Enter(r); return; }
            r.Cleanup();
            PopupAndLoad.Instance.ShowPopup(AuthMessages.CreateCharacter(status));
        }

        private void OnRegisterResponse(byte[] data)
        {
            PopupAndLoad.Instance.HidePopup();
            var r = new MessageReader(data);
            short status = r.ReadShort();
            r.Cleanup();
            if (status == 0) PopupAndLoad.Instance.ShowPopup("Đăng ký thành công!", () => SwitchPanel(loginPanel));
            else PopupAndLoad.Instance.ShowPopup(AuthMessages.Register(status));
        }

        private void Enter(MessageReader r)
        {
            SavedLogin.Save(_pendingUser, _pendingPass); // mã hoá AES theo máy; tắt "Ghi nhớ" thì không lưu
            var info = EnterWorld.Read(r);
            r.Cleanup();
            EnterWorld.Run(info, HUDCanvas);
            Destroy(gameObject);
        }
    }
}
