using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// BẢNG KẺ HÀNG cho cửa sổ (2026-11-15): thay khối chữ nhiều dòng "Nhãn: giá trị" khó dò bằng các HÀNG nền xen kẽ sáng / tối,
    /// có vạch dọc chia cột và viền ngoài. Cột tính theo TỈ LỆ bề ngang (0..1) → bảng co giãn theo cửa sổ.
    ///
    /// Cách dùng:
    ///   var t = new UITable("Info", Body, 27);            // cao mỗi hàng 27 px
    ///   t.Root.Place(...);                                 // đặt chỗ (neo trên, cao tự tính ở Finish)
    ///   t.Header(Col2, "Tên", "Cấp 1");                     // hàng tiêu đề (chữ vàng, nền đậm)
    ///   var cells = t.Row(Col2, "HP", "100/100");          // cells[1].text = ... để cập nhật về sau
    ///   var row = t.Custom(56); UITable.Cell(row, 0.5f, 1f) // hàng tự đặt nội dung (nút...)
    ///   t.Finish();                                         // chốt chiều cao + viền ngoài
    /// Dựng lại mỗi lần Refresh: t.Clear() rồi thêm hàng như trên.
    /// </summary>
    public sealed class UITable
    {
        public static readonly Color HeadBg = new Color(0f, 0f, 0f, 0.42f);
        public static readonly Color RowA = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color RowB = new Color(0f, 0f, 0f, 0.20f);
        public static readonly Color Line = new Color(1f, 0.85f, 0.5f, 0.22f);
        public static readonly Color Border = new Color(1f, 0.82f, 0.40f, 0.45f);

        public readonly RectTransform Root;
        private readonly float _rowH;
        private float _y;
        private int _rows;

        public UITable(string name, Transform parent, float rowH = 26f)
        {
            Root = UIKit.Rect(name, parent);
            _rowH = rowH;
        }

        /// <summary>Tổng chiều cao các hàng đã thêm.</summary>
        public float Height => _y;

        /// <summary>Hàng tiêu đề: chữ vàng đậm trên nền tối.</summary>
        public TextMeshProUGUI[] Header(float[] cols, params string[] cells)
        {
            var row = NewRow(_rowH + 4, HeadBg, cols);
            var t = FillCells(row, cols, cells, 16f);
            foreach (var c in t) { c.color = UIStyle.Gold; c.fontStyle = FontStyles.Bold; }
            return t;
        }

        /// <summary>Hàng thường: ô đầu (nhãn) chữ mờ, các ô sau chữ sáng. Trả mảng chữ từng ô để cập nhật sau.</summary>
        public TextMeshProUGUI[] Row(float[] cols, params string[] cells)
        {
            var row = NewRow(_rowH, _rows % 2 == 0 ? RowA : RowB, cols);
            var t = FillCells(row, cols, cells, 15f);
            if (t.Length > 1) t[0].color = new Color(0.82f, 0.86f, 0.90f);
            return t;
        }

        /// <summary>Hàng tự đặt nội dung (cao {h}); {cols} chỉ để kẻ vạch chia cột (null = không kẻ).</summary>
        public RectTransform Custom(float h, float[] cols = null) => NewRow(h, _rows % 2 == 0 ? RowA : RowB, cols);

        /// <summary>Ô từ tỉ lệ {x0} tới {x1} bề ngang hàng (chừa lề 8 px hai bên).</summary>
        public static RectTransform Cell(RectTransform row, float x0, float x1, float pad = 8f)
        {
            var c = UIKit.Rect("Cell", row);
            c.anchorMin = new Vector2(x0, 0f);
            c.anchorMax = new Vector2(x1, 1f);
            c.offsetMin = new Vector2(pad, 0f);
            c.offsetMax = new Vector2(-pad, 0f);
            return c;
        }

        /// <summary>Chốt chiều cao bảng + viền ngoài bo góc.</summary>
        public void Finish()
        {
            Root.sizeDelta = new Vector2(Root.sizeDelta.x, _y);
            if (Application.isPlaying) AddBorder(Root);
        }

        /// <summary>
        /// Viền vàng bo góc quanh bảng. CHỈ gắn lúc chạy game: hình bo góc (UIStyle.Ring) vẽ bằng code lúc chạy, xuất prefab thì
        /// hình không được lưu → Image mất hình thành 1 tấm vàng mờ PHỦ KÍN bảng (đã gặp). Cửa sổ dựng từ prefab gọi hàm này trong Bind().
        /// </summary>
        public static void AddBorder(Transform root)
        {
            if (root == null) return;
            UIStyle.AddRing(root, Border, 2.5f).transform.SetAsLastSibling();
        }

        /// <summary>Xoá mọi hàng (giữ Root) để dựng lại.</summary>
        public void Clear()
        {
            for (int i = Root.childCount - 1; i >= 0; i--)
            {
                var ch = Root.GetChild(i);
                if (ch.name == "Ring") continue;
                Object.Destroy(ch.gameObject);
            }
            _y = 0f;
            _rows = 0;
        }

        // ------------------------------------------------------------------

        private RectTransform NewRow(float h, Color bg, float[] cols)
        {
            var row = UIKit.Rect("Row" + _rows, Root);
            row.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -_y), new Vector2(0, h), new Vector2(0.5f, 1));
            var img = row.gameObject.AddComponent<Image>();
            img.sprite = UIKit.WhiteSprite;
            img.color = bg;
            img.raycastTarget = false;
            row.gameObject.AddComponent<NoSkin>();
            // vạch dọc giữa các cột
            if (cols != null)
            {
                float x = 0f;
                for (int i = 0; i < cols.Length - 1; i++)
                {
                    x += cols[i];
                    var line = UIKit.Rect("Sep", row);
                    line.anchorMin = new Vector2(x, 0f);
                    line.anchorMax = new Vector2(x, 1f);
                    line.sizeDelta = new Vector2(1f, 0f);
                    line.anchoredPosition = Vector2.zero;
                    var li = line.gameObject.AddComponent<Image>();
                    li.sprite = UIKit.WhiteSprite;
                    li.color = Line;
                    li.raycastTarget = false;
                    line.gameObject.AddComponent<NoSkin>();
                }
            }
            _y += h;
            _rows++;
            return row;
        }

        private static TextMeshProUGUI[] FillCells(RectTransform row, float[] cols, string[] cells, float size)
        {
            var res = new TextMeshProUGUI[cols.Length];
            float x = 0f;
            for (int i = 0; i < cols.Length; i++)
            {
                var cell = Cell(row, x, x + cols[i]);
                x += cols[i];
                var t = UIKit.Text("T", cell, i < cells.Length && cells[i] != null ? cells[i] : "", size, TextAlignmentOptions.MidlineLeft);
                t.rectTransform.Fill();
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.enableAutoSizing = true;      // chữ dài (số tiền lớn) tự nhỏ lại thay vì tràn ô
                t.fontSizeMax = size;
                t.fontSizeMin = size - 4f;
                t.raycastTarget = false;
                res[i] = t;
            }
            return res;
        }
    }
}
