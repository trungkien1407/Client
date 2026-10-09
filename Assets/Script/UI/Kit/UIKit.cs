using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// BỘ DỰNG UI (uGUI). Mỗi hàm Panel / Text / Button / Input / ScrollList / Bar / WindowFrame:
    ///   có prefab mẫu Resources/UI/Kit/&lt;Tên&gt; → tạo từ prefab (hình do bạn chỉnh trong Editor)
    ///   chưa có → dựng bằng code (hàm Build* bên dưới — tool cũng dùng chúng để sinh prefab mẫu lần đầu).
    /// Màu chung: UITheme (Resources/UI/UITheme.asset).
    /// </summary>
    public static class UIKit
    {
        // ---- Bảng màu chung (đọc từ UITheme) ----
        public static Color PanelColor => UITheme.Current.panel;
        public static Color HeaderColor => UITheme.Current.header;
        public static Color ButtonColor => UITheme.Current.button;
        public static Color ButtonHot => UITheme.Current.buttonHot;
        public static Color SlotColor => UITheme.Current.slot;
        public static Color TextColor => UITheme.Current.text;
        public static Color DimText => UITheme.Current.dimText;
        public static Color GoodText => UITheme.Current.goodText;
        public static Color BadText => UITheme.Current.badText;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5; // layer UI
            return (RectTransform)go.transform;
        }

        /// <summary>Đặt anchor + vị trí + kích thước gọn trong 1 dòng.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Kéo giãn kín cha, chừa lề.</summary>
        public static RectTransform Fill(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Tạo từ prefab mẫu UI/Kit/{kit} (đặt tên {name}); không có mẫu → null.</summary>
        private static T FromKit<T>(string kit, string name, Transform parent) where T : Component
        {
            var prefab = UIPrefabs.Kit(kit);
            if (prefab == null) return null;
            var go = UIPrefabs.Spawn(prefab, parent);
            go.name = name;
            return go.GetComponent<T>();
        }

        // ==========================================
        // PHẦN TỬ UI
        // ==========================================

        /// <summary>Khung nền (màu {color} nhân với sprite của mẫu).</summary>
        public static Image Panel(string name, Transform parent, Color color)
        {
            var img = FromKit<Image>("Panel", name, parent) ?? BuildPanel(name, parent);
            img.color = color;
            return img;
        }

        /// <summary>Chữ. {color} = null → màu của mẫu (hoặc UITheme.text khi dựng bằng code).</summary>
        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size = 18,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, Color? color = null)
        {
            var t = FromKit<TextMeshProUGUI>("Text", name, parent) ?? BuildText(name, parent);
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            if (color.HasValue) t.color = color.Value;
            return t;
        }

        /// <summary>Nút có chữ. Màu nút lấy từ mẫu; đổi riêng bằng button.image.color.</summary>
        public static Button Button(string name, Transform parent, string label, Action onClick, float fontSize = 18)
        {
            var btn = FromKit<Button>("Button", name, parent) ?? BuildButton(name, parent);
            var t = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) { t.text = label; t.fontSize = fontSize; }
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>
        /// Ô NHẬP CHỮ (TMP_InputField). Đọc giá trị: input.text.
        /// Đang gõ ô này thì phím tắt game tự bị bỏ qua (xem UIKit.IsTypingInUI).
        /// </summary>
        public static TMP_InputField Input(string name, Transform parent, string placeholder, float fontSize = 17,
            TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard, int charLimit = 0)
        {
            var input = FromKit<TMP_InputField>("Input", name, parent) ?? BuildInput(name, parent);
            if (input.placeholder is TextMeshProUGUI ph) { ph.text = placeholder; ph.fontSize = fontSize; }
            if (input.textComponent != null) input.textComponent.fontSize = fontSize;
            input.contentType = type;
            input.characterLimit = charLimit;
            input.pointSize = fontSize;
            return input;
        }

        /// <summary>Vùng cuộn dọc. Trả về "content" — thêm con vào đây, tự xếp dọc.</summary>
        public static RectTransform ScrollList(string name, Transform parent, float spacing = 4)
        {
            var content = ScrollContent(name, parent);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            return content;
        }

        /// <summary>Vùng cuộn dọc xếp lưới ô vuông (túi đồ, rương). Trả về "content".</summary>
        public static RectTransform ScrollGrid(string name, Transform parent, Vector2 cell, Vector2 spacing, int columns)
        {
            var content = ScrollContent(name, parent);
            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.padding = new RectOffset(6, 6, 6, 6);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            return content;
        }

        private static RectTransform ScrollContent(string name, Transform parent)
        {
            var scroll = FromKit<ScrollRect>("ScrollList", name, parent) ?? BuildScrollList(name, parent);
            return scroll.content;
        }

        /// <summary>Lưới ô vuông (không cuộn). Trả về transform cha — thêm ô con vào.</summary>
        public static RectTransform Grid(string name, Transform parent, Vector2 cell, Vector2 spacing, int columns)
        {
            var rt = Rect(name, parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            return rt;
        }

        /// <summary>Thanh tiến trình (máu / EXP): trả về Image "Fill" (đổi fillAmount 0..1).</summary>
        public static Image Bar(string name, Transform parent, Color fill)
        {
            var root = FromKit<Image>("Bar", name, parent) ?? BuildBar(name, parent);
            var f = root.transform.Find("Fill").GetComponent<Image>();
            f.color = fill;
            f.fillAmount = 0;
            return f;
        }

        /// <summary>Khung cửa sổ (GameWindow dùng).</summary>
        public static WindowFrame WindowFrame(string name, Transform parent)
            => FromKit<WindowFrame>("Window", name, parent) ?? BuildWindowFrame(name, parent);

        // ==========================================
        // DỰNG BẰNG CODE (khi chưa có prefab mẫu)
        // ==========================================

        public static Image BuildPanel(string name, Transform parent)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = PanelColor;
            return img;
        }

        public static TextMeshProUGUI BuildText(string name, Transform parent)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = 18;
            t.color = TextColor;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        public static Button BuildButton(string name, Transform parent)
        {
            var img = Panel(name, parent, ButtonColor);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            var t = Text("Label", img.transform, "", 18, TextAlignmentOptions.Center);
            t.rectTransform.Fill(6, 6, 2, 2);
            return btn;
        }

        public static TMP_InputField BuildInput(string name, Transform parent)
        {
            var bg = Panel(name, parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            var area = Rect("TextArea", bg.transform).Fill(8, 8, 3, 3);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Text("Placeholder", area, "", 17, TextAlignmentOptions.MidlineLeft, new Color(1, 1, 1, 0.35f));
            ph.rectTransform.Fill();
            ph.enableWordWrapping = false;
            var txt = Text("Text", area, "", 17, TextAlignmentOptions.MidlineLeft);
            txt.rectTransform.Fill();
            txt.enableWordWrapping = false;
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = txt.font;
            return input;
        }

        public static ScrollRect BuildScrollList(string name, Transform parent)
        {
            var view = Panel(name, parent, new Color(0, 0, 0, 0.25f));
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;

            var content = Rect("Content", view.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = (RectTransform)view.transform;
            return scroll;
        }

        public static Image BuildBar(string name, Transform parent)
        {
            var bg = Panel(name, parent, new Color(0, 0, 0, 0.6f));
            var f = Panel("Fill", bg.transform, Color.white);
            f.rectTransform.Fill(2, 2, 2, 2);
            f.sprite = WhiteSprite;   // Image kiểu Filled cần sprite
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            return bg;
        }

        public static WindowFrame BuildWindowFrame(string name, Transform parent)
        {
            var root = BuildPanel(name, parent);   // gốc khung không lồng mẫu Panel
            var rt = root.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 420));
            var frame = root.gameObject.AddComponent<WindowFrame>();

            var header = Panel("Header", root.transform, HeaderColor);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 36), new Vector2(0.5f, 1));
            header.gameObject.AddComponent<WindowDragger>().target = rt;
            frame.header = header.rectTransform;
            frame.title = Text("Title", header.transform, "Cửa sổ", 20, TextAlignmentOptions.Center);
            frame.title.rectTransform.Fill(40, 40);
            frame.close = Button("Close", header.transform, "X", null, 18);
            ((RectTransform)frame.close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(32, 28));
            frame.body = Rect("Body", root.transform).Fill(10, 10, 44, 10);
            return frame;
        }

        // ==========================================
        // TIỆN ÍCH
        // ==========================================

        /// <summary>Đang gõ vào 1 ô nhập UI (để phím tắt game như I/C/K/F, di chuyển... không chạy).</summary>
        public static bool IsTypingInUI()
        {
            var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return go != null && go.GetComponent<TMP_InputField>() is TMP_InputField f && f.isFocused;
        }

        /// <summary>Đổi chữ trên nút đã tạo bằng Button().</summary>
        public static void SetLabel(this Button b, string label)
        {
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = label;
        }

        /// <summary>Cho phần tử trong VerticalLayoutGroup có chiều cao cố định.</summary>
        public static T Height<T>(this T c, float h) where T : Component
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
            return c;
        }

        /// <summary>Xoá hết con (dùng khi vẽ lại danh sách).</summary>
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        private static Sprite _white;

        /// <summary>Sprite trắng 4x4 (Resources/UI/Kit/White.png do tool tạo; chưa có thì tạo tạm lúc chạy).</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white == null) _white = Resources.Load<Sprite>(UIPrefabs.KitFolder + "White");
                if (_white == null) _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }
    }
}
