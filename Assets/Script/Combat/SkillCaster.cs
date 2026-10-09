using Assets.Script.Constants;
using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.Player;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// RA CHIÊU phía client: chọn mục tiêu, kiểm tầm, gửi USE_SKILL, diễn anim ngay.
    /// Client KHÔNG tự trừ máu — server là trọng tài, kết quả về bằng BROADCAST_ATTACK (CombatNetwork vẽ).
    /// Gọi từ: phím J (CombatInput), nút "Đánh" (MobileControls), bấm ô chiêu (SkillBarManager).
    /// </summary>
    public static class SkillCaster
    {
        /// <summary>Bán kính tự tìm quái khi bấm đánh mà chưa chọn mục tiêu.</summary>
        public const float AutoTargetRadius = 5f;
        private const float LocalAttackGap = 0.3f;   // chống spam phía client; hồi chiêu thật do server quyết
        private const float DefaultRange = 5f;       // tầm 0 trong dữ liệu = 5 (giống server)

        private static float _nextAttackTime;
        private static float _nextRangeWarn;
        private static PlayerTargeting _targeting;

        /// <summary>Dùng chiêu {skillId} lên mục tiêu đang chọn; chưa chọn → tự chọn quái gần nhất.</summary>
        public static void TryCast(int skillId)
        {
            if (skillId < 0 || LocalPlayerState.IsDead) return;
            if (LocalPlayerState.IsStunned) { ChatBox.AddSystem("Đang bị choáng!"); return; }
            if (Time.time < _nextAttackTime) return;

            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local == null) return;

            // Chiêu hỗ trợ (hồi máu / tăng sức mạnh): lên bản thân + đồng đội gần, không cần mục tiêu
            if (Data.GameData.Skills.TryGetValue(skillId, out var tpl) && tpl.type == 3)
            {
                Data.GameActions.UseSkill(skillId, 2, LocalPlayerState.Id);   // 2 = bản thân / nhóm
                Swing(local, skillId);
                return;
            }

            ITargetable target = PickTarget(local.transform.position);
            if (target == null) { Swing(local, skillId); return; }

            byte targetType;
            switch (target.GetTargetType())
            {
                case TargetType.Mob: targetType = 0; break;
                case TargetType.Player: targetType = 1; break;
                case TargetType.NPC:
                    Data.GameActions.NpcTalk(target.GetId());   // "đánh" NPC = nói chuyện (tiện cho điện thoại)
                    return;
                default: return;
            }

            // Xa quá thì báo + vung suông, không gửi (server cũng chặn)
            float range = SkillRange(skillId);
            var tt = target.GetTransform();
            if (tt != null && Vector2.Distance(local.transform.position, tt.position) > range + 0.3f)
            {
                if (Time.time >= _nextRangeWarn)
                {
                    ChatBox.AddSystem($"Quá xa (tầm chiêu {range:0.#}) — lại gần mục tiêu.");
                    _nextRangeWarn = Time.time + 1.5f;
                }
                Swing(local, skillId);
                return;
            }

            Data.GameActions.UseSkill(skillId, targetType, target.GetId());
            Swing(local, skillId);   // diễn ngay, không đợi server (bị từ chối thì chỉ là anim suông)
        }

        public static void Revive() => Data.GameActions.Revive();

        /// <summary>Mục tiêu đang chọn (bỏ nếu đã chết); không có → quái sống gần nhất trong AutoTargetRadius.</summary>
        private static ITargetable PickTarget(Vector3 from)
        {
            if (_targeting == null) _targeting = Object.FindAnyObjectByType<PlayerTargeting>();
            ITargetable target = _targeting != null ? _targeting.currentTarget : null;
            if (target is MobController mob && (mob == null || mob.IsDead)) target = null;
            if (target != null || NetworkMobManager.Instance == null) return target;

            var nearest = NetworkMobManager.Instance.FindNearestAlive(from, AutoTargetRadius);
            if (nearest != null) _targeting?.SelectTarget(nearest);
            return nearest;
        }

        /// <summary>Tầm chiêu ở cấp đang có (dữ liệu GAME_DATA_SKILLS).</summary>
        private static float SkillRange(int skillId)
        {
            if (!Data.GameData.Skills.TryGetValue(skillId, out var tpl)) return DefaultRange;
            Data.GameData.MySkills.TryGetValue(skillId, out int point);
            var lv = tpl.Level(point) ?? (tpl.levels.Count > 0 ? tpl.levels[0] : null);
            return lv != null && lv.range > 0 ? lv.range : DefaultRange;
        }

        private static void Swing(Component local, int skillId)
        {
            _nextAttackTime = Time.time + LocalAttackGap;
            if (local.TryGetComponent(out PlayerVisualController vc)) vc.PlaySkill(skillId);
        }
    }
}
