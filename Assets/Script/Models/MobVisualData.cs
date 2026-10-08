using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D; // Bắt buộc để dùng SpriteAtlas

namespace Assets.Script.Models
{
    [System.Serializable]
    public class MobVisualData
    {
        public string mobName;
        public short templateId;
        public float frameRate = 0.15f;

        [Tooltip("Tiền tố tên sprite trong atlas: <spriteKey>_0,_1 (đi) _2 (đánh) _3 (chết). Để trống = dùng templateId. "
               + "Cho nhiều loại quái dùng chung 1 bộ hình (vd Ốc Sên Tinh Anh dùng hình Ốc Sên).")]
        public string spriteKey;

        [Tooltip("Nhân kích thước hình (tinh anh/boss to hơn). 0 = 1.")]
        public float scale = 1f;

        [Tooltip("Nhuộm màu hình (GĐ8): nhiều loại quái mượn chung 1 bộ hình → đổi màu cho dễ phân biệt. Trắng = giữ nguyên. "
               + "Alpha = 0 (dòng cũ chưa đặt) cũng coi như trắng.")]
        public Color tint = Color.white;

        [Header("Data Package (Sprite Atlas)")]
        // Đây chính là "File Data" chứa toàn bộ ảnh của quái vật
        [Tooltip("[CẦN ĐIỀN] Atlas ảnh của quái này (vd Assets/Sprite/Mob/mob_1.spriteatlasv2). Atlas phải là Addressable (group Atlases)")]
        public AssetReferenceT<SpriteAtlas> mobAtlas;
    }
}