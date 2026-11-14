using System.Text;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// GIAO DỊCH 2 NGƯỜI (an toàn kiểu NSO, server giữ trạng thái):
    ///   1. Mỗi bên bỏ đồ (Hành trang → chọn ô → [Đưa vào GD]) và nhập số yên.
    ///   2. Cả 2 bấm [Khoá] — sau khi khoá không đổi được nữa (chống lừa đổi đồ phút chót).
    ///   3. Cả 2 bấm [Xác nhận] → server đổi đồ trong 1 transaction (ghi trade_logs). Huỷ bất cứ lúc nào.
    /// Đồ khoá (mua bằng xu/lượng) không đưa vào được.
    /// </summary>
    public class TradeWindow : GameWindow
    {
        [SerializeField] private TextMeshProUGUI _mine, _theirs, _status;
        [SerializeField] private TMP_InputField _yen;
        [SerializeField] private Button _lock, _confirm, _setYen, _cancel;

        protected override void Configure()
        {
            Title = "Giao dịch";
            Size = new Vector2(470, 480);
        }

        /// <summary>
        /// Dựng giao diện (1 lần): nửa trái "Bạn đưa", nửa phải "Người kia đưa"; dưới cùng ô nhập yên + [Đặt yên],
        /// dòng trạng thái và 3 nút Khoá / Xác nhận / Huỷ. Hai khung đồ chừa 150px phía dưới cho phần nút.
        /// </summary>
        protected override void Build()
        {
            var body = CreateBody();

            var left = UIKit.Panel("Mine", body, new Color(0, 0, 0, 0.3f)).rectTransform;
            left.Place(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0), Vector2.zero, new Vector2(0, 0));
            // offsetMin / offsetMax: lề trái-dưới / phải-trên so với khung neo (chừa 150px đáy, 5px khe giữa 2 nửa)
            left.offsetMin = new Vector2(0, 150); left.offsetMax = new Vector2(-5, 0);
            _mine = UIKit.Text("Text", left, "", 15, TextAlignmentOptions.TopLeft);
            _mine.rectTransform.Fill(8, 8, 6, 6);

            var right = UIKit.Panel("Theirs", body, new Color(0, 0, 0, 0.3f)).rectTransform;
            right.Place(new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Vector2(0, 0));
            right.offsetMin = new Vector2(5, 150); right.offsetMax = Vector2.zero;
            _theirs = UIKit.Text("Text", right, "", 15, TextAlignmentOptions.TopLeft);
            _theirs.rectTransform.Fill(8, 8, 6, 6);

            _yen = UIKit.Input("Yen", body, "Số yên đưa", 16, TMP_InputField.ContentType.IntegerNumber, 9);
            ((RectTransform)_yen.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 100), new Vector2(150, 38), Vector2.zero);
            _setYen = UIKit.Button("SetYen", body, "Đặt yên", null, 15);
            ((RectTransform)_setYen.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(156, 100), new Vector2(100, 38), Vector2.zero);

            // Dòng nhắc bước tiếp theo (Refresh() điền theo trạng thái khoá / xác nhận)
            _status = UIKit.Text("Status", body, "", 14, TextAlignmentOptions.MidlineLeft, UIKit.DimText);
            _status.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 58), new Vector2(0, 36), new Vector2(0, 0));

            _lock = UIKit.Button("Lock", body, "Khoá", null, 17);
            ((RectTransform)_lock.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 8), new Vector2(140, 44), Vector2.zero);
            _confirm = UIKit.Button("Confirm", body, "Xác nhận", null, 17);
            ((RectTransform)_confirm.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(150, 8), new Vector2(140, 44), Vector2.zero);
            _confirm.image.color = UIKit.ButtonHot;
            _cancel = UIKit.Button("Cancel", body, "Huỷ", null, 17);
            ((RectTransform)_cancel.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(140, 44), new Vector2(1, 0));
        }

        private RectTransform _mineGrid, _theirsGrid;
        private TextMeshProUGUI _mineHead, _theirsHead;

        /// <summary>
        /// Bố cục mới (2026-11-15, dựng lúc chạy — prefab cũ vẫn dùng được): mỗi bên 1 dòng đầu (tên + nhãn trạng thái + yên) và LƯỚI Ô ĐỒ có icon
        /// như túi đồ; dưới cùng: ô yên + [Đặt], dòng nhắc, 1 NÚT CHÍNH đổi theo bước (Khoá → Xác nhận → chờ) và [Huỷ] đỏ.
        /// Trước: đồ liệt kê bằng chữ, 3 nút Khoá / Xác nhận / Huỷ giống hệt nhau → khó biết bấm gì.
        /// </summary>
        protected override void Bind()
        {
            _setYen.onClick.AddListener(() =>
            {
                if (int.TryParse(_yen.text, out int v) && v >= 0) GameActions.TradeSetYen(v);
            });
            _lock.onClick.AddListener(MainAction);
            _cancel.onClick.AddListener(GameActions.TradeCancel);
            _confirm.gameObject.SetActive(false);   // gộp vào nút chính
            GameData.OnChanged += k => { if (k == DataKind.Trade || k == DataKind.Templates) RefreshIfOpen(); };

            ((RectTransform)transform).sizeDelta = new Vector2(480, 470);   // vừa đặt cạnh Hành trang (780) trên màn 1280
            (_mineHead, _mineGrid) = Side((RectTransform)_mine.transform.parent, _mine, false);
            (_theirsHead, _theirsGrid) = Side((RectTransform)_theirs.transform.parent, _theirs, true);

            var yenRt = (RectTransform)_yen.transform;
            yenRt.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 74), new Vector2(180, 36), Vector2.zero);
            ((RectTransform)_setYen.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(186, 74), new Vector2(92, 36), Vector2.zero);
            _setYen.SetLabel("Đặt yên");
            UIStyle.Chip(_setYen, 14);
            _status.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 44), new Vector2(0, 26), new Vector2(0, 0));
            _status.fontSize = 14;
            ((RectTransform)_lock.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(300, 40), Vector2.zero);
            ((RectTransform)_cancel.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(140, 40), new Vector2(1, 0));
            UIStyle.Chip(_cancel, 16);
            _cancel.image.color = new Color(0.55f, 0.16f, 0.14f, 0.95f);
        }

        /// <summary>Khung 1 bên: dòng đầu (tên, trạng thái, yên) cao 52 px + lưới ô đồ 4 cột phía dưới. Khung chừa 120 px đáy cho phần nút.</summary>
        private static (TextMeshProUGUI head, RectTransform grid) Side(RectTransform panel, TextMeshProUGUI head, bool right)
        {
            panel.offsetMin = new Vector2(right ? 5 : 0, 120);
            panel.offsetMax = new Vector2(right ? 0 : -5, 0);
            UIStyle.HudPanel(panel.GetComponent<Image>());
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), new Vector2(-16, 52), new Vector2(0.5f, 1));
            head.alignment = TextAlignmentOptions.TopLeft;
            head.fontSize = 15;
            var grid = UIKit.Grid("Slots", panel, new Vector2(62, 70), new Vector2(6, 6), 3);
            grid.Fill(8, 8, 64, 8);
            // dòng nhắc khi chưa có đồ: nằm ngang cả khung (không phải 1 ô lưới — trước bị bóp chữ trong ô 62 px)
            var hint = UIKit.Text("Hint", panel, "", 13, TextAlignmentOptions.Top);
            hint.rectTransform.Fill(8, 8, 70, 8);
            hint.raycastTarget = false;
            return (head, grid);
        }

        /// <summary>Nút chính theo bước: chưa khoá → Khoá; 2 bên đã khoá → Xác nhận; còn lại chờ bên kia (nút mờ).</summary>
        private void MainAction()
        {
            var me = GameData.TradeMine; var other = GameData.TradeTheirs;
            if (!me.locked) GameActions.TradeLock();
            else if (other.locked && !me.confirmed) GameActions.TradeConfirm();
        }

        public void OpenWithBag()
        {
            Show();
            SideBySide(this, Open<InventoryWindow>());
        }

        /// <summary>Dòng đầu 1 bên: tên + nhãn trạng thái màu + số yên.</summary>
        private static string Head(string who, TradeSide s)
        {
            string tag = s.confirmed ? "<color=#6f6>● ĐÃ XÁC NHẬN</color>" : s.locked ? "<color=#fd5>● ĐÃ KHOÁ</color>" : "<color=#aab>● đang chọn</color>";
            return $"<b>{who}</b>   <size=13>{tag}</size>\n<color=#fd5>{s.yen:N0} yên</color>";
        }

        /// <summary>Vẽ lưới ô đồ có icon (như túi đồ). Trống → dòng nhắc.</summary>
        private static void Slots(RectTransform grid, TradeSide s, bool mine)
        {
            UIKit.Clear(grid);
            for (int i = 0; i < s.items.Count; i++)
            {
                var it = s.items[i];
                string qty = it.qty > 1 ? $"\n<color=#ffd56b>x{it.qty}</color>" : "";
                var b = UIKit.Button("S" + i, grid, GameData.ColoredName(it.tpl, it.level) + qty, null, 11);
                b.image.color = UIKit.SlotColor;
                b.interactable = false;
                ItemIcon.ForItem(b, it.tpl, ItemIcon.Place.Top, 32);
                ItemGlow.Add(b, it.level);
            }
            var hint = grid.parent.Find("Hint")?.GetComponent<TextMeshProUGUI>();
            if (hint != null) hint.text = s.items.Count > 0 ? "" : mine ? "<color=#99a>Chọn đồ trong Hành trang\nrồi bấm [Đưa vào GD]</color>" : "<color=#99a>(chưa có đồ)</color>";
        }

        protected override void Refresh()
        {
            string who = string.IsNullOrEmpty(GameData.TradeWithName) ? "đối phương" : GameData.TradeWithName;   // chưa có tên (mở thử) → chữ dự phòng
            SetTitle($"Giao dịch với {who}");
            var me = GameData.TradeMine; var other = GameData.TradeTheirs;
            _mine.text = Head("Bạn đưa", me);
            _theirs.text = Head(char.ToUpper(who[0]) + who.Substring(1) + " đưa", other);
            if (_mineGrid != null) Slots(_mineGrid, me, true);
            if (_theirsGrid != null) Slots(_theirsGrid, other, false);
            _yen.interactable = _setYen.interactable = !me.locked;
            // nút chính theo bước — vàng khi có việc để bấm, mờ khi đang chờ bên kia
            bool canConfirm = me.locked && other.locked && !me.confirmed;
            _lock.SetLabel(!me.locked ? "Khoá (chốt đồ của mình)" : canConfirm ? "Xác nhận giao dịch" : me.confirmed ? "Đã xác nhận — chờ bên kia" : "Đã khoá — chờ bên kia");
            _lock.interactable = !me.locked || canConfirm;
            _lock.image.color = _lock.interactable ? UIKit.ButtonHot : new Color(0.45f, 0.45f, 0.5f, 1f);
            _status.text = !me.locked ? "Bỏ đồ / yên rồi bấm Khoá."
                         : !other.locked ? "Chờ bên kia khoá..."
                         : !me.confirmed ? "Kiểm tra kỹ rồi bấm Xác nhận."
                         : "Chờ bên kia xác nhận...";
        }

        private void OnDisable()
        {
            // Đóng cửa sổ khi giao dịch còn mở = huỷ (server cũng huỷ nếu 2 bên đi xa)
            if (GameData.TradeWith > 0) GameActions.TradeCancel();
        }
    }
}
