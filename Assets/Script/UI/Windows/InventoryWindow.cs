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
    /// TÚI ĐỒ + TRANG BỊ (phím I).
    ///  - Trái: 9 ô trang bị (Vũ khí, Áo, Ngọc bội, Quần, Găng, Giày, Nhẫn, Dây chuyền, Phù). Bấm → Tháo.
    ///  - Phải: các ô túi THEO ĐÚNG THỨ TỰ server (mỗi ô 1 chỉ số bagIndex). Món +N tô màu theo cấp, đồ khoá có chữ [Khoá].
    ///  - Nút theo ngữ cảnh: Dùng / Mặc / Bán (đang mở shop) / Cất (đang mở rương) / Đưa vào GD (đang giao dịch).
    /// Mọi thao tác chỉ GỬI yêu cầu kèm bagIndex; server trả INVENTORY/EQUIPMENT mới → cửa sổ tự vẽ lại.
    ///
    /// [CẦN ĐIỀN khi có icon] hiện mỗi ô chỉ ghi tên vật phẩm. Có atlas icon thì gán Image theo ItemTpl.iconId.
    /// </summary>
    public class InventoryWindow : GameWindow
    {
        private RectTransform _equipGrid, _bagGrid;
        private TextMeshProUGUI _detail, _bagTitle, _money;
        private Button _btnUse, _btnEquip, _btnSell, _btnPut, _btnTrade, _btnMarket;
        private int _selectedEquipSlot = -1;

        /// <summary>Ô túi đang chọn (bagIndex) — cửa sổ Thư dùng để đính kèm.</summary>
        public static int SelectedIndex = -1;

        /// <summary>NPC shop đang mở (-1 = không) — ShopWindow đặt, để hiện nút Bán.</summary>
        public static int OpenShopNpc = -1;

        protected override void Build()
        {
            Title = "Hành trang";
            HotKey = Key.I;
            Size = new Vector2(780, 540);
            var body = CreateBody();

            var eqTitle = UIKit.Text("EqTitle", body, "Trang bị", 18, TextAlignmentOptions.Center, UIKit.ButtonHot);
            eqTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 26), new Vector2(0, 1));
            _equipGrid = UIKit.Grid("Equip", body, new Vector2(94, 64), new Vector2(6, 6), 3);
            _equipGrid.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -30), new Vector2(300, 210), new Vector2(0, 1));

            _bagTitle = UIKit.Text("BagTitle", body, "Túi", 18, TextAlignmentOptions.Center, UIKit.ButtonHot);
            _bagTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(110, 0), new Vector2(-420, 26), new Vector2(0.5f, 1));
            var sort = UIKit.Button("Sort", body, "Sắp xếp", GameActions.SortBag, 14);
            ((RectTransform)sort.transform).Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(90, 26), new Vector2(1, 1));

            var bagScroll = UIKit.ScrollList("BagScroll", body);
            var scrollRt = (RectTransform)bagScroll.parent;
            scrollRt.Fill(310, 0, 30, 30);
            // thay VerticalLayout của content bằng lưới
            Object.DestroyImmediate(bagScroll.GetComponent<VerticalLayoutGroup>());
            var g = bagScroll.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(82, 66);
            g.spacing = new Vector2(6, 6);
            g.padding = new RectOffset(6, 6, 6, 6);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            _bagGrid = bagScroll;

            _money = UIKit.Text("Money", body, "", 15, TextAlignmentOptions.MidlineRight, new Color(1f, 0.85f, 0.3f));
            _money.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(160, 0), new Vector2(-320, 26), new Vector2(0.5f, 0));

            // Khung chi tiết + nút thao tác (dưới cột trang bị)
            var detailBg = UIKit.Panel("Detail", body, new Color(0, 0, 0, 0.3f));
            detailBg.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(300, 236), new Vector2(0, 0));
            _detail = UIKit.Text("Text", detailBg.transform, "Chọn 1 vật phẩm", 15, TextAlignmentOptions.TopLeft);
            _detail.rectTransform.Fill(8, 8, 6, 84);
            _btnUse = Btn(detailBg.transform, "Use", "Dùng", OnUse, 8, 44);
            _btnEquip = Btn(detailBg.transform, "Equip", "Mặc", OnEquip, 104, 44);
            _btnSell = Btn(detailBg.transform, "Sell", "Bán", OnSell, 200, 44);
            _btnPut = Btn(detailBg.transform, "Put", "Cất rương", OnPut, 8, 6);
            _btnTrade = Btn(detailBg.transform, "Trade", "Đưa vào GD", OnTrade, 104, 6);
            _btnMarket = Btn(detailBg.transform, "Market", "Treo bán", OnMarket, 200, 6);   // GĐ9: đang mở Chợ

            GameData.OnChanged += k =>
            {
                if (k == DataKind.Inventory || k == DataKind.Equipment || k == DataKind.Templates || k == DataKind.Money
                    || k == DataKind.Character || k == DataKind.Storage || k == DataKind.Trade || k == DataKind.Market) RefreshIfOpen();
            };
        }

        private static Button Btn(Transform parent, string name, string label, System.Action onClick, float x, float y)
        {
            var b = UIKit.Button(name, parent, label, onClick, 15);
            ((RectTransform)b.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, y), new Vector2(90, 34), Vector2.zero);
            return b;
        }

        protected override void Refresh()
        {
            // ---- trang bị ----
            UIKit.Clear(_equipGrid);
            for (int slot = 1; slot <= GameData.EquipSlotCount; slot++)
            {
                int s = slot;
                GameData.Equipment.TryGetValue(slot, out int tpl);
                GameData.EquipLevel.TryGetValue(slot, out int lv);
                string label = tpl > 0 ? $"<size=12><color=#9aa>{GameData.SlotNames[slot]}</color></size>\n{GameData.ColoredName(tpl, lv)}"
                                       : $"<color=#666>{GameData.SlotNames[slot]}</color>";
                var b = UIKit.Button("Slot" + slot, _equipGrid, label, () => SelectEquip(s), 13);
                b.image.color = slot == _selectedEquipSlot ? UIKit.ButtonHot : tpl > 0 ? UIKit.SlotColor : new Color(0.1f, 0.12f, 0.15f, 1f);
            }

            // ---- túi ----
            UIKit.Clear(_bagGrid);
            for (int i = 0; i < GameData.Inventory.Count; i++)
            {
                int idx = i;
                var s = GameData.Inventory[i];
                string qty = s.qty > 1 ? $"\n<color=#fd5>x{s.qty}</color>" : "";
                string lockMark = s.locked ? "<size=11><color=#aaa>[Khoá] </color></size>" : "";
                var b = UIKit.Button("Item", _bagGrid, lockMark + GameData.ColoredName(s.tpl, s.level) + qty, () => SelectBag(idx), 13);
                b.image.color = idx == SelectedIndex ? UIKit.ButtonHot : UIKit.SlotColor;
            }
            if (GameData.Inventory.Count == 0) UIKit.Text("Empty", _bagGrid, "(túi trống)", 14);
            _bagTitle.text = $"Túi ({GameData.Inventory.Count}/{GameData.BagCapacity})";
            _money.text = $"Yên {GameData.Me.yen:N0}   <color=#7cf>Xu {GameData.Me.xu:N0}</color>   <color=#f8c>Lượng {GameData.Me.luong:N0}</color>";

            UpdateDetail();
        }

        private void SelectBag(int index) { SelectedIndex = index; _selectedEquipSlot = -1; Refresh(); }
        private void SelectEquip(int slot) { _selectedEquipSlot = slot; SelectedIndex = -1; Refresh(); }

        private void UpdateDetail()
        {
            _btnUse.gameObject.SetActive(false);
            _btnEquip.gameObject.SetActive(false);
            _btnSell.gameObject.SetActive(false);
            _btnPut.gameObject.SetActive(false);
            _btnTrade.gameObject.SetActive(false);
            _btnMarket.gameObject.SetActive(false);

            if (_selectedEquipSlot > 0)
            {
                if (!GameData.Equipment.TryGetValue(_selectedEquipSlot, out int eq)) { _detail.text = "Ô trống"; return; }
                GameData.EquipLevel.TryGetValue(_selectedEquipSlot, out int lv);
                _detail.text = Describe(eq, lv);
                _btnEquip.gameObject.SetActive(true);
                _btnEquip.SetLabel("Tháo");
                return;
            }
            var s = GameData.BagAt(SelectedIndex);
            if (s == null) { _detail.text = "Chọn 1 vật phẩm"; return; }

            _detail.text = Describe(s.tpl, s.level, s.locked) + (s.qty > 1 ? $"\nSố lượng: {s.qty}" : "");
            if (GameData.Items.TryGetValue(s.tpl, out var t))
            {
                _btnUse.gameObject.SetActive(t.IsUsable);
                if (t.IsEquip) { _btnEquip.gameObject.SetActive(true); _btnEquip.SetLabel("Mặc"); }
                _btnSell.gameObject.SetActive(OpenShopNpc >= 0 && t.price > 0);
                _btnPut.gameObject.SetActive(GameWindow.Get<StorageWindow>().IsOpen);
                _btnTrade.gameObject.SetActive(GameData.TradeWith > 0 && !s.locked && t.tradeable);
                _btnMarket.gameObject.SetActive(GameWindow.Get<MarketWindow>().IsOpen && !s.locked && t.tradeable);
            }
        }

        /// <summary>Mô tả vật phẩm: tên (+N), yêu cầu, chỉ số (đã tính % cường hoá), giá.</summary>
        public static string Describe(int tplId, int level = 0, bool locked = false)
        {
            if (!GameData.Items.TryGetValue(tplId, out var t)) return $"Vật phẩm {tplId}";
            var sb = new StringBuilder($"<b>{GameData.ColoredName(tplId, level)}</b>");
            if (t.IsEquip) sb.Append($"  <color=#9aa>[{GameData.SlotNames[Mathf.Clamp(t.slot, 0, 9)]}]</color>");
            sb.Append('\n');
            if (locked) sb.Append("<color=#aaa>[Khoá] Đồ khoá: không giao dịch/gửi thư được, bán NPC 0 yên</color>\n");
            if (!string.IsNullOrEmpty(t.description)) sb.Append($"<color=#bbb>{t.description}</color>\n");
            if (t.levelRequire > 1)
            {
                bool ok = GameData.Me.level >= t.levelRequire;
                sb.Append($"<color={(ok ? "#7f7" : "#f77")}>Yêu cầu cấp {t.levelRequire}</color>\n");
            }
            int pct = 0;
            var up = GameData.Upgrade;
            if (level > 0 && up != null && level < up.statPercent.Length) pct = up.statPercent[level];
            string Plus(int v) => pct > 0 ? $"{v + v * pct / 100} <color=#7f7>(+{pct}%)</color>" : v.ToString();
            if (t.bonusDamage > 0) sb.Append($"+{Plus(t.bonusDamage)} sát thương\n");
            if (t.bonusHp > 0) sb.Append($"+{Plus(t.bonusHp)} HP tối đa\n");
            if (t.bonusMp > 0) sb.Append($"+{Plus(t.bonusMp)} MP tối đa\n");
            if (t.hpRestore > 0) sb.Append($"Hồi {t.hpRestore} HP\n");
            if (t.mpRestore > 0) sb.Append($"Hồi {t.mpRestore} MP\n");
            if (t.price > 0 && !locked) sb.Append($"<color=#fd5>Giá bán lại: {t.price / 2} yên</color>");
            return sb.ToString();
        }

        private void OnUse() { var s = GameData.BagAt(SelectedIndex); if (s != null) GameActions.UseItem(s.tpl); }

        private void OnEquip()
        {
            if (_selectedEquipSlot > 0) { GameActions.Unequip(_selectedEquipSlot); _selectedEquipSlot = -1; }
            else if (GameData.BagAt(SelectedIndex) != null) { GameActions.Equip(SelectedIndex); SelectedIndex = -1; }
        }

        private void OnSell() { if (GameData.BagAt(SelectedIndex) != null) GameActions.Sell(SelectedIndex, 1); }

        private void OnPut()
        {
            var s = GameData.BagAt(SelectedIndex);
            if (s != null) { GameActions.StoragePut(SelectedIndex, s.qty); SelectedIndex = -1; }
        }

        private void OnMarket()
        {
            var s = GameData.BagAt(SelectedIndex);
            if (s != null) GameWindow.Get<MarketSellDialog>().Ask(SelectedIndex, s);
        }

        private void OnTrade()
        {
            var s = GameData.BagAt(SelectedIndex);
            if (s != null) { GameActions.TradeAddItem(SelectedIndex, s.qty); SelectedIndex = -1; }
        }
    }
}
