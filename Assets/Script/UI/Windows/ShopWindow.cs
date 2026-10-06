using System.Collections.Generic;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CỬA HÀNG NPC (mở khi nhận SHOP_DATA). Mua: [Mua 1] / [Mua 10] (vật phẩm xếp chồng).
    /// Bán: mở kèm cửa sổ Hành trang → chọn vật phẩm → [Bán] (được nửa giá).
    /// </summary>
    public class ShopWindow : GameWindow
    {
        private RectTransform _list;
        private TextMeshProUGUI _money;
        private int _npcId = -1;
        private readonly List<KeyValuePair<int, int>> _goods = new List<KeyValuePair<int, int>>(); // (itemId, price)

        protected override void Build()
        {
            Title = "Cửa hàng";
            Size = new Vector2(480, 480);
            var body = CreateBody();
            _money = UIKit.Text("Money", body, "", 18, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f));
            _money.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 28), new Vector2(0.5f, 1));
            _list = UIKit.ScrollList("List", body, 6);
            ((RectTransform)_list.parent).Fill(0, 0, 32, 0);
            GameData.OnChanged += k => { if (k == DataKind.Character || k == DataKind.Templates) RefreshIfOpen(); };
        }

        public void ShowShop(int npcId, List<KeyValuePair<int, int>> goods)
        {
            _npcId = npcId;
            _goods.Clear();
            _goods.AddRange(goods);
            InventoryWindow.OpenShopNpc = npcId;
            Show();
            // Mở luôn túi đồ bên cạnh để bán
            var bag = Open<InventoryWindow>();
            ((RectTransform)bag.transform).anchoredPosition = new Vector2(250, 0);
            ((RectTransform)transform).anchoredPosition = new Vector2(-400, 0);
        }

        protected override void Refresh()
        {
            _money.text = $"Yên đang có: {GameData.Me.yen:N0}";
            UIKit.Clear(_list);
            foreach (var g in _goods)
            {
                int id = g.Key, price = g.Value;
                GameData.Items.TryGetValue(id, out var t);
                var row = UIKit.Panel("Row" + id, _list, UIKit.SlotColor).Height(64);
                var txt = UIKit.Text("Name", row.transform, $"<b>{GameData.ItemName(id)}</b>\n<color=#fd5>{price:N0} yên</color>" +
                                                            (t != null && t.levelRequire > 1 ? $"  <size=13>cấp {t.levelRequire}</size>" : ""), 16);
                txt.rectTransform.Fill(8, 190, 4, 4);
                var b1 = UIKit.Button("Buy1", row.transform, "Mua 1", () => GameActions.Buy(_npcId, id, 1), 16);
                ((RectTransform)b1.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 0), new Vector2(84, 36), new Vector2(1, 0.5f));
                b1.interactable = GameData.Me.yen >= price;
                if (t != null && t.maxStack > 1)
                {
                    var b10 = UIKit.Button("Buy10", row.transform, "Mua 10", () => GameActions.Buy(_npcId, id, 10), 16);
                    ((RectTransform)b10.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(84, 36), new Vector2(1, 0.5f));
                    b10.interactable = GameData.Me.yen >= price * 10;
                }
            }
        }

        private void OnDisable()
        {
            InventoryWindow.OpenShopNpc = -1;
        }
    }
}
