using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// SKIN UI BẰNG HÌNH: có hình trong Resources/UISkin/ thì khung cửa sổ, tiêu đề, nút, ô nhập, khung danh sách, ô đồ
    /// dùng hình đó (9-slice); không có (bản public chưa có hình riêng) → giao diện màu phẳng như cũ, không lỗi gì.
    ///   Hình demo nội bộ: Tools → Naruto → Demo art → 1. Nhập ... (copy từ TaiNguyen/08_UI vào Assets/_Demo/Resources/UISkin, KHÔNG commit).
    ///   Hình riêng khi phát hành: đặt cùng tên vào một thư mục Resources/UISkin (xem docs/ky-thuat/TAI_NGUYEN.md).
    /// Cách áp:
    ///   - Bảng màu (UITheme) được chỉnh cho hợp hình: nút kem chữ nâu, nút đang chọn vàng, ô / hàng tím đậm (Restyle).
    ///   - Hình NHẠT (kem) + màu theme nhân vào → code cũ đổi màu (ButtonHot, SlotColor...) vẫn đúng, chỉ thêm khung bo / viền.
    ///   - Cửa sổ / HUD: SkinTree lúc tạo; nút, ô nhập tạo thêm lúc chạy: UIKit.Button / UIKit.Input gọi SkinButton / SkinInput.
    ///   - Chỉ đổi Image đang là MÀU PHẲNG (chưa có hình riêng) → nút tròn HUD, ô kỹ năng, icon... giữ nguyên.
    /// </summary>
    public static class UISkin
    {
        public const string Folder = "UISkin/";
        /// <summary>Tên hình (Resources/UISkin/&lt;tên&gt;) — DemoArtTool chép đúng các tên này.</summary>
        public const string Window = "window", Header = "header", Close = "close", Button = "button",
                            Input = "input", List = "list", Slot = "slot";

        // Hình vẽ ở độ phân giải cao (viền 30–40 px) → thu viền lại cho hợp khung 1280x720
        private const float BorderScale = 2f;
        private static readonly Color ButtonText = new Color(0.30f, 0.16f, 0.12f, 1f);   // chữ nâu trên nút kem

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static bool? _enabled;
        private static Color _oldButton, _oldSlot, _oldPanel;   // màu theme gốc (prefab mẫu đã nướng sẵn màu này)

        public static Sprite Get(string key)
        {
            if (!Cache.TryGetValue(key, out var s)) Cache[key] = s = Resources.Load<Sprite>(Folder + key);
            return s;
        }

        /// <summary>
        /// Có bộ hình skin (đủ hình khung cửa sổ + nút). CHỈ lúc chạy game: tool Editor xuất prefab UI (UIPrefabTool) mà dùng skin thì
        /// prefab được commit sẽ trỏ tới hình demo (gitignore) → hỏng trên máy khác.
        /// </summary>
        public static bool Enabled => Application.isPlaying && (_enabled ??= Get(Window) != null && Get(Button) != null);

        /// <summary>UITheme.Current gọi 1 lần: có skin → bản sao bảng màu chỉnh cho hợp hình (không sửa asset gốc).</summary>
        public static UITheme Restyle(UITheme theme)
        {
            _oldButton = theme.button; _oldSlot = theme.slot; _oldPanel = theme.panel;
            if (!Enabled) return theme;
            var t = Object.Instantiate(theme);
            t.button = Color.white;                                // hình kem giữ nguyên màu
            t.buttonHot = new Color(1f, 0.80f, 0.35f, 1f);         // vàng (đang chọn / nút chính)
            t.slot = new Color(0.36f, 0.24f, 0.34f, 1f);           // ô / hàng: tím mận đậm, chữ trắng đọc rõ
            t.panel = new Color(0.22f, 0.13f, 0.20f, 0.92f);       // khung phụ trong cửa sổ
            return t;
        }

        // ------------------------------------------------------------------ áp cho cả 1 cây (cửa sổ / HUD)
        public static void SkinTree(Transform root)
        {
            if (!Enabled || root == null) return;
            foreach (var f in root.GetComponentsInChildren<WindowFrame>(true)) SkinWindow(f);
            foreach (var b in root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) SkinButton(b);
            foreach (var i in root.GetComponentsInChildren<TMP_InputField>(true)) SkinInput(i);
            foreach (var s in root.GetComponentsInChildren<ScrollRect>(true))
                if (s.TryGetComponent(out Image img) && IsFlat(img)) Set(img, Get(List), keepColor: true);
            foreach (var img in root.GetComponentsInChildren<Image>(true)) SkinPanel(img);
        }

        public static void SkinWindow(WindowFrame f)
        {
            if (!Enabled || f == null) return;
            if (f.TryGetComponent(out Image bg)) { Set(bg, Get(Window)); bg.color = Color.white; }
            if (f.header != null && f.header.TryGetComponent(out Image head) && Get(Header) != null) { Set(head, Get(Header)); head.color = Color.white; }
            if (f.title != null)
            {
                // Tiêu đề rõ hơn: đậm, vàng kem, to vừa thanh (tên dài tự nhỏ lại)
                f.title.fontStyle = FontStyles.Bold;
                f.title.color = new Color(1f, 0.92f, 0.72f);
                f.title.enableAutoSizing = true;
                f.title.fontSizeMin = 14;
                f.title.fontSizeMax = 21;
            }
            if (f.close != null && Get(Close) != null)
            {
                Set(f.close.image, Get(Close));
                f.close.image.type = Image.Type.Simple;
                f.close.image.preserveAspect = true;
                f.close.image.color = Color.white;
                var label = f.close.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = "";
                f.close.name = "Close";
            }
        }

        private static readonly List<UnityEngine.UI.Button> _pending = new List<UnityEngine.UI.Button>();

        /// <summary>
        /// Phủ skin NGAY TRƯỚC KHI canvas vẽ khung hình này (sau khi code đã Place / Height / bố cục xong).
        /// UIKit.Button gọi hàm này: lúc vừa tạo, nút còn cỡ mẫu 100×100 → trước đây bị coi là "khung bấm lớn" và bỏ qua
        /// → nút Học / Nâng, lựa chọn hội thoại NPC... còn màu xanh đậm kiểu cũ.
        /// </summary>
        public static void SkinButtonLater(UnityEngine.UI.Button b)
        {
            if (!Enabled || b == null) return;
            if (_pending.Count == 0) Canvas.willRenderCanvases += FlushPending;
            _pending.Add(b);
        }

        private static void FlushPending()
        {
            Canvas.willRenderCanvases -= FlushPending;
            var list = _pending.ToArray();
            _pending.Clear();
            foreach (var b in list) if (b != null) SkinButton(b);
        }

        public static void SkinButton(UnityEngine.UI.Button b)
        {
            if (!Enabled || b == null || b.image == null || !IsFlat(b.image)) return;
            var frame = b.GetComponentInParent<WindowFrame>();
            if (frame != null && frame.close == b) return;          // nút đóng: SkinWindow lo
            // Chiều cao thật: ô Height() (LayoutElement) nếu có, không thì cỡ đang đặt
            float h = b.TryGetComponent(out LayoutElement le) && le.preferredHeight > 0 ? le.preferredHeight : ((RectTransform)b.transform).rect.height;
            if (h > 70f) return;   // "nút" cỡ lớn = khung bấm được (bảng nhiệm vụ HUD...) → giữ nguyên
            Set(b.image, Get(Button), keepColor: true);
            // Màu nướng sẵn trong prefab (màu nút cũ, xám tối) → màu nút mới. Màu riêng (đỏ đồ sát...) giữ nguyên.
            if (Near(b.image.color, _oldButton) || IsNeutralDark(b.image.color)) b.image.color = UIKit.ButtonColor;
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && !b.TryGetComponent(out LabelContrast _))
                b.gameObject.AddComponent<LabelContrast>().Init(b.image, label);
        }

        /// <summary>Nút có chữ nằm NGOÀI nền (nút icon HUD): bỏ tự đổi màu chữ theo nền.</summary>
        public static void KeepLabelColor(UnityEngine.UI.Button b)
        {
            if (b != null && b.TryGetComponent(out LabelContrast lc)) Object.Destroy(lc);
        }

        /// <summary>
        /// Chữ trên nút đổi theo ĐỘ SÁNG nền nút: nền kem / vàng → chữ nâu, nền tối (ô túi, nút đỏ...) → chữ trắng.
        /// Kiểm mỗi khung hình nhưng chỉ làm khi màu nền đổi (code cũ đổi image.color lúc chọn / bỏ chọn).
        /// Chữ có màu riêng (vàng, xanh...) thì không đụng.
        /// </summary>
        private sealed class LabelContrast : MonoBehaviour
        {
            private Image _img;
            private TextMeshProUGUI _label;
            private Color _last = new Color(-1, -1, -1, -1);

            public void Init(Image img, TextMeshProUGUI label) { _img = img; _label = label; LateUpdate(); }

            private void LateUpdate()
            {
                if (_img == null || _label == null || _img.color == _last) return;
                _last = _img.color;
                if (!IsWhite(_label.color) && !Near(_label.color, ButtonText)) return;   // màu riêng
                float lum = 0.3f * _last.r + 0.59f * _last.g + 0.11f * _last.b;
                _label.color = lum > 0.55f ? ButtonText : UIKit.TextColor;
            }
        }

        public static void SkinInput(TMP_InputField input)
        {
            if (!Enabled || input == null || !input.TryGetComponent(out Image img) || !IsFlat(img)) return;
            Set(img, Get(Input));
            img.color = Color.white;
        }

        /// <summary>Khung màu phẳng có màu ô / khung phụ của theme cũ → hình bo góc + màu mới (hàng danh sách, ô đồ...).</summary>
        public static void SkinPanel(Image img)
        {
            if (!Enabled || img == null || !IsFlat(img) || img.GetComponent<Selectable>() != null) return;
            if (Near(img.color, _oldSlot) || Near(img.color, UIKit.SlotColor))
            {
                Set(img, Get(Slot), keepColor: true);
                img.color = UIKit.SlotColor;
            }
        }

        // ------------------------------------------------------------------ tiện ích
        private static void Set(Image img, Sprite s, bool keepColor = true)
        {
            if (img == null || s == null) return;
            img.sprite = s;
            img.type = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = BorderScale;
            if (!keepColor) img.color = Color.white;
        }

        /// <summary>Image chưa có hình riêng: không sprite, hoặc hình trắng / mặc định của Unity / hình skin vừa gán.</summary>
        private static bool IsFlat(Image img)
        {
            var s = img.sprite;
            if (s == null) return true;
            string n = s.name;
            return n == "White" || n == "UISprite" || n == "Background" || n == "InputFieldBackground" || n == "UIMask"
                   || s == UIKit.WhiteSprite;
        }

        private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        private static bool IsWhite(Color c) => c.r > 0.85f && c.g > 0.85f && c.b > 0.85f;
        /// <summary>Xám tối ít màu (màu nút / khung mặc định cũ).</summary>
        private static bool IsNeutralDark(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max < 0.45f && max - min < 0.15f;
        }
    }
}
