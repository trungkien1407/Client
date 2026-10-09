using Assets.Script.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.Combat
{
    /// <summary>
    /// PHÍM CHIẾN ĐẤU: J = đánh bằng chiêu đang chọn, R = hồi sinh khi chết, nút "AttackBtn" trên HUD.
    /// Chỉ chuyển ý định sang SkillCaster — không chứa luật chiến đấu.
    /// </summary>
    public class CombatInput : MonoBehaviour
    {
        private bool _attackButtonHooked;
        private float _nextHookTry;

        private void Update()
        {
            if (LocalPlayerState.Id < 0) return;

            if (!_attackButtonHooked && Time.unscaledTime >= _nextHookTry) HookAttackButton();

            var kb = Keyboard.current;
            if (kb == null || ChatBox.IsTyping) return;
            if (kb.jKey.wasPressedThisFrame) CastSelected();
            if (kb.rKey.wasPressedThisFrame && LocalPlayerState.IsDead) SkillCaster.Revive();
        }

        /// <summary>Đánh bằng chiêu đang chọn trên thanh phím tắt.</summary>
        public static void CastSelected()
        {
            if (SkillBarManager.Instance != null) SkillCaster.TryCast(SkillBarManager.Instance.GetSelectedSkillId());
        }

        /// <summary>Nút "AttackBtn" của HUD chưa nối sự kiện trong prefab; HUD bật sau đăng nhập nên thử lại mỗi giây.</summary>
        private void HookAttackButton()
        {
            _nextHookTry = Time.unscaledTime + 1f;
            var go = GameObject.Find("AttackBtn");
            if (go == null || !go.TryGetComponent(out UnityEngine.UI.Button btn)) return;
            btn.onClick.AddListener(CastSelected);
            _attackButtonHooked = true;
        }
    }
}
