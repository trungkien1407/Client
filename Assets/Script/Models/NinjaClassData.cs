using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Assets.Script.Models
{
    [CreateAssetMenu(fileName = "NewNinjaClass", menuName = "Game Data/Ninja Class")]
    public class NinjaClassData : ScriptableObject
    {
        public int classId;          // 1: Đấu sĩ (Naruto), 2: Hỗ trợ (Sakura), 3: Sát thủ (Sasuke) — khớp class_type server
        public string className;     // "Đấu Sĩ"
        public string element;       // vai trò, hiện sau chữ "Hệ:" ở màn tạo nhân vật
        public string academy;       // trường của hệ (Đấu Sĩ Đường...)

        [TextArea(3, 5)]
        public string description;

        [Header("Spine Asset (Tải động)")]
        // Dùng AssetReference chung nhất, tương thích với mọi bản Unity Addressables
        public AssetReference spinePrefab;
    }
}
