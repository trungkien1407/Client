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

        void Start()
        {
            // UI Listeners
            if (btnLoginSend != null) btnLoginSend.onClick.AddListener(() => DoSocketLogin(inputLoginUser.text, inputLoginPass.text));
            if (btnRegisterSend != null) btnRegisterSend.onClick.AddListener(() => DoSocketRegister(inputRegUser.text, inputRegPass.text, inputRegEmail.text));
            if (btnOpenRegister != null) btnOpenRegister.onClick.AddListener(() => SwitchPanel(registerPanel));
            if (btnCloseRegister != null) btnCloseRegister.onClick.AddListener(() => SwitchPanel(loginPanel));
            if (btnQuickLogin != null) btnQuickLogin.onClick.AddListener(OnQuickLoginClick);
            if (btnSwitchAccount != null) btnSwitchAccount.onClick.AddListener(OnSwitchAccountClick);

            // Network Listeners
            NetworkEventDispatcher.Instance.AddHandler(Cmd.LOGIN, OnSocketLoginResponse);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.REGISTER, OnSocketRegisterResponse);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.CREATE_CHARACTER, OnCreateCharResponse);

            InitUI();
        }

        private void InitUI()
        {
            string savedUser = PlayerPrefs.GetString("SavedUser", "");
            if (!string.IsNullOrEmpty(savedUser))
            {
                if (txtQuickLogin != null) txtQuickLogin.text = "Tiếp tục: " + savedUser;
                SwitchPanel(quickLoginPanel);
            }
            else
            {
                SwitchPanel(loginPanel);
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

        private void DoSocketLogin(string user, string pass)
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
                PopupAndLoad.Instance.ShowPopup("Đang đăng nhập...");

                MessageWriter writer = new MessageWriter();
                writer.WriteUTF(user);
                writer.WriteUTF(pass);
                NetworkManager.Instance.Send(Cmd.LOGIN, writer.ToArray());
                writer.Cleanup();
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
                MessageWriter writer = new MessageWriter();
                writer.WriteUTF(user);
                writer.WriteUTF(pass);
                writer.WriteUTF(email);
                NetworkManager.Instance.Send(Cmd.REGISTER, writer.ToArray());
                writer.Cleanup();
            }));
        }

        private void OnQuickLoginClick()
        {
            string u = PlayerPrefs.GetString("SavedUser", "");
            string p = PlayerPrefs.GetString("SavedPass", "");
            if (!string.IsNullOrEmpty(u)) DoSocketLogin(u, p);
            else OnSwitchAccountClick();
        }

        private void OnSwitchAccountClick()
        {
          //  if (NetworkManager.Instance != null) NetworkManager.Instance.Disconnect();
            PlayerPrefs.DeleteKey("SavedUser");
            PlayerPrefs.DeleteKey("SavedPass");
            PlayerPrefs.Save();
            SwitchPanel(loginPanel);
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
                reader.Cleanup();
                string errorMsg = status switch
                {
                    3 => "Sai tài khoản hoặc mật khẩu",
                    4 => "Tài khoản đang đăng nhập ở nơi khác",
                    _ => "Lỗi máy chủ!"
                };
                PopupAndLoad.Instance.ShowPopup(errorMsg);
            }
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
                PopupAndLoad.Instance.ShowPopup("Lỗi tạo nhân vật!");
            }
        }

        private void HandleLoginSuccess(MessageReader reader)
        {
            PlayerPrefs.SetString("SavedUser", _pendingUser);
            PlayerPrefs.SetString("SavedPass", _pendingPass);
            PlayerPrefs.Save();

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

            string equipmentJson = reader.ReadUTF();
            string skillsJson = reader.ReadUTF();
            string settingsJson = reader.ReadUTF();
            reader.Cleanup();

            PopupAndLoad.Instance.ShowLoading();
            MapManager.Instance.LoadMap(mapId > 0 ? mapId : 1);

            NetworkPlayerManager.Instance.SpawnLocalPlayer(
                id, name, classType, level, exp, yen, xu, luong, hp, mp, maxHp, maxMp,
                moveSpeed, jumpForce, gravity, x, y
            );

            if (HUDCanvas != null) HUDCanvas.SetActive(true);

            UISetup.Instance.SetupAvatar(classType);
            UISetup.Instance.SetupHealth(hp, maxHp, mp, maxMp);

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
            else PopupAndLoad.Instance.ShowPopup("Tài khoản đã tồn tại!");
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