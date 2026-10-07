using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// RƯƠNG ĐỒ (mở ở Thợ Rèn → "Rương đồ", server gửi STORAGE_DATA). Giữ đồ an toàn, không mất khi chết.
    ///  - Bấm 1 ô trong rương → [Lấy 1] / [Lấy hết] về túi.
    ///  - Cất đồ: chọn ô bên cửa sổ Hành trang (tự mở cạnh) → [Cất rương].
    /// Server kiểm NPC còn ở gần; đi xa thì thao tác bị từ chối.
    /// </summary>
    public class StorageWindow : GameWindow
    {
        private RectTransform _grid;
        private TextMeshProUGUI _title, _detail;
        private Button _take1, _takeAll;
        private int _sel = -1;

        protected override void Build()
        {
            Title = "Rương đồ";
            Size = new Vector2(460, 500);
            var body = CreateBody();
            _title = UIKit.Text("Cap", body, "", 16, TextAlignmentOptions.MidlineLeft, UIKit.DimText);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 24), new Vector2(0.5f, 1));

            var scroll = UIKit.ScrollList("Grid", body);
            ((RectTransform)scroll.parent).Fill(0, 0, 28, 110);
            Object.DestroyImmediate(scroll.GetComponent<VerticalLayoutGroup>());
            var g = scroll.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(80, 62);
            g.spacing = new Vector2(6, 6);
            g.padding = new RectOffset(6, 6, 6, 6);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            _grid = scroll;

            var bottom = UIKit.Panel("Bottom", body, new Color(0, 0, 0, 0.3f)).rectTransform;
            bottom.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 104), new Vector2(0.5f, 0));
            _detail = UIKit.Text("Detail", bottom, "Chọn 1 ô trong rương. Muốn cất: chọn đồ trong Hành trang → [Cất rương].", 14, TextAlignmentOptions.TopLeft);
            _detail.rectTransform.Fill(8, 200, 6, 6);
            _take1 = UIKit.Button("Take1", bottom, "Lấy 1", () => Take(false), 15);
            ((RectTransform)_take1.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(88, 36), new Vector2(1, 0.5f));
            _takeAll = UIKit.Button("TakeAll", bottom, "Lấy hết", () => Take(true), 15);
            ((RectTransform)_takeAll.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(88, 36), new Vector2(1, 0.5f));

            GameData.OnChanged += k => { if (k == DataKind.Storage || k == DataKind.Templates) RefreshIfOpen(); };
        }

        public void OpenWithBag()
        {
            _sel = -1;
            Show();
            SideBySide(this, Open<InventoryWindow>());
        }

        protected override void Refresh()
        {
            _title.text = $"Đang chứa {GameData.Storage.Count}/{GameData.StorageCapacity} ô";
            UIKit.Clear(_grid);
            for (int i = 0; i < GameData.Storage.Count; i++)
            {
                int idx = i;
                var s = GameData.Storage[i];
                string qty = s.qty > 1 ? $"\n<color=#fd5>x{s.qty}</color>" : "";
                var b = UIKit.Button("S" + i, _grid, GameData.ColoredName(s.tpl, s.level) + qty, () => { _sel = idx; Refresh(); }, 13);
                b.image.color = idx == _sel ? UIKit.ButtonHot : UIKit.SlotColor;
            }
            if (GameData.Storage.Count == 0) UIKit.Text("Empty", _grid, "(trống)", 14);

            var sel = _sel >= 0 && _sel < GameData.Storage.Count ? GameData.Storage[_sel] : null;
            _take1.interactable = _takeAll.interactable = sel != null;
            if (sel != null) _detail.text = InventoryWindow.Describe(sel.tpl, sel.level, sel.locked) + (sel.qty > 1 ? $"\nSố lượng: {sel.qty}" : "");
            // Cửa sổ túi đổi nút theo việc rương đang mở → vẽ lại nó
            Get<InventoryWindow>().RefreshIfOpen();
        }

        private void Take(bool all)
        {
            if (_sel < 0 || _sel >= GameData.Storage.Count) return;
            GameActions.StorageTake(_sel, all ? GameData.Storage[_sel].qty : 1);
            if (all) _sel = -1;
        }

        private void OnDisable()
        {
            // Không dùng Get<>() ở đây: lúc thoát game UIRoot có thể đã bị huỷ
            var inv = transform.parent != null ? transform.parent.GetComponentInChildren<InventoryWindow>(true) : null;
            if (inv != null) inv.RefreshIfOpen();
        }
    }
}
