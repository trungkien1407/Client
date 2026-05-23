using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "NewNinjaClass", menuName = "Game Data/Ninja Class")]
public class NinjaClassData : ScriptableObject
{
    public int classId;          // 1: Kiếm, 2: Tiêu, 3: Kunai
    public string className;     // "Phái Kiếm"
    public string element;       // "Hỏa"
    public string academy;       // "Làng Lá"

    [TextArea(3, 5)]
    public string description;

    [Header("Spine Asset (Tải động)")]
    // Dùng AssetReference chung nhất, tương thích với mọi bản Unity Addressables
    public AssetReference spinePrefab;
}