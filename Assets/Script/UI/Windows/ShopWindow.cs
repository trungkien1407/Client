using System.Collections.Generic;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI.Windows
{
    /// <summary>Một mặt hàng trong shop: mã đồ, giá, loại tiền (0 yên / 1 xu / 2 lượng).</summary>
    public struct ShopGood { public int itemId, price, currency; }

    /// <summary>
    /// CỬA HÀNG NPC (mở khi nhận SHOP_DATA). Mua: [Mua 1] / [Mua 10] (vật phẩm xếp chồng).
    /// Mỗi món có thể bán bằng yên, xu hoặc lượng. Mua bằng xu/lượng → đồ KHOÁ (không giao dịch được).
    /// Bán: mở kèm cửa sổ Hành trang → chọn vật phẩm → [Bán] (được nửa giá, đồ khoá 0 yên).
    /// </summary>
    public class ShopWindow : GameWindow
    {
        [SerializeField] private RectTransform _list;
        [SerializeField] private TextMeshProUGUI _money;
        private int _npcId = -1;
        private readonly List<ShopGood> _goods = new List<ShopGood>();

        protected override void Configure()
        {
            Title = "Cửa hàng";
            Size = new Vector2(460, 480);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _money = UIKit.Text("Money", body, "", 17, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f));
            _money.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 28), new Vector2(0.5f, 1));
            _list = UIKit.ScrollList("List", body, 6);
            ((RectTransform)_list.parent).Fill(0, 0, 32, 0);
        }

        protected override void Bind()
        {
            GameData.OnChanged += k => { if (k == DataKind.Character || k == DataKind.Money || k == DataKind.Templates) RefreshIfOpen(); };
        }

        public void ShowShop(int npcId, List<ShopGood> goods)
        {
            _npcId = npcId;
            _goods.Clear();
            _goods.AddRange(goods);
            InventoryWindow.OpenShopNpc = npcId;
            Show();
            // Mở luôn túi đồ bên cạnh để bán
            SideBySide(this, Open<InventoryWindow>());
        }

        protected override void Refresh()
        {
            _money.text = $"Yên {GameData.Me.yen:N0}   <color=#7cf>Xu {GameData.Me.xu:N0}</color>   <color=#f8c>Lượng {GameData.Me.luong:N0}</color>";
            UIKit.Clear(_list);
            foreach (var g in _goods)
            {
                int id = g.itemId, price = g.price, cur = g.currency;
                GameData.Items.TryGetValue(id, out var t);
                string curColor = cur == 1 ? "#7cf" : cur == 2 ? "#f8c" : "#fd5";
                var row = UIKit.Panel("Row" + id, _list, UIKit.SlotColor).Height(64);
                var txt = UIKit.Text("Name", row.transform, $"<b>{GameData.ItemName(id)}</b>\n<color={curColor}>{price:N0} {GameData.CurrencyName(cur)}</color>" +
                                                            (t != null && t.levelRequire > 1 ? $"  <size=13>cấp {t.levelRequire}</size>" : "") +
                                                            (cur > 0 ? "  <size=12><color=#aaa>(đồ khoá)</color></size>" : ""), 16);
                txt.rectTransform.Fill(8, 190, 4, 4);
                int have = GameData.Money(cur);
                var b1 = UIKit.Button("Buy1", row.transform, "Mua 1", () => GameActions.Buy(_npcId, id, 1), 16);
                ((RectTransform)b1.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 0), new Vector2(84, 36), new Vector2(1, 0.5f));
                b1.interactable = have >= price;
                if (t != null && t.maxStack > 1)
                {
                    var b10 = UIKit.Button("Buy10", row.transform, "Mua 10", () => GameActions.Buy(_npcId, id, 10), 16);
                    ((RectTransform)b10.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(84, 36), new Vector2(1, 0.5f));
                    b10.interactable = have >= price * 10;
                }
            }
        }

        private void OnDisable()
        {
            InventoryWindow.OpenShopNpc = -1;
        }
    }
}
