using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// KHẢM NGỌC (GĐ9) — mở ở Thợ Rèn → "Khảm ngọc" (server gửi GEM_OPEN).
    ///   Trái:  trang bị đang nằm trong TÚI (muốn khảm đồ đang mặc thì tháo ra trước) → chọn 1 món → xem các lỗ, [Tháo] ngọc.
    ///   Phải:  ngọc trong túi → [Khảm] vào món đang chọn, [Ghép] 3 viên → 1 viên cấp cao hơn.
    /// Server kiểm mọi luật (số lỗ, phí, ngọc khoá → đồ khoá); cửa sổ chỉ gửi chỉ số ô túi.
    /// </summary>
    public class GemWindow : GameWindow
    {
        [SerializeField] private RectTransform _equips, _gems, _sockets;
        [SerializeField] private TextMeshProUGUI _status, _detail;
        private int _selTpl = -1, _selPos = -1;   // nhớ món đang chọn theo vị trí trong túi (túi đổi thì tìm lại)

        protected override void Configure()
        {
            Title = "Khảm ngọc";
            Size = new Vector2(700, 540);
        }

        protected override void Build()
        {
            var body = CreateBody();

            UIKit.Text("H1", body, "<b><color=#fd5>Trang bị trong túi</color></b>", 16).rectTransform
                .Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(330, 24), new Vector2(0, 1));
            _equips = UIKit.ScrollList("Equips", body, 4);
            ((RectTransform)_equips.parent).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -26), new Vector2(330, 200), new Vector2(0, 1));

            _detail = UIKit.Text("Detail", body, "", 14, TextAlignmentOptions.TopLeft);
            _detail.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -232), new Vector2(330, 90), new Vector2(0, 1));
            _sockets = UIKit.ScrollList("Sockets", body, 4);
            ((RectTransform)_sockets.parent).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -326), new Vector2(330, 120), new Vector2(0, 1));

            UIKit.Text("H2", body, "<b><color=#fd5>Ngọc trong túi</color></b>", 16).rectTransform
                .Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(340, 24), new Vector2(1, 1));
            _gems = UIKit.ScrollList("Gems", body, 4);
            ((RectTransform)_gems.parent).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -26), new Vector2(340, 420), new Vector2(1, 1));

            _status = UIKit.Text("Status", body, "", 14, TextAlignmentOptions.MidlineLeft);
            _status.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 40), new Vector2(0.5f, 0));
        }

        protected override void Bind()
        {
            GameData.OnChanged += k => { if (k == DataKind.Gem || k == DataKind.Inventory || k == DataKind.Templates || k == DataKind.Money) RefreshIfOpen(); };
        }

        /// <summary>Vị trí trong túi của món đang chọn (−1 = chưa chọn / đã mất).</summary>
        private int SelectedIndex()
        {
            if (_selPos >= 0 && _selPos < GameData.Inventory.Count && GameData.Inventory[_selPos].tpl == _selTpl) return _selPos;
            return -1;
        }

        protected override void Refresh()
        {
            _status.text = (string.IsNullOrEmpty(GameData.GemMessage) ? "" : GameData.GemMessage + "\n")
                + $"<color=#9aa>Phí theo cấp ngọc: khảm {GameData.GemSocketCost}·cấp · tháo {GameData.GemRemoveCost}·cấp · ghép {GameData.GemCombineCost}·cấp yên. Yên: {GameData.Me.yen:N0}</color>";

            // ---- trang bị ----
            UIKit.Clear(_equips);
            int sel = SelectedIndex();
            int shown = 0;
            for (int i = 0; i < GameData.Inventory.Count; i++)
            {
                var s = GameData.Inventory[i];
                if (!GameData.Items.TryGetValue(s.tpl, out var t) || !t.IsEquip) continue;
                int idx = i;
                var b = UIKit.Button("E", _equips, $"{GameData.ColoredName(s.tpl, s.level)}  <size=12><color=#e080ff>{s.gems.Count}/{t.Sockets} lỗ</color>"
                    + (s.bonus > 0 ? $" <color=#6fd0ff>+{s.bonus}%</color>" : "") + "</size>", () => { _selPos = idx; _selTpl = s.tpl; Refresh(); }, 14).Height(34);
                b.image.color = idx == sel ? UIKit.ButtonHot : UIKit.SlotColor;
                shown++;
            }
            if (shown == 0) UIKit.Text("None", _equips, "<color=#888>Không có trang bị trong túi (đồ đang mặc phải tháo ra trước).</color>", 14).Height(40);

            // ---- lỗ khảm của món đang chọn ----
            UIKit.Clear(_sockets);
            var cur = sel >= 0 ? GameData.Inventory[sel] : null;
            GameData.Items.TryGetValue(cur?.tpl ?? -1, out var ct);
            _detail.text = cur == null ? "<color=#9aa>Chọn 1 trang bị bên trên.</color>"
                : $"<b>{GameData.ColoredName(cur.tpl, cur.level)}</b>{(cur.locked ? " <color=#aaa>[Khoá]</color>" : "")}\n"
                  + (cur.bonus > 0 ? $"<color=#6fd0ff>Phẩm chất +{cur.bonus}%</color>\n" : "")
                  + $"<color=#e080ff>Lỗ khảm {cur.gems.Count}/{ct?.Sockets ?? 1}</color> <size=12><color=#9aa>(đồ cấp 15 trở lên có 2 lỗ)</color></size>";
            if (cur != null)
            {
                for (int i = 0; i < (ct?.Sockets ?? 1); i++)
                {
                    int pos = i;
                    var row = UIKit.Panel("S", _sockets, new Color(0.15f, 0.12f, 0.2f)).Height(36);
                    bool has = i < cur.gems.Count;
                    UIKit.Text("T", row.transform, has ? InventoryWindow.GemLine(cur.gems[i]) : "<color=#777>(lỗ trống)</color>", 14).rectTransform.Fill(8, 90, 2, 2);
                    if (!has) continue;
                    int tier = GameData.Items.TryGetValue(cur.gems[i], out var gt) ? gt.GemTier : 1;
                    var rm = UIKit.Button("Rm", row.transform, "Tháo", () => ConfirmWindow.Ask(
                        $"Tháo {InventoryWindow.GemLine(cur.gems[pos])} ra? Phí {GameData.GemRemoveCost * tier:N0} yên.", "Tháo",
                        () => GameActions.GemRemove(sel, pos)), 14);
                    ((RectTransform)rm.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(80, 30), new Vector2(1, 0.5f));
                }
            }

            // ---- ngọc trong túi ----
            UIKit.Clear(_gems);
            int gemsShown = 0;
            bool canSocket = cur != null && ct != null && cur.gems.Count < ct.Sockets;
            for (int i = 0; i < GameData.Inventory.Count; i++)
            {
                var s = GameData.Inventory[i];
                if (!GameData.Items.TryGetValue(s.tpl, out var g) || !g.IsGem) continue;
                int idx = i, tpl = s.tpl;
                var row = UIKit.Panel("G", _gems, UIKit.SlotColor).Height(54);
                UIKit.Text("T", row.transform, $"{InventoryWindow.GemLine(s.tpl)} <color=#fd5>x{s.qty}</color>{(s.locked ? " <size=12><color=#aaa>[Khoá]</color></size>" : "")}", 14)
                    .rectTransform.Fill(8, 8, 2, 26);
                var put = UIKit.Button("Put", row.transform, "Khảm", () => GameActions.GemSocket(sel, idx), 13);
                ((RectTransform)put.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 3), new Vector2(90, 24), Vector2.zero);
                put.interactable = canSocket;
                var comb = UIKit.Button("Comb", row.transform, "Ghép 3→1", () => GameActions.GemCombine(tpl), 13);
                ((RectTransform)comb.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(104, 3), new Vector2(100, 24), Vector2.zero);
                comb.interactable = g.GemTier < 3 && GameData.CountItem(tpl) >= 3;
                gemsShown++;
            }
            if (gemsShown == 0) UIKit.Text("None", _gems, "<color=#888>Chưa có ngọc. Ngọc rơi từ quái tinh anh / boss, thưởng boss thế giới, hoặc mua ở \"Cửa hàng ngọc (xu)\" của Thợ Rèn.</color>", 14).Height(60);
        }
    }
}
