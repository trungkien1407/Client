using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// Tự tạo các manager gameplay mới (CombatNetwork, CombatInput, GroundItemNetwork, ChatBox...) khi game chạy,
    /// để KHÔNG phải sửa scene bằng tay. [RuntimeInitializeOnLoadMethod] = Unity tự gọi hàm này
    /// 1 lần sau khi scene đầu tiên load xong (giống "main" của game).
    /// Muốn đặt vào scene thủ công thì cứ đặt — hàm này thấy đã có sẽ không tạo thêm.
    /// </summary>
    public static class GameplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateManagers()
        {
            if (Object.FindAnyObjectByType<CombatNetwork>() != null) return;

            var go = new GameObject("[GameplayNetwork]");
            go.AddComponent<CombatNetwork>();      // Awake() tự DontDestroyOnLoad
            go.AddComponent<CombatInput>();        // phím J / R, nút AttackBtn
            go.AddComponent<Assets.Script.Network.Heartbeat>();
            go.AddComponent<GroundItemNetwork>();
            go.AddComponent<ChatBox>();
            go.AddComponent<Assets.Script.Data.GameDataNetwork>(); // dữ liệu tĩnh, túi đồ, chỉ số, nhiệm vụ
            go.AddComponent<Assets.Script.Data.EconomyNetwork>();  // nâng cấp, rương, giao dịch, chợ, ngọc, giftcode
            go.AddComponent<Assets.Script.Data.SocialNetwork>();   // PvP, nhóm, bạn bè, thư, gia tộc, xếp hạng
            go.AddComponent<Assets.Script.Data.EventNetwork>();    // hiệu ứng, phó bản, sự kiện, khu, tên map, hoạt động, mẹo
            go.AddComponent<Assets.Script.Data.NpcNetwork>();      // nói chuyện NPC: hộp thoại, cửa hàng
            go.AddComponent<Assets.Script.UI.GameHud>();           // thanh EXP, nút menu, tiền, banner, khung sự kiện
            go.AddComponent<Assets.Script.UI.MobileControls>();    // joystick + nút ảo (chỉ hiện trên máy cảm ứng)
        }
    }
}
