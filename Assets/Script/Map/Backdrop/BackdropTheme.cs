using UnityEngine;

namespace Assets.Script.Map.Backdrop
{
    /// <summary>Chủ đề nền của map. Auto = chọn theo số map (BackdropThemes.ForMap). Đặt ở MapInfo.backdrop để chọn tay.</summary>
    public enum BackdropTheme
    {
        Auto = 0,
        Village,      // làng: trời xanh, đồi xanh, cây tán tròn
        Meadow,       // đồi hoa: trời trong, đồi thoải, bụi hoa
        Bamboo,       // rừng trúc: trời xanh ngọc, núi mờ sương, thân trúc
        Rock,         // vùng đá: trời chiều cam, núi đỉnh bằng, tảng đá
        Swamp,        // đầm lầy: trời xám xanh, cây khô, sương dày
        Snow,         // tuyết: trời nhạt, núi phủ tuyết, thông tuyết
        Cave,         // hang: vách đá tối, thạch nhũ
        Arena,        // lôi đài: trời hoàng hôn, núi xa
        Indoor,       // trong nhà (trường): tường, cửa sổ sáng, cột gỗ
    }

    /// <summary>Bảng màu 1 chủ đề. Màu xa được pha dần về màu chân trời (horizon) để tạo cảm giác xa — "phối cảnh khí quyển".</summary>
    public class BackdropPalette
    {
        public Color skyTop, skyHorizon;   // trời: trên → chân trời
        public Color far, mid, near;       // núi xa · đồi giữa · hàng cây / đá gần (màu gốc, chưa pha sương)
        public Color fog;                  // màu sương (alpha = độ dày)
        public bool clouds = true, snowCaps, flatTops, sun;
        public NearKind nearKind = NearKind.RoundTrees;
    }

    /// <summary>Kiểu hình ở lớp gần nhất.</summary>
    public enum NearKind { RoundTrees, Bushes, Bamboo, Rocks, DeadTrees, Pines, Stalagmites, Pillars }

    public static class BackdropThemes
    {
        /// <summary>Số map → chủ đề (khớp NOI_DUNG.md §1: 3 làng, 3 trường, 5 bãi luyện, 2 phó bản, Lôi đài).</summary>
        public static BackdropTheme ForMap(int mapId) => mapId switch
        {
            1 => BackdropTheme.Village,          // Làng Lá
            2 => BackdropTheme.Meadow,           // Đồi Hoa Cúc
            3 => BackdropTheme.Cave,             // Hang Ốc Sên (phó bản)
            4 => BackdropTheme.Arena,            // Lôi Đài
            5 => BackdropTheme.Bamboo,           // Rừng Trúc
            6 => BackdropTheme.Rock,             // Làng Đá
            7 => BackdropTheme.Rock,             // Thung Lũng Đá
            8 => BackdropTheme.Swamp,            // Đầm Lầy Sương Mù
            9 => BackdropTheme.Cave,             // Động Xà Vương (phó bản)
            10 => BackdropTheme.Snow,            // Làng Tuyết
            11 => BackdropTheme.Snow,            // Núi Tuyết
            12 or 13 or 14 => BackdropTheme.Indoor,   // 3 trường
            _ => BackdropTheme.Village,
        };

        private static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static BackdropPalette Get(BackdropTheme t) => t switch
        {
            BackdropTheme.Meadow => new BackdropPalette { skyTop = C("#4f9be8"), skyHorizon = C("#cfeaff"), far = C("#6f9fc4"), mid = C("#79c26a"), near = C("#3f8f45"), fog = C("#e8f6ff40"), sun = true, nearKind = NearKind.Bushes },
            BackdropTheme.Bamboo => new BackdropPalette { skyTop = C("#5fb6c9"), skyHorizon = C("#dff3ea"), far = C("#6e9e93"), mid = C("#4f8a62"), near = C("#2f6b3c"), fog = C("#e6f5ee80"), nearKind = NearKind.Bamboo },
            BackdropTheme.Rock => new BackdropPalette { skyTop = C("#e58b4f"), skyHorizon = C("#ffd9a0"), far = C("#b07a5a"), mid = C("#9a6444"), near = C("#6e4630"), fog = C("#ffe2b850"), flatTops = true, sun = true, nearKind = NearKind.Rocks },
            BackdropTheme.Swamp => new BackdropPalette { skyTop = C("#5d6f6a"), skyHorizon = C("#b9c7b8"), far = C("#6c7f74"), mid = C("#4d5f4f"), near = C("#2c3a2e"), fog = C("#c9d6c8a0"), clouds = false, nearKind = NearKind.DeadTrees },
            BackdropTheme.Snow => new BackdropPalette { skyTop = C("#7fb2e0"), skyHorizon = C("#eef6ff"), far = C("#9fb7d6"), mid = C("#7f9bbf"), near = C("#3f5f6f"), fog = C("#ffffff60"), snowCaps = true, nearKind = NearKind.Pines },
            BackdropTheme.Cave => new BackdropPalette { skyTop = C("#120f1a"), skyHorizon = C("#2e2638"), far = C("#3a3046"), mid = C("#2b2335"), near = C("#1a1522"), fog = C("#6a5a8030"), clouds = false, nearKind = NearKind.Stalagmites },
            BackdropTheme.Arena => new BackdropPalette { skyTop = C("#6b4fa0"), skyHorizon = C("#ffb27a"), far = C("#8a6a9a"), mid = C("#6a4f7a"), near = C("#3f2f4f"), fog = C("#ffc49a40"), sun = true, nearKind = NearKind.Rocks },
            BackdropTheme.Indoor => new BackdropPalette { skyTop = C("#6b4a32"), skyHorizon = C("#c79a6a"), far = C("#a9794f"), mid = C("#8a5f3c"), near = C("#5a3a22"), fog = C("#ffd9a020"), clouds = false, nearKind = NearKind.Pillars },
            _ => new BackdropPalette { skyTop = C("#3f8fe0"), skyHorizon = C("#c8e8ff"), far = C("#7aa6c8"), mid = C("#5fae5a"), near = C("#2f7a3a"), fog = C("#e8f6ff40"), sun = true, nearKind = NearKind.RoundTrees },
        };
    }
}
