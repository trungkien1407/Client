using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>
    /// THÔNG TIN MAP gắn ở gốc prefab Map_N — prefab là NGUỒN DỮ LIỆU map:
    ///   va chạm lấy từ Tilemap (Ground = đặc · Platform = bục 1 chiều · Water = nước; Decor không va chạm),
    ///   NPC / quái / cổng lấy từ các điểm MapNpcSpot / MapMobSpot / MapPortalSpot (con của "Spots").
    /// Sửa xong: Tools → Naruto → Map → "Xuất map cho server" → data/maps/map_N.json (server đọc khi khởi động).
    /// Toạ độ: 1 ô = 1 đơn vị, ô (0,0) ở góc dưới-trái; map rộng {width} ô, cao {height} ô.
    /// </summary>
    public class MapInfo : MonoBehaviour
    {
        public int mapId;
        public string mapName;
        [Tooltip("Số ô ngang / dọc (ô ngoài khung không tính va chạm)")] public int width = 60, height = 14;
        [Tooltip("Chỗ xuất hiện khi vào map không qua cổng (đăng nhập, hồi sinh)")] public Vector2 spawn = new Vector2(3.5f, 4.05f);
        [Tooltip("Map có boss thế giới")] public bool hasBoss;
        public Vector2 boss;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            var p = transform.position;
            Gizmos.DrawWireCube(p + new Vector3(width / 2f, height / 2f), new Vector3(width, height));
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(p + (Vector3)spawn + Vector3.up * 0.5f, 0.5f);
            if (hasBoss) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(p + (Vector3)boss + Vector3.up, 1f); }
        }
    }
}
