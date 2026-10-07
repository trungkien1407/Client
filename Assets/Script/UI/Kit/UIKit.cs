using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// BỘ DỰNG UI BẰNG CODE (uGUI). Dùng cho các cửa sổ game (túi đồ, nhân vật, kỹ năng...) để không phụ thuộc
    /// prefab kéo-thả. Muốn đổi giao diện đẹp hơn: sửa màu/kích thước ở đây (1 chỗ) hoặc thay dần bằng prefab.
    ///
    /// [CẦN ĐIỀN khi có art UI] gán Sprite khung/nút vào PanelSprite / ButtonSprite để thay ô màu trơn.
    /// </summary>
    public static class UIKit
    {
        // ---- Bảng màu chung ----
        public static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.14f, 0.94f);
        public static readonly Color HeaderColor = new Color(0.85f, 0.45f, 0.10f, 1f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.27f, 0.36f, 1f);
        public static readonly Color ButtonHot = new Color(0.90f, 0.55f, 0.15f, 1f);
        public static readonly Color SlotColor = new Color(0.16f, 0.19f, 0.25f, 1f);
        public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color DimText = new Color(0.70f, 0.74f, 0.80f, 1f);
        public static readonly Color GoodText = new Color(0.45f, 1f, 0.45f, 1f);
        public static readonly Color BadText = new Color(1f, 0.45f, 0.45f, 1f);

        /// <summary>[CẦN ĐIỀN tuỳ chọn] Sprite 9-slice cho khung cửa sổ / nút. Để null = ô màu trơn.</summary>
        public static Sprite PanelSprite, ButtonSprite;

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

        public static Image Panel(string name, Transform parent, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            if (PanelSprite != null) { img.sprite = PanelSprite; img.type = Image.Type.Sliced; }
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size = 18,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, Color? color = null)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Action onClick, float fontSize = 18)
        {
            var img = Panel(name, parent, ButtonColor);
            if (ButtonSprite != null) img.sprite = ButtonSprite;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            var t = Text("Label", img.transform, label, fontSize, TextAlignmentOptions.Center);
            t.rectTransform.Fill(6, 6, 2, 2);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>
        /// Ô NHẬP CHỮ (TMP_InputField) dựng bằng code: nền + vùng chữ + chữ mờ gợi ý.
        /// Đọc giá trị: input.text. Đang gõ ô này thì phím tắt game tự bị bỏ qua (xem UIKit.IsTypingInUI).
        /// </summary>
        public static TMP_InputField Input(string name, Transform parent, string placeholder, float fontSize = 17,
            TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard, int charLimit = 0)
        {
            var bg = Panel(name, parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            var area = Rect("TextArea", bg.transform).Fill(8, 8, 3, 3);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Text("Placeholder", area, placeholder, fontSize, TextAlignmentOptions.MidlineLeft, new Color(1, 1, 1, 0.35f));
            ph.rectTransform.Fill();
            ph.enableWordWrapping = false;
            var txt = Text("Text", area, "", fontSize, TextAlignmentOptions.MidlineLeft);
            txt.rectTransform.Fill();
            txt.enableWordWrapping = false;
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.contentType = type;
            input.characterLimit = charLimit;
            input.fontAsset = txt.font;
            input.pointSize = fontSize;
            return input;
        }

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

        /// <summary>
        /// Vùng cuộn dọc. Trả về "content" — thêm con vào đây, tự xếp dọc (VerticalLayoutGroup).
        /// </summary>
        public static RectTransform ScrollList(string name, Transform parent, float spacing = 4)
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
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = (RectTransform)view.transform;
            return content;
        }

        /// <summary>Lưới ô vuông (túi đồ). Trả về transform cha — thêm ô con vào.</summary>
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

        /// <summary>Đang trỏ/chạm lên UI không (để click UI không bị tính là click chọn mục tiêu trong map).</summary>
        public static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>Thanh tiến trình (máu/EXP): trả về Image fill (đổi fillAmount 0..1).</summary>
        public static Image Bar(string name, Transform parent, Color fill)
        {
            var bg = Panel(name, parent, new Color(0, 0, 0, 0.6f));
            var f = Panel("Fill", bg.transform, fill);
            f.rectTransform.Fill(2, 2, 2, 2);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillAmount = 0;
            // Image.Filled cần sprite; dùng sprite trắng có sẵn của Unity
            f.sprite = WhiteSprite;
            return f;
        }

        private static Sprite _white;
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white == null)
                    _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }
    }
}
