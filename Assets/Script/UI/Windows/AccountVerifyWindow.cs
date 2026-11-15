using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// XÁC THỰC TÀI KHOẢN KHÁCH (Cài đặt → Xác thực tài khoản, 2026-11-15).
    /// Người chơi khách đặt tên đăng nhập + mật khẩu + email thật cho CHÍNH tài khoản đang chơi (server đổi tên / mật khẩu trên
    /// cùng 1 dòng accounts → giữ nguyên nhân vật, đồ, tiến độ). Gói ACCOUNT_VERIFY; kết quả về AccountNetwork → OnResult.
    /// Nút chỉ hiện khi server báo đủ điều kiện (xong nhiệm vụ đầu hoặc cấp 3) — server vẫn kiểm lại (chống sửa client).
    /// </summary>
    public class AccountVerifyWindow : GameWindow
    {
        [SerializeField] private TMP_InputField _user, _pass, _again, _email;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _ok, _cancel;
        private string _pendingPass;
        /// <summary>Mã server trả lần gần nhất (-99 = chưa có) — AutoTestRunner đọc.</summary>
        public static int LastStatus = -99;

        protected override void Configure()
        {
            Title = "Xác thực tài khoản";
            Size = new Vector2(460, 400);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _user = Row(body, 0, "Tài khoản", "User", TMP_InputField.ContentType.Standard, 32);   // Alphanumeric chặn mất dấu _
            _pass = Row(body, 1, "Mật khẩu", "Pass", TMP_InputField.ContentType.Password, 64);
            _again = Row(body, 2, "Nhập lại", "Again", TMP_InputField.ContentType.Password, 64);
            _email = Row(body, 3, "Email", "Email", TMP_InputField.ContentType.EmailAddress, 100);
            _status = UIKit.Text("Status", body, "", 14, TextAlignmentOptions.TopLeft, UIKit.DimText);
            _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -184), new Vector2(0, 70), new Vector2(0.5f, 1));
            _ok = UIKit.Button("Ok", body, "Xác thực", null, 16);
            ((RectTransform)_ok.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(150, 42), new Vector2(1, 0));
            _ok.image.color = UIKit.ButtonHot;
            _cancel = UIKit.Button("Cancel", body, "Để sau", null, 16);
            ((RectTransform)_cancel.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(120, 42), Vector2.zero);
        }

        private static TMP_InputField Row(RectTransform body, int row, string label, string name, TMP_InputField.ContentType type, int limit)
        {
            float y = -row * 44;
            UIKit.Text("L" + name, body, label, 15).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(110, 36), new Vector2(0, 1));
            var input = UIKit.Input(name, body, "", 16, type, limit);
            ((RectTransform)input.transform).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(116, y), new Vector2(-116, 36), new Vector2(0, 1));
            return input;
        }

        protected override void Bind()
        {
            _ok.onClick.AddListener(Submit);
            _cancel.onClick.AddListener(Hide);
        }

        public void Open()
        {
            Show();
            _user.text = _pass.text = _again.text = _email.text = "";
            _pendingPass = null;
            _ok.interactable = true;
            _status.text = "Đặt tên đăng nhập + mật khẩu riêng cho tài khoản đang chơi. Nhân vật, đồ và tiến độ giữ nguyên; " +
                           "sau đó đăng nhập được trên máy khác.";
            UIRoot.Instance.BringToFront(this);
        }

        /// <summary>Kiểm ở máy (cùng luật đăng ký + cấm tên "khach_") rồi gửi ACCOUNT_VERIFY.</summary>
        private void Submit()
        {
            if (!GameData.IsGuest) { _status.text = "<color=#f77>" + AuthMessages.Verify(5) + "</color>"; return; }
            string user = _user.text.Trim(), email = _email.text.Trim();
            string err = AuthMessages.CheckRegisterForm(user, _pass.text, _again.text, email);
            if (err != null) { _status.text = "<color=#f77>" + err + "</color>"; return; }
            _pendingPass = _pass.text;
            _ok.interactable = false;
            _status.text = "Đang xác thực...";
            GameActions.AccountVerify(user, _pass.text, email);
        }

        /// <summary>Server trả ACCOUNT_VERIFY (AccountNetwork gọi).</summary>
        public static void OnResult(short status, string newUser, int extra)
        {
            LastStatus = status;
            var w = Get<AccountVerifyWindow>();
            w._ok.interactable = true;
            if (status == 0)
            {
                // Bỏ thông tin đăng nhập khách trên máy, lưu tài khoản mới cho nút "Tiếp tục" (nếu đang bật ghi nhớ)
                SavedLogin.ClearGuest();
                if (!string.IsNullOrEmpty(newUser) && w._pendingPass != null) SavedLogin.Save(newUser, w._pendingPass);
                GameData.IsGuest = false;
                GameData.CanVerify = false;
                w._pendingPass = null;
                w.Hide();
                GameHud.Banner("Đã xác thực tài khoản.");
                NoticeMascot.Show($"Xác thực xong! Từ nay đăng nhập bằng tài khoản <b>{newUser}</b> và mật khẩu vừa đặt.");
                return;
            }
            w._pendingPass = null;
            w._status.text = "<color=#f77>" + AuthMessages.Verify(status, extra) + "</color>";
        }
    }
}
