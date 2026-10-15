using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.UI.Kit;
using Assets.Script.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// ĐỔI MẬT KHẨU (Cài đặt → Đổi mật khẩu): mật khẩu cũ + mới + nhập lại → gói CHANGE_PASSWORD.
    /// Kết quả về AccountNetwork.OnChangePassword → OnResult. Dùng sau khi GM đặt lại mật khẩu tạm (quên mật khẩu).
    /// </summary>
    public class ChangePasswordWindow : GameWindow
    {
        [SerializeField] private TMP_InputField _old, _new, _again;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _ok, _cancel;
        private string _pending;   // mật khẩu mới đang chờ server trả lời (đổi xong thì cập nhật "ghi nhớ đăng nhập")
        /// <summary>Mã server trả lần gần nhất (-99 = chưa có) — AutoTestRunner đọc.</summary>
        public static int LastStatus = -99;

        protected override void Configure()
        {
            Title = "Đổi mật khẩu";
            Size = new Vector2(440, 330);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _old = Row(body, 0, "Mật khẩu cũ", "Old");
            _new = Row(body, 1, "Mật khẩu mới", "New");
            _again = Row(body, 2, "Nhập lại", "Again");
            _status = UIKit.Text("Status", body, "", 14, TextAlignmentOptions.TopLeft, UIKit.DimText);
            _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -138), new Vector2(0, 48), new Vector2(0.5f, 1));
            _ok = UIKit.Button("Ok", body, "Đổi", null, 16);
            ((RectTransform)_ok.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(140, 42), new Vector2(1, 0));
            _ok.image.color = UIKit.ButtonHot;
            _cancel = UIKit.Button("Cancel", body, "Huỷ", null, 16);
            ((RectTransform)_cancel.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(120, 42), Vector2.zero);
        }

        private static TMP_InputField Row(RectTransform body, int row, string label, string name)
        {
            float y = -row * 44;
            UIKit.Text("L" + name, body, label, 15).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(130, 36), new Vector2(0, 1));
            var input = UIKit.Input(name, body, "", 16, TMP_InputField.ContentType.Password, 64);
            ((RectTransform)input.transform).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(136, y), new Vector2(-136, 36), new Vector2(0, 1));
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
            _old.text = _new.text = _again.text = "";
            _pending = null;
            _ok.interactable = true;
            _status.text = "Mật khẩu mới 6–64 ký tự. Quên mật khẩu: nhờ quản trị viên đặt lại, rồi đổi tại đây.";
            UIRoot.Instance.BringToFront(this);
        }

        private void Submit()
        {
            string n = _new.text;
            if (n.Length < 6 || n.Length > 64) { _status.text = "<color=#f77>Mật khẩu mới phải 6–64 ký tự.</color>"; return; }
            if (n != _again.text) { _status.text = "<color=#f77>Nhập lại mật khẩu mới không khớp.</color>"; return; }
            if (n == _old.text) { _status.text = "<color=#f77>Mật khẩu mới phải khác mật khẩu cũ.</color>"; return; }
            _pending = n;
            _ok.interactable = false;
            _status.text = "Đang đổi...";
            GameActions.ChangePassword(_old.text, n);
        }

        /// <summary>Server trả CHANGE_PASSWORD (AccountNetwork gọi).</summary>
        public static void OnResult(short status, int extra)
        {
            LastStatus = status;
            var w = Get<ChangePasswordWindow>();
            w._ok.interactable = true;
            if (status == 0)
            {
                // đang "ghi nhớ đăng nhập" → lưu mật khẩu mới, lần sau tự đăng nhập vẫn được
                if (w._pending != null && SavedLogin.HasSaved) SavedLogin.Save(SavedLogin.User, w._pending);
                w._pending = null;
                w.Hide();
                GameHud.Banner("Đã đổi mật khẩu.");
                Combat.ChatBox.AddSystem("Đã đổi mật khẩu. Lần đăng nhập sau dùng mật khẩu mới.");
                return;
            }
            w._pending = null;
            w._status.text = "<color=#f77>" + AuthMessages.ChangePassword(status, extra) + "</color>";
        }
    }
}
