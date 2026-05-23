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

        [Header("Data Package (Sprite Atlas)")]
        // Đây chính là "File Data" chứa toàn bộ ảnh của quái vật
        public AssetReferenceT<SpriteAtlas> mobAtlas;
    }
}