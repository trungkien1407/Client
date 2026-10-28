using UnityEngine;

namespace Assets.Script.Models
{
    /// <summary>
    /// BỘ HÌNH 1 NPC kiểu NSO / G4M: 2 ảnh tĩnh — THÂN (đầu + mình) và CHÂN. 1 bộ dùng cho mọi NPC có npc_template.art = số này.
    /// Tải theo tên: Addressables "NpcArt_{art}" → Resources "NpcArt/NpcArt_{art}" (demo: Tools/Naruto/Demo art).
    /// Ảnh: pivot GIỮA-ĐÁY, pixelsPerUnit 96 (như hình quái). Chân đặt ở gốc NPC, thân đặt tại {bodyOffsetPx}.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcArt", menuName = "NinjaSchool/Npc Art")]
    public class NpcArtSO : ScriptableObject
    {
        public Sprite body;
        [Tooltip("Có thể trống (NPC 1 ảnh, vd tượng / cây)")] public Sprite legs;

        [Tooltip("Vị trí giữa-đáy ảnh THÂN so với giữa-đáy ảnh CHÂN, điểm ảnh gốc (x: sang phải, y: lên trên). " +
                 "Ảnh thân hay có tay / vũ khí chìa 1 bên nên tâm ảnh ≠ tâm eo → không đặt thẳng tâm được. " +
                 "DemoArtTool tự đo (mép dưới thân khớp mép trên chân); hình riêng thì chỉnh tay ở đây.")]
        public Vector2 bodyOffsetPx;

        public static string Address(int art) => "NpcArt_" + art;

        private float Ppu => body != null ? body.pixelsPerUnit : 96f;

        /// <summary>Vị trí đặt ảnh thân (đơn vị thế giới, so với gốc NPC).</summary>
        public Vector2 BodyPos => legs == null ? Vector2.zero : bodyOffsetPx / Ppu;

        /// <summary>Đỉnh đầu (đơn vị thế giới) — đặt tên NPC / mũi tên chọn phía trên.</summary>
        public float TopY => BodyPos.y + (body != null ? body.rect.height / body.pixelsPerUnit : 1.5f);
    }
}
