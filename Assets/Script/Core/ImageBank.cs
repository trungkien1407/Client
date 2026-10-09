using System;
using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// KHO ẢNH ĐÁNH SỐ (học từ NSO / G4M: icon vật phẩm, icon chiêu, khung hiệu ứng đều là "ảnh số N").
    /// Server chỉ gửi SỐ (item.iconId, skill.iconId, khung hiệu ứng) → client lấy ảnh img{N}:
    ///   Addressables address "img{N}"  →  Resources "ImageBank/img{N}" (demo nội bộ).
    /// Quy ước ảnh: vẽ ở 4× (96 px = 1 ô map), pixelsPerUnit 96, pivot = tâm ảnh (khung hiệu ứng đặt tâm tại dx, dy).
    /// </summary>
    public static class ImageBank
    {
        public static string Address(int id) => "img" + id;

        public static void Get(int id, Action<Sprite> done)
        {
            if (id < 0) { done?.Invoke(null); return; }
            string a = Address(id);
            AssetSource.Load(a, "ImageBank/" + a, done);
        }

        /// <summary>Ảnh đã tải sẵn (hiệu ứng vẽ từng khung — chưa có thì bỏ qua khung đó).</summary>
        public static Sprite Cached(int id) => id < 0 ? null : AssetSource.Cached<Sprite>(Address(id));

        /// <summary>Tải trước (VD mọi khung của 1 hiệu ứng sắp diễn).</summary>
        public static void Preload(int id) { if (id >= 0 && Cached(id) == null) Get(id, null); }
    }
}
