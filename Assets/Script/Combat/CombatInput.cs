using Assets.Script.Core;
using Assets.Script.Player;
using UnityEngine;
using Assets.Script.Skill;

namespace Assets.Script.Combat
{
    /// <summary>
    /// PHÍM CHIẾN ĐẤU: J = đánh bằng chiêu đang chọn, R = hồi sinh khi chết, nút "AttackBtn" + icon bình "HP" / "MP" trên HUD (QuickPotion).
    /// Chỉ chuyển ý định sang SkillCaster — không chứa luật chiến đấu.
    /// </summary>
    public class CombatInput : MonoBehaviour
    {
        private bool _attackButtonHooked, _potionIconsHooked;
        private float _nextHookTry;

        private void Update()
        {
            if (LocalPlayerState.Id < 0) return;

            if ((!_attackButtonHooked || !_potionIconsHooked) && Time.unscaledTime >= _nextHookTry) HookAttackButton();

            if (GameInput.Pressed(GameKey.Attack)) CastSelected();
            SkillCaster.Tick();   // lệnh đánh bấm lúc đòn trước sắp xong
            if (GameInput.Pressed(GameKey.Revive) && LocalPlayerState.IsDead) SkillCaster.Revive();
        }

        /// <summary>Đánh bằng chiêu đang chọn trên thanh phím tắt.</summary>
        public static void CastSelected()
        {
            if (UI.Kit.UIRoot.GameplayBlocked) return;   // đang mở túi / shop... → không đánh (cả nút cảm ứng)
            if (SkillBarManager.Instance != null) SkillCaster.TryCast(SkillBarManager.Instance.GetSelectedSkillId());
        }

        /// <summary>Nút "AttackBtn" của HUD chưa nối sự kiện trong prefab; HUD bật sau đăng nhập nên thử lại mỗi giây.</summary>
        private void HookAttackButton()
        {
            _nextHookTry = Time.unscaledTime + 1f;
            if (!_potionIconsHooked) _potionIconsHooked = QuickPotion.HookHudIcons();
            if (_attackButtonHooked) return;
            var go = GameObject.Find("AttackBtn");
            if (go == null || !go.TryGetComponent(out UnityEngine.UI.Button btn)) return;
            btn.onClick.AddListener(CastSelected);
            _attackButtonHooked = true;
        }
    }
}
