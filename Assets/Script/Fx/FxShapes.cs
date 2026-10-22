using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// HÌNH VẼ BẰNG CODE cho hiệu ứng không cần art: chấm mờ (tâm sáng, mép tan dần) và ngôi sao 5 cánh.
    /// Tạo 1 lần rồi dùng chung (static). Màu thật do SpriteRenderer.color quyết định (hình gốc màu trắng).
    /// </summary>
    public static class FxShapes
    {
        private const int Size = 32;
        private const float PixelsPerUnit = 32f;   // 32 px = 1 đơn vị → scale 1 = rộng 1 ô
        private static Sprite _dot, _star;

        /// <summary>Chấm tròn mờ dần ra mép (vòng sáng dưới chân, tia lửa).</summary>
        public static Sprite Dot => _dot != null ? _dot : _dot = Make((x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);            // 0 ở tâm, 1 ở mép
            return Mathf.Pow(Mathf.Clamp01(1f - d), 0.8f);   // tan dần nhưng giữa vẫn đậm (vòng dưới chân dễ thấy)
        });

        /// <summary>Sao 5 cánh (choáng: sao xoay trên đầu).</summary>
        public static Sprite Star => _star != null ? _star : _star = Make((x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x) - Mathf.PI / 2f;      // góc tính từ hướng lên → 1 cánh chĩa lên
            // Bán kính viền sao dao động giữa 0,45 (khe) và 1 (đỉnh cánh) theo góc, 5 lần mỗi vòng
            float edge = Mathf.Lerp(0.45f, 1f, Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f));
            return Mathf.Clamp01((edge - r) * 8f);            // mép hơi mềm
        });

        /// <summary>Dựng texture Size×Size: {alpha}(x, y) với x, y ∈ [-1, 1] → độ đậm 0..1.</summary>
        private static Sprite Make(System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[Size * Size];
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    px[j * Size + i] = new Color(1f, 1f, 1f, alpha(x, y));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
    }
}
