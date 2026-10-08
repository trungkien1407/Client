using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CHỢ (GĐ9) — mở ở NPC Chủ Chợ (server gửi MARKET_OPEN). 2 thẻ:
    ///   Mua hàng:     lọc theo loại + tên, sắp xếp mới nhất / rẻ nhất, 8 món / trang, nút [Mua].
    ///   Hàng của tôi: món đang treo (nút [Huỷ]) + 5 món bán gần nhất.
    /// Treo bán: chọn đồ trong Hành trang (tự mở cạnh) → [Treo bán] → hộp MarketSellDialog nhập số lượng + giá.
    /// Mọi kiểm tra (đủ tiền, đồ khoá, giới hạn 8 món, ai mua trước) đều ở server — client chỉ gửi yêu cầu.
    /// </summary>
    public class MarketWindow : GameWindow
    {
        private static readonly string[] Cats = { "Tất cả", "Vũ khí", "Trang phục", "Bình", "Nguyên liệu" };
        private int _tab, _cat, _sort, _page;
        private TMP_InputField _kw;
        private RectTransform _list, _filters;
        private TextMeshProUGUI _status, _pageText;
        private Button _tabBuy, _tabMine, _sortBtn, _prev, _next;

        protected override void Build()
        {
            Title = "Chợ";
            Size = new Vector2(480, 560);   // + Hành trang 780 = vừa màn 1280
            var body = CreateBody();

            _tabBuy = UIKit.Button("TabBuy", body, "Mua hàng", () => { _tab = 0; Search(); }, 16);
            ((RectTransform)_tabBuy.transform).Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(150, 34), new Vector2(0, 1));
            _tabMine = UIKit.Button("TabMine", body, "Hàng của tôi", () => { _tab = 1; GameActions.MarketMine(); Refresh(); }, 16);
            ((RectTransform)_tabMine.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(156, 0), new Vector2(150, 34), new Vector2(0, 1));

            // Bộ lọc (thẻ Mua)
            _filters = UIKit.Rect("Filters", body);
            _filters.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(0, 76), new Vector2(0.5f, 1));
            for (int i = 0; i < Cats.Length; i++)
            {
                int c = i;
                var b = UIKit.Button("Cat" + i, _filters, Cats[i], () => { _cat = c; _page = 0; Search(); }, 14);
                ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 92, 0), new Vector2(88, 32), new Vector2(0, 1));
            }
            _kw = UIKit.Input("Kw", _filters, "Tìm theo tên...", 15, TMP_InputField.ContentType.Standard, 30);
            ((RectTransform)_kw.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -38), new Vector2(230, 34), new Vector2(0, 1));
            var find = UIKit.Button("Find", _filters, "Tìm", () => { _page = 0; Search(); }, 15);
            ((RectTransform)find.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(236, -38), new Vector2(70, 34), new Vector2(0, 1));
            _sortBtn = UIKit.Button("Sort", _filters, "Mới nhất", () => { _sort = 1 - _sort; _page = 0; Search(); }, 14);
            ((RectTransform)_sortBtn.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(312, -38), new Vector2(120, 34), new Vector2(0, 1));

            _list = UIKit.ScrollList("List", body, 4);
            ((RectTransform)_list.parent).Fill(0, 0, 122, 72);

            _prev = UIKit.Button("Prev", body, "< Trước", () => { if (_page > 0) { _page--; Search(); } }, 14);
            ((RectTransform)_prev.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 36), new Vector2(100, 30), Vector2.zero);
            _pageText = UIKit.Text("Page", body, "", 14, TextAlignmentOptions.Center, UIKit.DimText);
            _pageText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(200, 30), new Vector2(0.5f, 0));
            _next = UIKit.Button("Next", body, "Sau >", () => { _page++; Search(); }, 14);
            ((RectTransform)_next.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 36), new Vector2(100, 30), new Vector2(1, 0));
            _status = UIKit.Text("Status", body, "", 14, TextAlignmentOptions.MidlineLeft);
            _status.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 32), new Vector2(0.5f, 0));

            GameData.OnChanged += k => { if (k == DataKind.Market || k == DataKind.Templates || k == DataKind.Money) RefreshIfOpen(); };
        }

        /// <summary>MARKET_OPEN: mở chợ + Hành trang cạnh nhau (chọn đồ bên túi để treo bán).</summary>
        public void OpenWithBag()
        {
            _tab = 0; _cat = 0; _sort = 0; _page = 0;
            if (_kw != null) _kw.text = "";
            Show();
            SideBySide(this, Open<InventoryWindow>());
        }

        private void Search()
        {
            GameActions.MarketSearch(_cat, _sort, _page, _kw.text);
            Refresh();
        }

        private static string Left(int secs) => secs >= 3600 ? $"{secs / 3600} giờ" : $"{Mathf.Max(1, secs / 60)} phút";

        protected override void Refresh()
        {
            _tabBuy.image.color = _tab == 0 ? UIKit.ButtonHot : UIKit.ButtonColor;
            _tabMine.image.color = _tab == 1 ? UIKit.ButtonHot : UIKit.ButtonColor;
            _filters.gameObject.SetActive(_tab == 0);
            _prev.gameObject.SetActive(_tab == 0);
            _next.gameObject.SetActive(_tab == 0);
            _pageText.gameObject.SetActive(_tab == 0);
            ((RectTransform)_list.parent).offsetMax = new Vector2(0, _tab == 0 ? -122 : -40);
            for (int i = 0; i < Cats.Length; i++)
                _filters.GetChild(i).GetComponent<Image>().color = i == _cat ? UIKit.ButtonHot : UIKit.ButtonColor;
            _sortBtn.SetLabel(_sort == 0 ? "Mới nhất" : "Rẻ nhất");
            _status.text = string.IsNullOrEmpty(GameData.MarketMessage)
                ? $"<color=#9aa>Phí treo {GameData.MarketFeePermil / 10f:0.#}% (tối thiểu 100 yên) · thuế khi bán {GameData.MarketTaxPercent}% · tối đa {GameData.MarketMax} món · {GameData.MarketHours} giờ</color>"
                : GameData.MarketMessage;

            UIKit.Clear(_list);
            if (_tab == 0) DrawBuy(); else DrawMine();
        }

        private void DrawBuy()
        {
            int pages = Mathf.Max(1, (GameData.MarketTotal + 7) / 8);
            _pageText.text = $"Trang {GameData.MarketPage + 1}/{pages}  ({GameData.MarketTotal} món)";
            _prev.interactable = GameData.MarketPage > 0;
            _next.interactable = GameData.MarketPage + 1 < pages;
            if (GameData.MarketRows.Count == 0) { UIKit.Text("Empty", _list, "<color=#888>Không có món nào khớp.</color>", 15).Height(30); return; }
            foreach (var m in GameData.MarketRows)
            {
                long id = m.id;
                bool afford = GameData.Me.yen >= m.price;
                var row = UIKit.Panel("R", _list, UIKit.SlotColor).Height(64);
                string qty = m.slot.qty > 1 ? $" <color=#fd5>x{m.slot.qty}</color>" : "";
                string each = m.slot.qty > 1 ? $" <color=#9aa>({m.price / m.slot.qty:N0}/cái)</color>" : "";
                // 3 dòng: tên · giá · người bán + thời gian (cửa sổ hẹp để đặt cạnh Hành trang)
                UIKit.Text("T", row.transform, $"{GameData.ColoredName(m.slot.tpl, m.slot.level)}{qty}\n" +
                    $"<color={(afford ? "#fd5" : "#f77")}>{m.price:N0} yên</color><size=13>{each}</size>\n" +
                    $"<size=12><color=#9aa>{m.seller} · còn {Left(m.secondsLeft)}</color></size>", 14, TextAlignmentOptions.TopLeft)
                    .rectTransform.Fill(8, 92, 3, 2);
                var buy = UIKit.Button("Buy", row.transform, "Mua", () => ConfirmWindow.Ask(
                    $"Mua {GameData.ColoredName(m.slot.tpl, m.slot.level)} x{m.slot.qty} giá <color=#fd5>{m.price:N0} yên</color>?",
                    "Mua", () => GameActions.MarketBuy(id)), 15);
                ((RectTransform)buy.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(80, 36), new Vector2(1, 0.5f));
                buy.interactable = afford;
            }
        }

        private void DrawMine()
        {
            int active = 0;
            foreach (var m in GameData.MarketMine) if (m.status == 0) active++;
            UIKit.Text("H", _list, $"<b><color=#fd5>Đang treo {active}/{GameData.MarketMax}</color></b>  <size=13><color=#9aa>Treo thêm: chọn đồ trong Hành trang → [Treo bán]</color></size>", 15).Height(28);
            foreach (var m in GameData.MarketMine)
            {
                long id = m.id;
                var row = UIKit.Panel("M", _list, m.status == 0 ? UIKit.SlotColor : new Color(0.15f, 0.25f, 0.15f)).Height(46);
                string state = m.status == 0 ? $"còn {Left(m.secondsLeft)}" : "<color=#7f7>ĐÃ BÁN — tiền đã gửi vào hòm thư</color>";
                UIKit.Text("T", row.transform, $"{GameData.ColoredName(m.slot.tpl, m.slot.level)} x{m.slot.qty} — <color=#fd5>{m.price:N0} yên</color>\n<size=13><color=#9aa>{state}</color></size>", 15)
                    .rectTransform.Fill(8, 100, 2, 2);
                if (m.status != 0) continue;
                var cancel = UIKit.Button("Cancel", row.transform, "Huỷ", () => GameActions.MarketCancel(id), 15);
                ((RectTransform)cancel.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(80, 34), new Vector2(1, 0.5f));
            }
            if (GameData.MarketMine.Count == 0) UIKit.Text("E", _list, "<color=#888>Bạn chưa treo món nào.</color>", 15).Height(30);
        }

        private void OnDisable()
        {
            // Đóng chợ → cửa sổ Hành trang bỏ nút [Treo bán]
            var inv = transform.parent != null ? transform.parent.GetComponentInChildren<InventoryWindow>(true) : null;
            if (inv != null) inv.RefreshIfOpen();
        }
    }
}
