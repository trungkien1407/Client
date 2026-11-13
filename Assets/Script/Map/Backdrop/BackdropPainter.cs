using UnityEngine;

namespace Assets.Script.Map.Backdrop
{
    /// <summary>
    /// VẼ ẢNH NỀN BẰNG CODE (chưa có art riêng). Mọi ảnh dải ngang đều LẶP LIỀN MẠCH: mép phải nối khít mép trái
    /// → ghép bao nhiêu ô cũng không thấy đường nối. Bí quyết: đường núi = tổng các sóng sin có SỐ CHU KỲ NGUYÊN
    /// trên bề ngang ảnh (sin(2π·k·x/W)), nên giá trị ở x = 0 và x = W bằng nhau.
    /// Ảnh vẽ 1 lần mỗi chủ đề rồi giữ lại (ParallaxBackdrop cache), nên tốn vài chục ms lúc vào map đầu tiên.
    /// </summary>
    public static class BackdropPainter
    {
        public const int PixelsPerUnit = 32;

        // ---------------------------------------------------------------- tiện ích
        private static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            return t;
        }

        private static Color32 Blend(Color32 under, Color over)
        {
            float a = over.a, ia = 1f - a;
            float ua = under.a / 255f;
            float outA = a + ua * ia;
            if (outA <= 0.0001f) return new Color32(0, 0, 0, 0);
            float r = (over.r * a + under.r / 255f * ua * ia) / outA;
            float g = (over.g * a + under.g / 255f * ua * ia) / outA;
            float b = (over.b * a + under.b / 255f * ua * ia) / outA;
            return new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(outA * 255));
        }

        private static void Put(Color32[] px, int w, int h, int x, int y, Color c)
        {
            if (y < 0 || y >= h || c.a <= 0f) return;
            x = ((x % w) + w) % w;   // vẽ tràn mép thì quấn sang mép kia → vẫn liền mạch
            int i = y * w + x;
            px[i] = Blend(px[i], c);
        }

        /// <summary>Đường cong lặp liền mạch trong [0, 1]: tổng sin số chu kỳ nguyên, biên độ giảm dần theo tần số.</summary>
        private static float[] Ridge(int w, int seed, int kMin, int kMax, float rough)
        {
            var rng = new System.Random(seed);
            int n = kMax - kMin + 1;
            var phase = new float[n];
            var amp = new float[n];
            for (int i = 0; i < n; i++) { phase[i] = (float)(rng.NextDouble() * Mathf.PI * 2); amp[i] = Mathf.Pow(kMin + i, -rough) * (0.6f + (float)rng.NextDouble() * 0.8f); }
            var v = new float[w];
            float lo = float.MaxValue, hi = float.MinValue;
            for (int x = 0; x < w; x++)
            {
                float s = 0;
                for (int i = 0; i < n; i++) s += amp[i] * Mathf.Sin(2 * Mathf.PI * (kMin + i) * x / w + phase[i]);
                v[x] = s; lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s);
            }
            for (int x = 0; x < w; x++) v[x] = (v[x] - lo) / Mathf.Max(0.0001f, hi - lo);
            return v;
        }

        // ---------------------------------------------------------------- các lớp
        /// <summary>Trời: dải dọc chuyển màu (trên → chân trời). Kéo giãn phủ cả khung nhìn.</summary>
        public static Texture2D Sky(BackdropPalette p)
        {
            const int w = 4, h = 256;
            var t = NewTex(w, h); t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float k = y / (float)(h - 1);
                Color c = Color.Lerp(p.skyHorizon, p.skyTop, Mathf.SmoothStep(0, 1, k));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>Mặt trời / trăng: đĩa sáng + quầng mờ.</summary>
        public static Texture2D Sun(Color c)
        {
            const int s = 128;
            var t = NewTex(s, s); t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    float a = d < 0.32f ? 1f : Mathf.Pow(Mathf.Clamp01(1 - (d - 0.32f) / 0.68f), 2.2f) * 0.55f;
                    px[y * s + x] = new Color(c.r, c.g, c.b, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>
        /// Dải núi / đồi lặp liền mạch. haze = pha về màu chân trời bao nhiêu (xa → nhiều).
        /// Chân núi nhạt dần vào sương, mép đỉnh sáng hơn 1 chút (như nắng chiếu) → có khối, không phẳng.
        /// </summary>
        public static Texture2D Ridge(int w, int h, int seed, Color baseColor, Color horizon, float haze,
                                      float minH, float maxH, int kMin, int kMax, float rough, bool snowCaps, bool flatTops, bool fromTop = false)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            var r = Ridge(w, seed, kMin, kMax, rough);
            Color body = Color.Lerp(baseColor, horizon, haze);
            Color rim = Color.Lerp(body, Color.white, 0.18f);
            Color foot = Color.Lerp(body, horizon, 0.45f);
            for (int x = 0; x < w; x++)
            {
                float v = r[x];
                if (flatTops) v = Mathf.Min(v, 0.72f) / 0.72f;   // núi đỉnh bằng (mesa) cho vùng đá
                int top = Mathf.RoundToInt(Mathf.Lerp(minH, maxH, v) * h);
                for (int y = 0; y < top; y++)
                {
                    float k = y / (float)Mathf.Max(1, top);               // 0 chân → 1 đỉnh
                    Color c = Color.Lerp(foot, body, Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.6f)));
                    if (top - y <= 2) c = rim;                            // viền sáng ở đỉnh
                    if (snowCaps && v > 0.55f && top - y < (v - 0.45f) * 0.35f * h) c = Color.Lerp(c, Color.white, 0.85f);
                    int yy = fromTop ? h - 1 - y : y;
                    px[yy * w + x] = c;
                }
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>Mây: các cụm hình bầu dục mờ mép, lặp liền mạch.</summary>
        public static Texture2D Clouds(int w, int h, int seed, Color tint)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                float cx = (float)rng.NextDouble() * w, cy = h * (0.35f + (float)rng.NextDouble() * 0.45f);
                int puffs = 4 + rng.Next(4);
                float len = 60 + (float)rng.NextDouble() * 90;
                for (int j = 0; j < puffs; j++)
                {
                    float px0 = cx + (j - puffs / 2f) * len / puffs, py0 = cy + (float)(rng.NextDouble() - 0.5) * 10;
                    float rx = 22 + (float)rng.NextDouble() * 22, ry = rx * 0.55f;
                    for (int y = (int)(py0 - ry); y <= py0 + ry; y++)
                        for (int x = (int)(px0 - rx); x <= px0 + rx; x++)
                        {
                            float d = Mathf.Sqrt(Mathf.Pow((x - px0) / rx, 2) + Mathf.Pow((y - py0) / ry, 2));
                            if (d > 1) continue;
                            Put(px, w, h, x, y, new Color(tint.r, tint.g, tint.b, tint.a * Mathf.SmoothStep(0, 1, 1 - d) * 0.55f));
                        }
                }
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>Dải sương: mờ ở giữa, tan dần lên trên / xuống dưới.</summary>
        public static Texture2D Fog(Color c)
        {
            const int w = 4, h = 128;
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float k = y / (float)(h - 1);
                float a = c.a * Mathf.Pow(Mathf.Sin(k * Mathf.PI), 1.5f) * (k < 0.5f ? 1f : 0.8f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(c.r, c.g, c.b, a);
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>Lớp gần: hàng cây / trúc / đá / thông / cây khô / thạch nhũ / cột gỗ — bóng 1 màu có viền sáng trên.</summary>
        public static Texture2D Near(NearKind kind, int w, int h, int seed, Color color, Color horizon, float haze)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            Color body = Color.Lerp(color, horizon, haze);
            Color light = Color.Lerp(body, Color.white, 0.15f);
            Color dark = Color.Lerp(body, Color.black, 0.18f);

            void Disc(float cx, float cy, float r, Color c)
            {
                for (int y = (int)(cy - r); y <= cy + r; y++)
                    for (int x = (int)(cx - r); x <= cx + r; x++)
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                            Put(px, w, h, x, y, (y - cy) > r * 0.45f ? light : c);
            }
            void Rect(float x0, float y0, float x1, float y1, Color c)
            {
                for (int y = (int)y0; y < y1; y++) for (int x = (int)x0; x < x1; x++) Put(px, w, h, x, y, c);
            }
            void Tri(float cx, float y0, float halfW, float hgt, Color c, Color tip)
            {
                for (int y = 0; y < hgt; y++)
                {
                    float hw = halfW * (1 - y / hgt);
                    for (int x = (int)(cx - hw); x <= cx + hw; x++) Put(px, w, h, x, (int)(y0 + y), y > hgt * 0.7f ? tip : c);
                }
            }

            // nền đất mấp mô dưới chân cho liền khối
            var ground = Ridge(w, seed + 7, 2, 6, 1.2f);
            for (int x = 0; x < w; x++) { int top = (int)(h * (0.06f + ground[x] * 0.08f)); for (int y = 0; y < top; y++) Put(px, w, h, x, y, dark); }

            float step = kind switch { NearKind.Bamboo => 46, NearKind.Pillars => 170, NearKind.Rocks => 90, NearKind.Bushes => 60, _ => 80 };
            for (float bx = 0; bx < w; bx += step * (0.7f + (float)rng.NextDouble() * 0.6f))
            {
                float s = 0.7f + (float)rng.NextDouble() * 0.6f;   // to nhỏ ngẫu nhiên
                switch (kind)
                {
                    case NearKind.RoundTrees:
                        Rect(bx - 4 * s, 0, bx + 4 * s, h * 0.35f * s, dark);
                        Disc(bx, h * 0.45f * s, 30 * s, body); Disc(bx - 18 * s, h * 0.36f * s, 22 * s, body); Disc(bx + 18 * s, h * 0.37f * s, 22 * s, body);
                        break;
                    case NearKind.Bushes:
                        Disc(bx, h * 0.12f, 22 * s, body); Disc(bx + 16 * s, h * 0.1f, 16 * s, body);
                        if (rng.NextDouble() < 0.6) Disc(bx + 6, h * 0.12f + 18 * s, 3, new Color(1f, 0.9f, 0.4f, 0.9f));   // hoa
                        break;
                    case NearKind.Bamboo:
                        {
                            float th = 4 * s, top = h * (0.55f + (float)rng.NextDouble() * 0.4f);
                            Rect(bx - th, 0, bx + th, top, body);
                            for (float ny = 30; ny < top; ny += 34 * s) Rect(bx - th - 1, ny, bx + th + 1, ny + 2, dark);   // đốt trúc
                            Rect(bx + th - 2, 0, bx + th, top, light);
                            if (rng.NextDouble() < 0.7) Tri(bx + 14, top - 30, 14, 10, light, light);   // lá
                            break;
                        }
                    case NearKind.Rocks:
                        Disc(bx, h * 0.12f, 34 * s, body); Disc(bx + 30 * s, h * 0.08f, 22 * s, body);
                        break;
                    case NearKind.DeadTrees:
                        {
                            float top = h * (0.5f + (float)rng.NextDouble() * 0.4f);
                            Rect(bx - 4 * s, 0, bx + 4 * s, top, body);
                            for (int b = 0; b < 3; b++)
                            {
                                float by = top * (0.45f + b * 0.17f), dir = b % 2 == 0 ? 1 : -1;
                                for (int i = 0; i < 26 * s; i++) Rect(bx + dir * i, by + i * 0.6f, bx + dir * i + 3, by + i * 0.6f + 3, body);
                            }
                            break;
                        }
                    case NearKind.Pines:
                        {
                            float hh = h * (0.42f + (float)rng.NextDouble() * 0.33f);
                            Rect(bx - 3 * s, 0, bx + 3 * s, hh * 0.25f, dark);
                            for (int tier = 0; tier < 3; tier++)
                                Tri(bx, hh * (0.18f + tier * 0.22f), 24 * s * (1 - tier * 0.22f), hh * 0.38f, body, Color.Lerp(body, Color.white, 0.75f));
                            break;
                        }
                    case NearKind.Stalagmites:
                        Tri(bx, 0, 18 * s, h * (0.25f + (float)rng.NextDouble() * 0.35f), body, light);
                        break;
                    case NearKind.Pillars:
                        Rect(bx - 16, 0, bx + 16, h, body);
                        Rect(bx + 9, 0, bx + 16, h, light);
                        Rect(bx - 24, h * 0.9f, bx + 24, h * 0.96f, dark);          // xà ngang
                        Disc(bx + 60, h * 0.7f, 12, new Color(1f, 0.55f, 0.25f, 0.95f)); // đèn lồng
                        break;
                }
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>Tường trong nhà: gỗ sẫm + ô cửa sổ sáng có song gỗ, lặp liền mạch.</summary>
        public static Texture2D IndoorWall(int w, int h, Color wall, Color glow)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float plank = (x / 24) % 2 == 0 ? 0.04f : -0.03f;
                    px[y * w + x] = Color.Lerp(wall, Color.black, 0.15f + plank);
                }
            for (int wx = 64; wx < w; wx += 256)
                for (int y = (int)(h * 0.45f); y < h * 0.85f; y++)
                    for (int x = wx; x < wx + 96; x++)
                    {
                        bool bar = (x - wx) % 32 < 3 || (y - (int)(h * 0.45f)) % 30 < 3;
                        px[y * w + x] = bar ? (Color32)Color.Lerp(wall, Color.black, 0.35f) : (Color32)glow;
                    }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        /// <summary>Ảnh → Sprite lặp được (FullRect để SpriteRenderer vẽ kiểu Tiled), gốc ở giữa đáy.</summary>
        public static Sprite ToSprite(Texture2D t, float pivotY = 0f) =>
            Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, pivotY), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }
}
