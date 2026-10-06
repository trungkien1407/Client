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
    ///  - Phải: các ô túi. Bấm 1 ô → xem chi tiết + nút Dùng / Mặc / Bán (khi đang mở shop).
    /// Mọi thao tác chỉ GỬI yêu cầu; server trả INVENTORY/EQUIPMENT mới → cửa sổ tự vẽ lại.
    ///
    /// [CẦN ĐIỀN khi có icon] hiện mỗi ô chỉ ghi tên vật phẩm. Có atlas icon thì gán Image theo ItemTpl.iconId.
    /// </summary>
    public class InventoryWindow : GameWindow
    {
        private RectTransform _equipGrid, _bagGrid;
        private TextMeshProUGUI _detail;
        private Button _btnUse, _btnEquip, _btnSell;
        private int _selectedTemplate = -1;
        private int _selectedEquipSlot = -1;

        /// <summary>NPC shop đang mở (-1 = không) — ShopWindow đặt, để hiện nút Bán.</summary>
        public static int OpenShopNpc = -1;

        protected override void Build()
        {
            Title = "Hành trang";
            HotKey = Key.I;
            Size = new Vector2(820, 520);
            var body = CreateBody();

            var eqTitle = UIKit.Text("EqTitle", body, "Trang bị", 18, TextAlignmentOptions.Center, UIKit.ButtonHot);
            eqTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 26), new Vector2(0, 1));
            _equipGrid = UIKit.Grid("Equip", body, new Vector2(94, 70), new Vector2(6, 6), 3);
            _equipGrid.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -30), new Vector2(300, 240), new Vector2(0, 1));

            var bagTitle = UIKit.Text("BagTitle", body, "Túi", 18, TextAlignmentOptions.Center, UIKit.ButtonHot);
            bagTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(160, 0), new Vector2(-320, 26), new Vector2(0.5f, 1));
            var bagScroll = UIKit.ScrollList("BagScroll", body);
            var scrollRt = (RectTransform)bagScroll.parent;
            scrollRt.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(160, 20), new Vector2(-320, -70));
            // thay VerticalLayout của content bằng lưới
            Object.DestroyImmediate(bagScroll.GetComponent<VerticalLayoutGroup>());
            var g = bagScroll.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(92, 70);
            g.spacing = new Vector2(6, 6);
            g.padding = new RectOffset(6, 6, 6, 6);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            _bagGrid = bagScroll;

            // Khung chi tiết + nút thao tác (dưới cột trang bị)
            var detailBg = UIKit.Panel("Detail", body, new Color(0, 0, 0, 0.3f));
            detailBg.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(300, 190), new Vector2(0, 0));
            _detail = UIKit.Text("Text", detailBg.transform, "Chọn 1 vật phẩm", 15, TextAlignmentOptions.TopLeft);
            _detail.rectTransform.Fill(8, 8, 6, 46);
            _btnUse = UIKit.Button("Use", detailBg.transform, "Dùng", OnUse);
            ((RectTransform)_btnUse.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 6), new Vector2(90, 34), Vector2.zero);
            _btnEquip = UIKit.Button("Equip", detailBg.transform, "Mặc", OnEquip);
            ((RectTransform)_btnEquip.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(104, 6), new Vector2(90, 34), Vector2.zero);
            _btnSell = UIKit.Button("Sell", detailBg.transform, "Bán", OnSell);
            ((RectTransform)_btnSell.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 6), new Vector2(90, 34), Vector2.zero);

            GameData.OnChanged += k =>
            {
                if (k == DataKind.Inventory || k == DataKind.Equipment || k == DataKind.Templates) RefreshIfOpen();
            };
        }

        protected override void Refresh()
        {
            // ---- trang bị ----
            UIKit.Clear(_equipGrid);
            for (int slot = 1; slot <= GameData.EquipSlotCount; slot++)
            {
                int s = slot;
                GameData.Equipment.TryGetValue(slot, out int tpl);
                string label = tpl > 0 ? $"<size=12><color=#9aa>{GameData.SlotNames[slot]}</color></size>\n{GameData.ItemName(tpl)}"
                                       : $"<color=#666>{GameData.SlotNames[slot]}</color>";
                var b = UIKit.Button("Slot" + slot, _equipGrid, label, () => SelectEquip(s), 14);
                b.image.color = tpl > 0 ? UIKit.SlotColor : new Color(0.1f, 0.12f, 0.15f, 1f);
            }

            // ---- túi ----
            UIKit.Clear(_bagGrid);
            foreach (var kv in GameData.Inventory)
            {
                int tpl = kv.Key;
                string qty = kv.Value > 1 ? $"\n<color=#fd5>x{kv.Value}</color>" : "";
                var b = UIKit.Button("Item", _bagGrid, GameData.ItemName(tpl) + qty, () => SelectBag(tpl), 14);
                b.image.color = tpl == _selectedTemplate ? UIKit.ButtonHot : UIKit.SlotColor;
            }
            if (GameData.Inventory.Count == 0) UIKit.Text("Empty", _bagGrid, "(túi trống)", 14);

            UpdateDetail();
        }

        private void SelectBag(int tpl) { _selectedTemplate = tpl; _selectedEquipSlot = -1; Refresh(); }
        private void SelectEquip(int slot) { _selectedEquipSlot = slot; _selectedTemplate = -1; Refresh(); }

        private void UpdateDetail()
        {
            _btnUse.gameObject.SetActive(false);
            _btnEquip.gameObject.SetActive(false);
            _btnSell.gameObject.SetActive(false);

            if (_selectedEquipSlot > 0)
            {
                if (!GameData.Equipment.TryGetValue(_selectedEquipSlot, out int eq)) { _detail.text = "Ô trống"; return; }
                _detail.text = Describe(eq);
                _btnEquip.gameObject.SetActive(true);
                _btnEquip.SetLabel("Tháo");
                return;
            }
            if (_selectedTemplate < 0 || GameData.CountItem(_selectedTemplate) == 0) { _detail.text = "Chọn 1 vật phẩm"; return; }

            _detail.text = Describe(_selectedTemplate) + $"\nĐang có: {GameData.CountItem(_selectedTemplate)}";
            if (GameData.Items.TryGetValue(_selectedTemplate, out var t))
            {
                _btnUse.gameObject.SetActive(t.IsUsable);
                if (t.IsEquip) { _btnEquip.gameObject.SetActive(true); _btnEquip.SetLabel("Mặc"); }
                _btnSell.gameObject.SetActive(OpenShopNpc >= 0 && t.price > 0);
            }
        }

        /// <summary>Mô tả vật phẩm: tên, yêu cầu, chỉ số, giá.</summary>
        public static string Describe(int tplId)
        {
            if (!GameData.Items.TryGetValue(tplId, out var t)) return $"Vật phẩm {tplId}";
            var sb = new StringBuilder($"<b>{t.name}</b>");
            if (t.IsEquip) sb.Append($"  <color=#9aa>[{GameData.SlotNames[Mathf.Clamp(t.slot, 0, 9)]}]</color>");
            sb.Append('\n');
            if (!string.IsNullOrEmpty(t.description)) sb.Append($"<color=#bbb>{t.description}</color>\n");
            if (t.levelRequire > 1)
            {
                bool ok = GameData.Me.level >= t.levelRequire;
                sb.Append($"<color={(ok ? "#7f7" : "#f77")}>Yêu cầu cấp {t.levelRequire}</color>\n");
            }
            if (t.bonusDamage > 0) sb.Append($"+{t.bonusDamage} sát thương\n");
            if (t.bonusHp > 0) sb.Append($"+{t.bonusHp} HP tối đa\n");
            if (t.bonusMp > 0) sb.Append($"+{t.bonusMp} MP tối đa\n");
            if (t.hpRestore > 0) sb.Append($"Hồi {t.hpRestore} HP\n");
            if (t.mpRestore > 0) sb.Append($"Hồi {t.mpRestore} MP\n");
            if (t.price > 0) sb.Append($"<color=#fd5>Giá bán lại: {t.price / 2} yên</color>");
            return sb.ToString();
        }

        private void OnUse() { if (_selectedTemplate > 0) GameActions.UseItem(_selectedTemplate); }

        private void OnEquip()
        {
            if (_selectedEquipSlot > 0) { GameActions.Unequip(_selectedEquipSlot); _selectedEquipSlot = -1; }
            else if (_selectedTemplate > 0) GameActions.Equip(_selectedTemplate);
        }

        private void OnSell() { if (_selectedTemplate > 0) GameActions.Sell(_selectedTemplate, 1); }
    }
}
