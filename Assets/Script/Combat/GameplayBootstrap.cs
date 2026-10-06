using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// Tự tạo các manager gameplay mới (CombatNetwork, GroundItemNetwork, ChatBox) khi game chạy,
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
            go.AddComponent<GroundItemNetwork>();
            go.AddComponent<ChatBox>();
            go.AddComponent<Assets.Script.Data.GameDataNetwork>(); // dữ liệu tĩnh, túi đồ, chỉ số, nhiệm vụ
            go.AddComponent<Assets.Script.UI.GameHud>();           // thanh EXP, nút menu, NPC, cửa hàng
            go.AddComponent<Assets.Script.UI.MobileControls>();    // joystick + nút ảo (chỉ hiện trên máy cảm ứng)
        }
    }
}
