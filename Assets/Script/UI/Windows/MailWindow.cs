using System;
using System.Text;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// HÒM THƯ (phím M). Thư hệ thống mang quà (thưởng boss, lôi đài, nạp, đồ tràn túi...) và thư người chơi gửi nhau.
    ///  - Trái: danh sách thư (chưa đọc in đậm, có quà ghi [Quà]). Bấm → đọc (MAIL_READ → MAIL_CONTENT).
    ///  - Phải: nội dung + [Nhận quà] + [Xoá] (thư còn quà chưa nhận thì server không cho xoá).
    ///  - [Soạn thư]: gửi cho người khác, kèm yên và/hoặc 1 ô đồ đang chọn trong Hành trang (đồ khoá không gửi được).
    /// </summary>
    public class MailWindow : GameWindow
    {
        [SerializeField] private RectTransform _list, _view, _compose;
        [SerializeField] private TextMeshProUGUI _content, _attachInfo;
        [SerializeField] private Button _claim, _delete;
        [SerializeField] private TMP_InputField _to, _title, _body, _yen;
        private bool _attach;
        [SerializeField] private Button _btnAttach;
        [SerializeField] private Button _btnNew, _btnReload, _btnSend, _btnBack;

        protected override void Configure()
        {
            Title = "Hòm thư";
            HotKey = Key.M;
            Size = new Vector2(760, 520);
        }

        protected override void Build()
        {
            var body = CreateBody();

            _btnNew = UIKit.Button("New", body, "Soạn thư", null, 15);
            ((RectTransform)_btnNew.transform).Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(130, 34), new Vector2(0, 1));
            _btnReload = UIKit.Button("Reload", body, "Làm mới", null, 15);
            ((RectTransform)_btnReload.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(136, 0), new Vector2(110, 34), new Vector2(0, 1));

            _list = UIKit.ScrollList("List", body, 4);
            ((RectTransform)_list.parent).Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(270, -40), new Vector2(0, 0));
            ((RectTransform)_list.parent).anchoredPosition = Vector2.zero;

            // ---- xem thư ----
            _view = UIKit.Panel("View", body, new Color(0, 0, 0, 0.3f)).rectTransform;
            _view.Fill(280, 0, 0, 0);
            _content = UIKit.Text("Content", _view, "Chọn 1 thư để đọc.", 16, TextAlignmentOptions.TopLeft);
            _content.rectTransform.Fill(10, 10, 8, 56);
            _claim = UIKit.Button("Claim", _view, "Nhận quà", null, 16);
            ((RectTransform)_claim.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 8), new Vector2(140, 40), Vector2.zero);
            _claim.image.color = UIKit.ButtonHot;
            _delete = UIKit.Button("Delete", _view, "Xoá thư", null, 16);
            ((RectTransform)_delete.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 8), new Vector2(120, 40), new Vector2(1, 0));

            // ---- soạn thư ----
            _compose = UIKit.Panel("Compose", body, new Color(0.05f, 0.07f, 0.1f, 1f)).rectTransform;
            _compose.Fill(280, 0, 0, 0);
            _to = Field(_compose, "To", "Gửi cho (tên nhân vật)", 0, 16);
            _title = Field(_compose, "Title", "Tiêu đề", 1, 40);
            _body = UIKit.Input("Body", _compose, "Nội dung", 15, TMP_InputField.ContentType.Standard, 300);
            _body.lineType = TMP_InputField.LineType.MultiLineNewline;
            _body.textComponent.enableWordWrapping = true;
            _body.textComponent.alignment = TextAlignmentOptions.TopLeft;
            ((RectTransform)_body.transform).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(-20, 130), new Vector2(0.5f, 1));
            _yen = Field(_compose, "Yen", "Kèm yên (0)", 4.4f, 9, TMP_InputField.ContentType.IntegerNumber);
            _btnAttach = UIKit.Button("Attach", _compose, "", null, 14);
            ((RectTransform)_btnAttach.transform).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -278), new Vector2(-20, 34), new Vector2(0.5f, 1));
            _attachInfo = UIKit.Text("AttachInfo", _compose, "", 13, TextAlignmentOptions.TopLeft, UIKit.DimText);
            _attachInfo.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -316), new Vector2(-20, 40), new Vector2(0.5f, 1));
            _btnSend = UIKit.Button("Send", _compose, "Gửi thư", null, 16);
            ((RectTransform)_btnSend.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 8), new Vector2(140, 40), Vector2.zero);
            _btnSend.image.color = UIKit.ButtonHot;
            _btnBack = UIKit.Button("Back", _compose, "Huỷ", null, 16);
            ((RectTransform)_btnBack.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 8), new Vector2(110, 40), new Vector2(1, 0));
            _compose.gameObject.SetActive(false);
        }

        protected override void Bind()
        {
            _btnNew.onClick.AddListener(() => Compose(""));
            _btnReload.onClick.AddListener(GameActions.MailList);
            _claim.onClick.AddListener(() => { if (GameData.OpenMail != null) GameActions.MailClaim(GameData.OpenMail.id); });
            _delete.onClick.AddListener(() =>
            {
                var m = GameData.OpenMail;
                if (m != null) { GameActions.MailDelete(m.id); GameData.OpenMail = null; Refresh(); }
            });
            _btnAttach.onClick.AddListener(() => { _attach = !_attach; UpdateAttach(); });
            _btnSend.onClick.AddListener(OnSend);
            _btnBack.onClick.AddListener(() => { _compose.gameObject.SetActive(false); Refresh(); });

            GameData.OnChanged += k => { if (k == DataKind.Mails || k == DataKind.Templates) RefreshIfOpen(); if (k == DataKind.Inventory) UpdateAttach(); };
        }

        private static TMP_InputField Field(RectTransform parent, string name, string hint, float row, int limit,
            TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard)
        {
            var f = UIKit.Input(name, parent, hint, 15, type, limit);
            ((RectTransform)f.transform).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -10 - row * 42), new Vector2(-20, 36), new Vector2(0.5f, 1));
            return f;
        }

        /// <summary>Mở khung soạn thư (có thể điền sẵn người nhận).</summary>
        public void Compose(string to)
        {
            if (!IsOpen) Show();
            _compose.gameObject.SetActive(true);
            _to.text = to ?? "";
            _attach = false;
            UpdateAttach();
        }

        private void UpdateAttach()
        {
            if (_btnAttach == null) return;
            var s = GameData.BagAt(InventoryWindow.SelectedIndex);
            if (s == null) _attach = false;
            _btnAttach.SetLabel($"{(_attach ? "[x]" : "[  ]")} Đính kèm ô đang chọn trong Hành trang");
            _attachInfo.text = s == null ? "Mở Hành trang (I) và chọn 1 ô để đính kèm."
                             : $"Ô đang chọn: {GameData.ColoredName(s.tpl, s.level)}" + (s.qty > 1 ? $" x{s.qty}" : "") + (s.locked ? " <color=#f77>(đồ khoá — không gửi được)</color>" : "");
        }

        private void OnSend()
        {
            string to = _to.text.Trim();
            if (to.Length == 0) { _attachInfo.text = "<color=#f77>Nhập tên người nhận.</color>"; return; }
            int.TryParse(_yen.text, out int yen);
            int idx = -1, qty = 0;
            var s = GameData.BagAt(InventoryWindow.SelectedIndex);
            if (_attach && s != null) { idx = InventoryWindow.SelectedIndex; qty = s.qty; }
            GameActions.MailSend(to, _title.text.Trim(), _body.text.Trim(), Math.Max(0, yen), idx, qty);
            _title.text = ""; _body.text = ""; _yen.text = ""; _attach = false;
            _compose.gameObject.SetActive(false);
            Refresh();
        }

        protected override void Refresh()
        {
            if (GameData.Mails.Count == 0 && !_requested) { _requested = true; GameActions.MailList(); }
            UIKit.Clear(_list);
            foreach (var m in GameData.Mails)
            {
                long id = m.id;
                string date = DateTimeOffset.FromUnixTimeMilliseconds(m.createdMs).ToLocalTime().ToString("dd/MM HH:mm");
                string text = (m.read ? "" : "<b>") + m.title + (m.read ? "" : "</b>") +
                              (m.hasAttach && !m.claimed ? " <color=#fd5>[Quà]</color>" : "") +
                              $"\n<size=12><color=#9aa>{m.from} · {date}</color></size>";
                var b = UIKit.Button("M" + id, _list, text, () => GameActions.MailRead(id), 14).Height(50);
                b.image.color = GameData.OpenMail != null && GameData.OpenMail.id == id ? UIKit.ButtonHot : UIKit.SlotColor;
            }
            if (GameData.Mails.Count == 0) UIKit.Text("Empty", _list, "Hòm thư trống.", 15).Height(30);

            var o = GameData.OpenMail;
            _view.gameObject.SetActive(!_compose.gameObject.activeSelf);
            if (o == null) { _content.text = "Chọn 1 thư để đọc."; _claim.gameObject.SetActive(false); _delete.gameObject.SetActive(false); return; }
            var sb = new StringBuilder($"<size=19><b>{o.title}</b></size>\n<color=#9aa>Từ: {o.from}</color>\n\n{o.content}\n");
            bool hasGift = o.yen > 0 || o.xu > 0 || o.luong > 0 || o.items.Count > 0;
            if (hasGift)
            {
                sb.Append("\n<color=#fd5>Quà đính kèm:</color>");
                if (o.yen > 0) sb.Append($"\n• {o.yen:N0} yên");
                if (o.xu > 0) sb.Append($"\n• {o.xu:N0} xu");
                if (o.luong > 0) sb.Append($"\n• {o.luong:N0} lượng");
                foreach (var it in o.items) sb.Append("\n• ").Append(GameData.ColoredName(it.tpl, it.level)).Append(it.qty > 1 ? $" x{it.qty}" : "");
                if (o.claimed) sb.Append("\n<color=#6f6>(đã nhận)</color>");
            }
            _content.text = sb.ToString();
            _claim.gameObject.SetActive(hasGift && !o.claimed);
            _delete.gameObject.SetActive(true);
        }

        private bool _requested;
        private void OnEnable() { _requested = false; if (LocalPlayerStateReady()) GameActions.MailList(); }
        private static bool LocalPlayerStateReady() => Assets.Script.Player.LocalPlayerState.Id >= 0;
    }
}
