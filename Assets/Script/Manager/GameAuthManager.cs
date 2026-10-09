using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.Map;
using Assets.Script.Models;
using Assets.Script.UI;
using Assets.Script.Core;
using Assets.Script.UI.Kit;
using Newtonsoft.Json;

namespace Assets.Script.Manager
{
    public class GameAuthManager : MonoBehaviour
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

            // Network Listeners
            NetworkEventDispatcher.Instance.AddHandler(Cmd.LOGIN, OnSocketLoginResponse);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.REGISTER, OnSocketRegisterResponse);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.CREATE_CHARACTER, OnCreateCharResponse);

            InitUI();
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
        // RECEIVE RESPONSES
        // ==========================================

        private void OnSocketLoginResponse(byte[] data)
        {
            PopupAndLoad.Instance.HidePopup();
            MessageReader reader = new MessageReader(data);
            short status = reader.ReadShort();

            if (status == 0) // Thành công, vào game
            {
                HandleLoginSuccess(reader);
            }
            else if (status == 1) // Chưa có nhân vật
            {
                reader.Cleanup();
                SwitchPanel(createCharPanel);
            }
            else
            {
                // Mã 12 (bảo trì) kèm 1 chuỗi lời nhắn của GM.
                // Mã 3, 8, 9, 10 kèm 1 số int (số lần thử còn lại / số giây phải đợi) — server cũ không gửi thì = -1
                string note = status == 12 && reader.Available() >= 2 ? reader.ReadUTF() : null;
                int extra = status != 12 && reader.Available() >= 4 ? reader.ReadInt() : -1;
                reader.Cleanup();
                string errorMsg = status switch
                {
                    3 => extra == 0 ? "Sai mật khẩu 5 lần. Tài khoản tạm khoá đăng nhập trên máy này 5 phút."
                        : extra > 0 ? $"Sai tài khoản hoặc mật khẩu. Còn {extra} lần thử."
                        : "Sai tài khoản hoặc mật khẩu",
                    4 => "Tài khoản đang đăng nhập ở nơi khác",
                    6 => "Tài khoản đã bị khoá. Liên hệ quản trị viên.",
                    7 => "Máy chủ đã đầy. Vui lòng thử lại sau ít phút.",
                    8 => $"Mạng của bạn đăng nhập quá nhiều lần. Đợi {WaitText(extra)} rồi thử lại.",
                    9 => $"Sai mật khẩu quá 5 lần. Đợi {WaitText(extra)} rồi thử lại.",
                    10 => $"Tài khoản vừa đăng nhập. Đợi {WaitText(extra)} rồi thử lại.",
                    11 => "Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!",
                    12 => string.IsNullOrEmpty(note) ? "Máy chủ đang bảo trì. Vui lòng quay lại sau." : note,
                    _ => "Lỗi máy chủ!"
                };
                // Mật khẩu đã lưu không còn đúng (đổi mật khẩu ở máy khác) → quên mật khẩu, điền sẵn tên để gõ lại
                if (status == 3 && _pendingFromSaved)
                {
                    SavedLogin.ForgetPassword();
                    ShowLoginForm(_pendingUser);
                }
                PopupAndLoad.Instance.ShowPopup(errorMsg);
            }
        }

        /// <summary>Số giây → "45 giây" / "5 phút".</summary>
        private static string WaitText(int seconds)
        {
            if (seconds <= 0) return "ít phút";
            if (seconds < 60) return seconds + " giây";
            return ((seconds + 59) / 60) + " phút";
        }

        private void OnCreateCharResponse(byte[] data)
        {
            PopupAndLoad.Instance.HidePopup();
            MessageReader reader = new MessageReader(data);
            short status = reader.ReadShort();

            if (status == 0) HandleLoginSuccess(reader);
            else
            {
                reader.Cleanup();
                PopupAndLoad.Instance.ShowPopup(status switch
                {
                    1 => "Tên nhân vật phải từ 3 đến 15 ký tự.",
                    2 => "Tên nhân vật đã có người dùng!",
                    3 => "Phiên đăng nhập đã hết, vui lòng đăng nhập lại.",
                    4 => "Hệ phái không hợp lệ.",
                    5 => "Tài khoản đã có nhân vật.",
                    _ => "Lỗi tạo nhân vật!"
                });
            }
        }

        private void HandleLoginSuccess(MessageReader reader)
        {
            SavedLogin.Save(_pendingUser, _pendingPass); // mã hoá AES theo máy; tắt "Ghi nhớ" thì không lưu

            // Đọc dữ liệu binary từ Java Player.writeTo()
            int id = reader.ReadInt();
            string name = reader.ReadUTF();
            short classType = reader.ReadShort();
            int level = reader.ReadInt();
            long exp = reader.ReadLong();
            int yen = reader.ReadInt();
            int xu = reader.ReadInt();
            int luong = reader.ReadInt();
            int hp = reader.ReadInt();
            int mp = reader.ReadInt();
            int maxHp = reader.ReadInt();
            int maxMp = reader.ReadInt();
            float moveSpeed = reader.ReadFloat();
            float jumpForce = reader.ReadFloat();
            float gravity = reader.ReadFloat();
            int mapId = reader.ReadInt();
            int zoneId = reader.ReadInt();
            float x = reader.ReadFloat();
            float y = reader.ReadFloat();
            Assets.Script.Player.LocalPlayerState.ZoneId = zoneId; // nút "Khu N" trên HUD

            string equipmentJson = reader.ReadUTF();
            string skillsJson = reader.ReadUTF();
            string settingsJson = reader.ReadUTF();
            reader.Cleanup();

            PopupAndLoad.Instance.ShowLoading();
            MapManager.Instance.LoadMap(mapId > 0 ? mapId : 1);

            NetworkPlayerManager.Instance.SpawnLocalPlayer(new PlayerData
            {
                id = id, name = name, class_type = classType, level = level, exp = exp, yen = yen, xu = xu, luong = luong,
                hp = hp, mp = mp, maxHp = maxHp, maxMp = maxMp, moveSpeed = moveSpeed, jumpForce = jumpForce, gravity = gravity, x = x, y = y
            });

            if (HUDCanvas != null) HUDCanvas.SetActive(true);

            UISetup.Instance.SetupAvatar(classType);
            // Dọn dữ liệu phiên trước TRƯỚC, rồi mới ghi chỉ số nhân vật vừa vào (ngược lại thì bị xoá mất)
            Assets.Script.Data.GameData.ClearSession();
            Assets.Script.Data.GameData.ClassType = classType;
            Assets.Script.Player.LocalPlayerState.Init(id, name, level, exp, yen, xu, luong, hp, maxHp, mp, maxMp);

            // Parse Skill & Shortcuts
            if (!string.IsNullOrEmpty(skillsJson) && skillsJson != "{}")
            {
                var mySkills = JsonConvert.DeserializeObject<List<PlayerSkillData>>(skillsJson);
                int[] myShortcuts = null;
                if (!string.IsNullOrEmpty(settingsJson) && settingsJson != "{}")
                    myShortcuts = JsonConvert.DeserializeObject<int[]>(settingsJson);

                SkillBarManager.Instance.InitPlayerSkills(mySkills, myShortcuts);
            }

            Destroy(gameObject);
        }

        private void OnSocketRegisterResponse(byte[] data)
        {
            PopupAndLoad.Instance.HidePopup();
            MessageReader reader = new MessageReader(data);
            short status = reader.ReadShort();
            reader.Cleanup();

            if (status == 0) PopupAndLoad.Instance.ShowPopup("Đăng ký thành công!", () => SwitchPanel(loginPanel));
            else PopupAndLoad.Instance.ShowPopup(status switch
            {
                1 => "Tài khoản từ 4 ký tự, mật khẩu từ 6 ký tự.",
                2 => "Tài khoản hoặc email đã được dùng!",
                3 => "Email không hợp lệ.",
                4 => "Mạng của bạn thử quá nhiều lần. Đợi ít phút rồi thử lại.",
                5 => "Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!",
                6 => "Máy chủ đang bảo trì, tạm chưa đăng ký được. Vui lòng quay lại sau.",
                _ => "Lỗi máy chủ!"
            });
        }

        private void OnDestroy()
        {
            if (NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.LOGIN, OnSocketLoginResponse);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.REGISTER, OnSocketRegisterResponse);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CREATE_CHARACTER, OnCreateCharResponse);
            }
        }
    }
}