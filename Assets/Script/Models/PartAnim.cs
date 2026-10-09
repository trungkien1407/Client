using System;

namespace Assets.Script.Models
{
    /// <summary>
    /// ĐƠN VỊ HÌNH ẢNH (theo game gốc NSO / G4M): dữ liệu tính bằng "điểm gốc" 1×, 24 điểm = 1 ô map = 1 đơn vị Unity;
    /// ảnh vẽ ở 4× → 96 px = 1 đơn vị (pixelsPerUnit 96).
    /// </summary>
    public static class ArtUnits
    {
        public const float PointsPerUnit = 24f;
        public const float PixelsPerUnit = 96f;
        public static float ToUnits(int points) => points / PointsPerUnit;
    }

    /// <summary>1 mảnh ghép: ảnh số {img} (của bộ hình quái, hoặc kho ảnh với hiệu ứng) đặt pivot tại (dx, dy) điểm gốc, dy âm = lên trên.</summary>
    [Serializable]
    public struct FramePart
    {
        public short img, dx, dy;
        public FramePart(int img, int dx, int dy) { this.img = (short)img; this.dx = (short)dx; this.dy = (short)dy; }
    }

    /// <summary>1 khung = nhiều mảnh ghép + (tuỳ chọn) hiệu ứng bật lúc vào khung này (VD tia lửa khi quái cắn).</summary>
    [Serializable]
    public class PartFrame
    {
        public FramePart[] parts = Array.Empty<FramePart>();
        public short fx = -1, fxX, fxY;
    }

    /// <summary>1 chuỗi khung (1 trạng thái quái: đứng / đi / đánh...).</summary>
    [Serializable]
    public class PartClip
    {
        public PartFrame[] frames = Array.Empty<PartFrame>();
        public int Count => frames == null ? 0 : frames.Length;
    }
}
