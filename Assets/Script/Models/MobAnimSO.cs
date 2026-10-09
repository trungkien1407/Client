using UnityEngine;

namespace Assets.Script.Models
{
    /// <summary>
    /// BỘ HÌNH 1 QUÁI kiểu NSO / G4M: vài ảnh mảnh + 5 chuỗi khung ghép mảnh. 1 bộ dùng cho mọi quái có monster_template.art = số này.
    /// Tải theo tên: Addressables "MobAnim_{art}" → Resources "MobAnim/MobAnim_{art}" (demo: Tools/Naruto/Demo art).
    /// Ảnh mảnh: pivot GÓC TRÊN-TRÁI, pixelsPerUnit 96; (dx, dy) của mảnh = vị trí góc đó so với chân quái.
    /// Hình gốc quay mặt sang PHẢI.
    /// </summary>
    [CreateAssetMenu(fileName = "MobAnim", menuName = "NinjaSchool/Mob Anim")]
    public class MobAnimSO : ScriptableObject
    {
        public const int Idle = 0, Walk = 1, Attack = 2, Attack2 = 3, Hurt = 4, StateCount = 5;

        [Tooltip("Khung va chạm (điểm gốc, 24 = 1 ô) — chỉ để hiển thị (thanh máu, chọn mục tiêu); server dùng monster_template.hit_w / hit_h")]
        public int hitW = 24, hitH = 32;
        [Tooltip("Giây mỗi khung")]
        public float frameSeconds = 0.08f;
        public Sprite[] images;
        [Tooltip("0 đứng · 1 đi · 2 đánh · 3 đánh (kèm hiệu ứng) · 4 bị đánh / chết")]
        public PartClip[] states = new PartClip[StateCount];

        public static string Address(int art) => "MobAnim_" + art;

        /// <summary>Chuỗi khung của trạng thái; rỗng → lùi về đứng.</summary>
        public PartClip Clip(int state)
        {
            if (states != null && state >= 0 && state < states.Length && states[state] != null && states[state].Count > 0) return states[state];
            return states != null && states.Length > 0 ? states[Idle] : null;
        }

        public Sprite Image(int i) => images != null && i >= 0 && i < images.Length ? images[i] : null;
    }
}
