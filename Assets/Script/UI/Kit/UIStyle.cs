using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// KIỂU DÁNG DÙNG CHUNG không cần hình kho (vẽ bằng code lúc chạy → bản public không có art vẫn đẹp):
    ///   Round / Ring   hình chữ nhật bo góc (nền) và viền bo góc (khung) — 9-slice, co giãn cỡ nào cũng giữ góc tròn.
    ///   Readable(t)    chữ trắng có VIỀN ĐEN + BÓNG ĐỔ → đọc được trên mọi nền (trời trắng, tuyết, mây...).
    ///   Chip(btn)      nút HUD kiểu "viên thuốc": nền tối trong suốt + viền vàng mảnh + chữ trắng (nút Khu, Auto, Cưỡi).
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color ChipBg = new Color(0.07f, 0.09f, 0.15f, 0.80f);
        public static readonly Color ChipBorder = new Color(1f, 0.82f, 0.40f, 0.85f);
        public static readonly Color ChipOn = new Color(0.16f, 0.55f, 0.30f, 0.92f);   // nút đang bật (Auto: Bật, đang cưỡi)
        public static readonly Color Gold = new Color(1f, 0.84f, 0.42f);

        private static Sprite _round, _ring;

        /// <summary>Nền bo góc (trắng — nhân màu bằng Image.color).</summary>
        public static Sprite Round => _round != null ? _round : _round = MakeRounded(false);

        /// <summary>Viền bo góc dày ~2 px (ruột trong suốt) — đặt đè lên nền để có khung.</summary>
        public static Sprite Ring => _ring != null ? _ring : _ring = MakeRounded(true);

        /// <summary>
        /// Vẽ hình 48×48 bo góc bán kính 14 (mép mịn), cắt 9 mảnh viền 16 px. ring = chỉ vẽ dải viền dày 3 px.
        /// Khoảng cách tới mép bo tính bằng hình học: điểm ở góc → đo tới tâm cung tròn; ở cạnh → đo tới cạnh.
        /// </summary>
        private static Sprite MakeRounded(bool ring)
        {
            const int size = 48, radius = 14, border = 16;
            const float thick = 3f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // khoảng cách từ tâm điểm ảnh vào trong hình (dương = trong, âm = ngoài)
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, radius, size - radius), cy = Mathf.Clamp(fy, radius, size - radius);
                    float inside = radius - Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(inside + 0.5f);                          // mép ngoài mịn 1 px
                    if (ring) a *= Mathf.Clamp01(thick - inside + 0.5f);             // chỉ giữ dải viền
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            s.name = ring ? "UIStyleRing" : "UIStyleRound";   // tên khác "White" → UISkin không phủ hình kho lên
            return s;
        }

        /// <summary>Đặt hình bo góc cho 1 Image (9-slice). scale &gt; 1 = góc nhỏ lại (nút thấp).</summary>
        public static Image SetRound(Image img, Color color, float cornerScale = 1f, bool ring = false)
        {
            img.sprite = ring ? Ring : Round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale;
            img.color = color;
            return img;
        }

        /// <summary>Thêm khung viền bo góc phủ kín {parent} (không nhận chuột).</summary>
        public static Image AddRing(Transform parent, Color color, float cornerScale = 1f)
        {
            var old = parent.Find("Ring");
            var img = old != null ? old.GetComponent<Image>() : UIKit.Rect("Ring", parent).Fill().gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            return SetRound(img, color, cornerScale, ring: true);
        }

        // ------------------------------------------------------------------ chữ dễ đọc
        private static readonly Dictionary<Material, Material> ReadableMats = new Dictionary<Material, Material>();

        /// <summary>
        /// Chữ có VIỀN ĐEN + BÓNG ĐỔ (outline + underlay của shader TextMeshPro SDF). Dùng chung 1 vật liệu cho mọi chữ cùng font
        /// (không tạo vật liệu riêng mỗi chữ). Chữ trắng / màu sáng đứng trên nền game (tên NPC, nhãn nút menu, tên mục tiêu...)
        /// trước chỉ có viền mỏng 0,2 → đứng trước trời / tuyết trắng gần như mất chữ.
        /// </summary>
        public static T Readable<T>(T t, float outline = 0.22f) where T : TMP_Text
        {
            if (t == null || t.font == null) return t;
            var src = t.font.material;
            if (src == null) return t;
            if (!ReadableMats.TryGetValue(src, out var m) || m == null)
            {
                ShaderUtilities.GetShaderPropertyIDs();
                m = new Material(src) { name = src.name + " (Readable)" };
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.95f));
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
                ReadableMats[src] = m;
            }
            t.fontSharedMaterial = m;
            return t;
        }

        // ------------------------------------------------------------------ nút HUD kiểu viên thuốc
        /// <summary>
        /// Biến nút thành "chip" HUD: nền tối trong suốt bo góc + viền vàng mảnh + chữ trắng có viền.
        /// Dùng sau khi nút đã Place xong. Gọi lại được (vd đổi màu khi bật / tắt: SetChipOn).
        /// </summary>
        public static Button Chip(Button b, float fontSize = 15)
        {
            if (b == null) return b;
            UISkin.KeepLabelColor(b);                // bỏ tự đổi màu chữ theo nền (nền chip luôn tối)
            SetRound(b.image, ChipBg, 1.4f);
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;           // không giữ màu "đang chọn" sau khi bấm
            b.colors = cb;
            AddRing(b.transform, ChipBorder, 1.4f);
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = Color.white;
                label.fontSize = fontSize;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.richText = true;
                Readable(label, 0.18f);
            }
            return b;
        }

        /// <summary>Chip đang bật → nền xanh lá; tắt → nền tối.</summary>
        public static void SetChipOn(Button b, bool on)
        {
            if (b != null && b.image != null) b.image.color = on ? ChipOn : ChipBg;
        }
    }
}
