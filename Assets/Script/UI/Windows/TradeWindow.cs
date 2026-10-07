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
        private TextMeshProUGUI _mine, _theirs, _status;
        private TMP_InputField _yen;
        private Button _lock, _confirm;

        protected override void Build()
        {
            Title = "Giao dịch";
            Size = new Vector2(470, 480);
            var body = CreateBody();

            var left = UIKit.Panel("Mine", body, new Color(0, 0, 0, 0.3f)).rectTransform;
            left.Place(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0), Vector2.zero, new Vector2(0, 0));
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
            var setYen = UIKit.Button("SetYen", body, "Đặt yên", () =>
            {
                if (int.TryParse(_yen.text, out int v) && v >= 0) GameActions.TradeSetYen(v);
            }, 15);
            ((RectTransform)setYen.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(156, 100), new Vector2(100, 38), Vector2.zero);

            _status = UIKit.Text("Status", body, "", 14, TextAlignmentOptions.MidlineLeft, UIKit.DimText);
            _status.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 58), new Vector2(0, 36), new Vector2(0, 0));

            _lock = UIKit.Button("Lock", body, "Khoá", GameActions.TradeLock, 17);
            ((RectTransform)_lock.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 8), new Vector2(140, 44), Vector2.zero);
            _confirm = UIKit.Button("Confirm", body, "Xác nhận", GameActions.TradeConfirm, 17);
            ((RectTransform)_confirm.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(150, 8), new Vector2(140, 44), Vector2.zero);
            _confirm.image.color = UIKit.ButtonHot;
            var cancel = UIKit.Button("Cancel", body, "Huỷ", GameActions.TradeCancel, 17);
            ((RectTransform)cancel.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(140, 44), new Vector2(1, 0));

            GameData.OnChanged += k => { if (k == DataKind.Trade || k == DataKind.Templates) RefreshIfOpen(); };
        }

        public void OpenWithBag()
        {
            Show();
            SideBySide(this, Open<InventoryWindow>());
        }

        private static string Side(string who, TradeSide s)
        {
            var sb = new StringBuilder($"<b>{who}</b>  ");
            sb.Append(s.confirmed ? "<color=#6f6>[ĐÃ XÁC NHẬN]</color>" : s.locked ? "<color=#fd5>[ĐÃ KHOÁ]</color>" : "<color=#aaa>[đang chọn]</color>");
            sb.Append($"\n<color=#fd5>{s.yen:N0} yên</color>\n");
            foreach (var it in s.items) sb.Append("• ").Append(GameData.ColoredName(it.tpl, it.level)).Append(it.qty > 1 ? $" x{it.qty}" : "").Append('\n');
            if (s.items.Count == 0) sb.Append("<color=#888>(chưa có đồ)</color>");
            return sb.ToString();
        }

        protected override void Refresh()
        {
            SetTitle($"Giao dịch với {GameData.TradeWithName}");
            var me = GameData.TradeMine; var other = GameData.TradeTheirs;
            _mine.text = Side("Bạn đưa", me);
            _theirs.text = Side(GameData.TradeWithName + " đưa", other);
            _lock.interactable = !me.locked;
            _yen.interactable = !me.locked;
            _confirm.interactable = me.locked && other.locked && !me.confirmed;
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
