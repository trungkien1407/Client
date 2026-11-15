using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// ICON VẼ BẰNG CODE (2026-11-15): mũi tên, dấu tích, dấu ✕, người, ổ khoá, con mắt, nút chơi, khiên xác thực…
    /// Không cần file ảnh (không dính hình rip) — mỗi icon là 1 "hàm khoảng cách" (SDF): với mỗi điểm ảnh tính khoảng cách tới hình,
    /// âm = nằm trong hình. Độ trong suốt = clamp(0.5 − khoảng cách) → mép mịn 1 px. Icon màu trắng, tô màu bằng Image.color.
    /// Tạo 1 lần rồi giữ trong bộ nhớ (cache theo tên).
    /// </summary>
    public static class UIIcons
    {
        private const int S = 64;   // cỡ ảnh 64×64, toạ độ 0..64, gốc dưới-trái
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite ChevronUp => Get("chevron", p => Seg2(p, new Vector2(16, 24), new Vector2(32, 40), new Vector2(48, 24)) - 5f);
        public static Sprite Check => Get("check", p => Seg2(p, new Vector2(15, 33), new Vector2(27, 21), new Vector2(49, 45)) - 5.5f);
        public static Sprite Close => Get("close", p => Mathf.Min(Seg(p, new Vector2(18, 18), new Vector2(46, 46)),
                                                                  Seg(p, new Vector2(18, 46), new Vector2(46, 18))) - 5f);
        public static Sprite User => Get("user", p => Mathf.Min(Circle(p, new Vector2(32, 43), 11f),
                                                                RoundBox(p, new Vector2(32, 13), new Vector2(19, 11), 10f)));
        public static Sprite Lock => Get("lock", p =>
        {
            float body = RoundBox(p, new Vector2(32, 22), new Vector2(18, 14), 4f);
            float shackle = p.y >= 38 ? Mathf.Abs(Vector2.Distance(p, new Vector2(32, 38)) - 11f) - 3.5f
                                      : Mathf.Min(Mathf.Abs(p.x - 21f), Mathf.Abs(p.x - 43f)) - 3.5f + Mathf.Max(0, 30f - p.y);
            float hole = Circle(p, new Vector2(32, 23), 4f);
            return Mathf.Max(Mathf.Min(body, shackle), -hole);   // khoét lỗ khoá
        });
        public static Sprite Eye => Get("eye", p => Mathf.Min(Mathf.Abs(Ellipse(p, new Vector2(32, 32), 25f, 14f)) - 3f,
                                                              Circle(p, new Vector2(32, 32), 7f)));
        public static Sprite EyeOff => Get("eyeoff", p => Mathf.Min(Mathf.Min(Mathf.Abs(Ellipse(p, new Vector2(32, 32), 25f, 14f)) - 3f,
                                                                              Circle(p, new Vector2(32, 32), 7f)),
                                                                    Seg(p, new Vector2(12, 12), new Vector2(52, 52)) - 3.5f));
        /// <summary>Tam giác "chơi ngay" bo góc.</summary>
        public static Sprite Play => Get("play", p => Triangle(p, new Vector2(22, 14), new Vector2(22, 50), new Vector2(50, 32)) - 3f);
        /// <summary>Khiên có dấu tích (xác thực tài khoản).</summary>
        public static Sprite Shield => Get("shield", p =>
        {
            // khiên = hộp bo góc trên + mũi nhọn dưới
            float top = RoundBox(p, new Vector2(32, 40), new Vector2(18, 14), 5f);
            float tip = Triangle(p, new Vector2(14, 34), new Vector2(50, 34), new Vector2(32, 6)) - 2f;
            float shield = Mathf.Min(top, tip);
            float tick = Seg2(p, new Vector2(22, 34), new Vector2(29, 27), new Vector2(42, 41)) - 3.5f;
            return Mathf.Max(shield, -tick);   // khoét dấu tích
        });
        public static Sprite Back => Get("back", p => Mathf.Min(Seg2(p, new Vector2(34, 16), new Vector2(18, 32), new Vector2(34, 48)) - 5f,
                                                                Seg(p, new Vector2(20, 32), new Vector2(48, 32)) - 5f));

        /// <summary>Thêm 1 icon (Image con) vào {parent}, đặt giữa, cỡ {size}, màu {color}. Không nhận chuột.</summary>
        public static Image Add(Transform parent, Sprite icon, float size, Color color, Vector2 offset = default)
        {
            var rt = UIKit.Rect("Icon", parent);
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            rt.gameObject.AddComponent<NoSkin>();   // UISkin không phủ hình kho lên
            return img;
        }

        // ------------------------------------------------------------------ vẽ

        private static Sprite Get(string name, Func<Vector2, float> sdf)
        {
            if (Cache.TryGetValue(name, out var s) && s != null) return s;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = sdf(new Vector2(x + 0.5f, y + 0.5f));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            s.name = "UIIcon_" + name;
            Cache[name] = s;
            return s;
        }

        /// <summary>Khoảng cách tới đoạn thẳng ab.</summary>
        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Đường gấp khúc a → b → c.</summary>
        private static float Seg2(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Mathf.Min(Seg(p, a, b), Seg(p, b, c));

        private static float Circle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;

        /// <summary>Hộp bo góc: tâm c, nửa cỡ h, bán kính góc r.</summary>
        private static float RoundBox(Vector2 p, Vector2 c, Vector2 h, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h + new Vector2(r, r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
        }

        /// <summary>Elip (gần đúng — đủ cho icon).</summary>
        private static float Ellipse(Vector2 p, Vector2 c, float a, float b)
        {
            Vector2 d = p - c;
            float k = new Vector2(d.x / a, d.y / b).magnitude;
            return (k - 1f) * Mathf.Min(a, b);
        }

        /// <summary>Tam giác abc (dương ngoài, âm trong).</summary>
        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 g = (a + b + c) / 3f;   // trọng tâm: dùng để chọn dấu (vẽ đỉnh theo chiều nào cũng đúng)
            float inside = Mathf.Max(Side(p, a, b, g), Mathf.Max(Side(p, b, c, g), Side(p, c, a, g)));
            if (inside <= 0) return inside;
            return Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
        }

        /// <summary>Khoảng cách có dấu tới đường ab: âm = cùng phía với trọng tâm g (phía trong tam giác).</summary>
        private static float Side(Vector2 p, Vector2 a, Vector2 b, Vector2 g)
        {
            Vector2 e = b - a, n = new Vector2(e.y, -e.x).normalized;
            if (Vector2.Dot(g - a, n) > 0) n = -n;
            return Vector2.Dot(p - a, n);
        }
    }
}
