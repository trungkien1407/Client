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

        private static Sprite _arrow;

        /// <summary>Mũi tên chỉ sang PHẢI (thân + đầu nhọn) — cổng chuyển map; xoay transform để đổi hướng. 64 px cho nét.</summary>
        public static Sprite Arrow => _arrow != null ? _arrow : _arrow = Make((x, y) =>
        {
            float head = (0.92f - x) * 0.95f - Mathf.Abs(y);             // tam giác đầu: hẹp dần tới mũi x = 0,92
            head = Mathf.Min(head, x + 0.05f);                           // đáy tam giác ở x ≈ -0,05
            float stem = Mathf.Min(0.26f - Mathf.Abs(y), Mathf.Min(x + 0.9f, 0.2f - x));   // thân chữ nhật
            return Mathf.Clamp01(Mathf.Max(head, stem) * 14f);           // mép hơi mềm
        }, 64);

        /// <summary>Dựng texture size×size: {alpha}(x, y) với x, y ∈ [-1, 1] → độ đậm 0..1. Luôn 1 đơn vị vuông.</summary>
        private static Sprite Make(System.Func<float, float, float> alpha, int size = Size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                    px[j * size + i] = new Color(1f, 1f, 1f, alpha(x, y));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * PixelsPerUnit / Size);
        }
    }
}
