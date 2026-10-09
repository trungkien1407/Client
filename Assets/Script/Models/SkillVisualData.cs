using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;

namespace Assets.Script.Models
{
    [System.Serializable]
    public class SkillVisualData
    {
        public string skillName;
        public int templateId;
        public int iconId; // Dùng để đối chiếu với icon_id từ DB Server

        [Header("Hiệu ứng chiêu thức (VFX)")]
        // Dùng AssetReference để Lazy Load Prefab hiệu ứng
        public AssetReferenceGameObject vfxPrefab;

        [Header("Icon Kỹ năng (Sprite)")]
        // Có thể trỏ thẳng vào 1 Sprite (hoặc AssetReferenceT<SpriteAtlas> nếu dùng Atlas)
        public AssetReferenceT<SpriteAtlas> iconSprite;
    }
}