using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>Hộp nhập SỐ LƯỢNG + GIÁ khi treo bán; hiện trước phí treo và số yên nhận được sau thuế.</summary>
    public class MarketSellDialog : GameWindow
    {
        [SerializeField] private TextMeshProUGUI _info, _calc;
        [SerializeField] private TMP_InputField _qty, _price;
        [SerializeField] private Button _ok, _cancel;
        private int _bagIndex;

        protected override void Configure()
        {
            Title = "Treo bán";
            Size = new Vector2(440, 300);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _info = UIKit.Text("Info", body, "", 16, TextAlignmentOptions.TopLeft);
            _info.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 50), new Vector2(0.5f, 1));
            UIKit.Text("LQ", body, "Số lượng", 15).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -56), new Vector2(100, 34), new Vector2(0, 1));
            _qty = UIKit.Input("Qty", body, "1", 16, TMP_InputField.ContentType.IntegerNumber, 4);
            ((RectTransform)_qty.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -56), new Vector2(120, 34), new Vector2(0, 1));
            UIKit.Text("LP", body, "Giá (yên)", 15).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -96), new Vector2(100, 34), new Vector2(0, 1));
            _price = UIKit.Input("Price", body, "cho cả chồng", 16, TMP_InputField.ContentType.IntegerNumber, 10);
            ((RectTransform)_price.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -96), new Vector2(200, 34), new Vector2(0, 1));
            _calc = UIKit.Text("Calc", body, "", 14, TextAlignmentOptions.TopLeft, UIKit.DimText);
            _calc.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -136), new Vector2(0, 50), new Vector2(0.5f, 1));
            _ok = UIKit.Button("Ok", body, "Treo bán", null, 16);
            ((RectTransform)_ok.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(140, 42), new Vector2(1, 0));
            _ok.image.color = UIKit.ButtonHot;
            _cancel = UIKit.Button("Cancel", body, "Huỷ", null, 16);
            ((RectTransform)_cancel.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(120, 42), Vector2.zero);
        }

        protected override void Bind()
        {
            _price.onValueChanged.AddListener(_ => UpdateCalc());
            _ok.onClick.AddListener(Submit);
            _cancel.onClick.AddListener(Hide);
        }

        public void Ask(int bagIndex, BagSlot s)
        {
            _bagIndex = bagIndex;
            Show();
            _info.text = $"<b>{GameData.ColoredName(s.tpl, s.level)}</b>  <color=#9aa>(có {s.qty})</color>";
            _qty.text = s.qty.ToString();
            _price.text = "";
            UpdateCalc();
            UIRoot.Instance.BringToFront(this);
        }

        private void UpdateCalc()
        {
            if (!long.TryParse(_price.text, out long p) || p <= 0) { _calc.text = "Nhập giá bán cho CẢ số lượng ở trên."; return; }
            long fee = GameData.MarketFee(p), gets = p - p * GameData.MarketTaxPercent / 100;
            _calc.text = $"Phí treo ngay: <color=#fd5>{fee:N0} yên</color> (không hoàn)\nBán được bạn nhận: <color=#7f7>{gets:N0} yên</color> (trừ {GameData.MarketTaxPercent}% thuế, qua hòm thư)";
        }

        private void Submit()
        {
            if (!int.TryParse(_qty.text, out int q) || q <= 0) return;
            if (!int.TryParse(_price.text, out int p) || p <= 0) return;
            GameActions.MarketSell(_bagIndex, q, p);
            InventoryWindow.SelectedIndex = -1;
            Hide();
        }
    }
}
