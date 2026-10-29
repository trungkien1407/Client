using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Script.Map
{
    /// <summary>
    /// MẶT NƯỚC VẼ ≠ MÉP Ô NƯỚC: vật lý đặt chân người đi trên nước ngang MÉP TRÊN ô nước (bằng mặt đất bờ — ra bờ không vướng góc),
    /// nhưng ảnh ô nước (Assets/Sprite/water2.png, 96 px = 1 ô) có 25 hàng trên cùng trong suốt → ngọn sóng thấp hơn mép ô ~0,26 ô.
    /// Không bù thì chân lơ lửng trên sóng → hạ PHẦN HÌNH nhân vật đúng chừng ấy (PlayerVisualController.SetWaterSink), vật lý giữ nguyên.
    /// Đổi ảnh nước khác thì sửa Depth = (số hàng trong suốt trên cùng) / 96.
    /// </summary>
    public static class WaterLine
    {
        public const float Depth = 25f / 96f;
        /// <summary>Anim chạy (Run) nhấc chân cao hơn tư thế đứng ~0,12 ô → hạ thêm chừng ấy để bước chạy chạm sóng.</summary>
        public const float RunLift = 0.12f;
        /// <summary>Hạ hình khi đi trên nước (người đi trên nước luôn đang chạy — đứng yên là chìm).</summary>
        public const float Sink = Depth + RunLift;

        private static GameObject _forMap;
        private static Tilemap _water;

        /// <summary>
        /// Chân ở {feet} đang ĐI TRÊN mặt nước (dưới chân là ô nước, chân ngang mép trên ô) — dùng cho người chơi khác
        /// (chỉ biết vị trí server gửi). Nhân vật mình biết sẵn qua PlayerMovement.
        /// </summary>
        public static bool StandingOnWater(Vector2 feet)
        {
            var root = MapManager.Instance != null ? MapManager.Instance.currentMapInstance : null;
            if (root == null) return false;
            if (_forMap != root)   // đổi map → tìm lại lớp "Water" (Tools → Naruto → Map → 5)
            {
                _forMap = root;
                var w = root.transform.Find("Water");
                _water = w != null ? w.GetComponent<Tilemap>() : null;
            }
            if (_water == null) return false;
            var cell = _water.WorldToCell(new Vector3(feet.x, feet.y - 0.1f, 0f));
            if (!_water.HasTile(cell)) return false;
            return Mathf.Abs(feet.y - _water.CellToWorld(cell + Vector3Int.up).y) < 0.15f;
        }
    }
}
