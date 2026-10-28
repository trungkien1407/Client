using Assets.Script.Constants;
using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.Player;
using UnityEngine;
using Assets.Script.Skill;

namespace Assets.Script.Combat
{
    /// <summary>
    /// RA CHIÊU phía client: kiểm hồi chiêu + chakra, chọn mục tiêu, kiểm tầm, gửi USE_SKILL, diễn anim ngay.
    /// Hồi chiêu do CLIENT tự đếm (SkillCooldowns) — chưa hồi thì không gửi; server chỉ xác thực, không trả lời.
    /// Client KHÔNG tự trừ máu — server là trọng tài, kết quả về bằng BROADCAST_ATTACK (CombatNetwork vẽ).
    /// Gọi từ: phím J (CombatInput), nút "Đánh" (MobileControls), bấm ô chiêu (SkillBarManager).
    /// </summary>
    public static class SkillCaster
    {
        /// <summary>Bán kính tự tìm quái khi bấm đánh mà chưa chọn mục tiêu.</summary>
        public const float AutoTargetRadius = 5f;
        private const float LocalAttackGap = 0.3f;   // chống spam phía client; hồi chiêu thật do server quyết
        private const float DefaultRange = 5f;       // tầm 0 trong dữ liệu = 5 (giống server)

        private const float QueueWindow = 0.35f;     // bấm khi đòn trước còn ≤ 0,35 giây thì nhớ lệnh

        private static float _nextAttackTime;
        private static float _nextWarn;
        private static PlayerTargeting _targeting;
        private static int _queuedSkill = -1;
        private static float _queuedUntil;

        /// <summary>Gọi mỗi khung (CombatInput): đòn trước diễn xong mà còn lệnh đã nhớ → ra chiêu đó.</summary>
        public static void Tick()
        {
            if (_queuedSkill < 0) return;
            if (Time.time > _queuedUntil || UI.Kit.UIRoot.GameplayBlocked) { _queuedSkill = -1; return; }
            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local == null || local.IsActionLocked) return;
            int s = _queuedSkill;
            _queuedSkill = -1;
            TryCast(s);
        }

        /// <summary>Dùng chiêu {skillId} lên mục tiêu đang chọn; chưa chọn → tự chọn quái gần nhất.</summary>
        public static void TryCast(int skillId)
        {
            if (skillId < 0 || LocalPlayerState.IsDead) return;
            if (LocalPlayerState.IsStunned) { ChatBox.AddSystem("Đang bị choáng!"); return; }
            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local == null) return;

            // Đánh theo nhịp anim: đòn trước chưa diễn xong thì chưa ra chiêu mới. Bấm lúc anim sắp hết (≤ QueueWindow)
            // → nhớ lại, hết anim tự ra (Tick) — bấm liên tục không bị "nuốt phím".
            float busy = local.TryGetComponent(out PlayerVisualController pvc) ? pvc.ActionLockRemaining : 0f;
            if (busy > 0f)
            {
                if (busy <= QueueWindow) { _queuedSkill = skillId; _queuedUntil = Time.time + busy + 0.1f; }
                return;
            }
            _queuedSkill = -1;
            if (Time.time < _nextAttackTime) return;
            if (!SkillCooldowns.Ready(skillId)) { Warn($"Chiêu chưa hồi ({SkillCooldowns.Remaining(skillId):0.0} giây)."); return; }
            var lv = LevelOf(skillId);
            if (lv != null && LocalPlayerState.Mp < lv.manaUse) { Warn($"Không đủ chakra (cần {lv.manaUse})."); return; }

            // Chiêu hỗ trợ (hồi máu / tăng sức mạnh): lên bản thân + đồng đội gần, không cần mục tiêu
            if (Data.GameData.Skills.TryGetValue(skillId, out var tpl) && tpl.type == (int)SkillType.Support)
            {
                Data.GameActions.UseSkill(skillId, AttackTarget.Self, LocalPlayerState.Id);
                SkillCooldowns.Start(skillId);
                Swing(local, skillId);
                return;
            }

            ITargetable target = PickTarget(local.transform.position);
            if (target == null) { Swing(local, skillId); return; }

            AttackTarget targetType;
            switch (target.GetTargetType())
            {
                case TargetType.Mob: targetType = AttackTarget.Mob; break;
                case TargetType.Player: targetType = AttackTarget.Player; break;
                case TargetType.NPC:
                    Data.GameActions.NpcTalk(target.GetId());   // "đánh" NPC = nói chuyện (tiện cho điện thoại)
                    return;
                default: return;
            }

            // Xa quá thì báo + vung suông, không gửi (server cũng chặn)
            float range = SkillRange(skillId);
            var tt = target.GetTransform();
            float dist = target is MobController m ? m.DistanceFrom(local.transform.position)   // tới mép khung va chạm, khớp server
                       : tt != null ? Vector2.Distance(local.transform.position, tt.position) : 0f;
            if (dist > range + 0.3f)
            {
                Warn($"Quá xa (tầm chiêu {range:0.#}) — lại gần mục tiêu.");
                Swing(local, skillId,target);
                return;
            }

            Data.GameActions.UseSkill(skillId, targetType, target.GetId());
            SkillCooldowns.Start(skillId);   // tự đếm, không chờ server
            Swing(local, skillId,target);   // diễn ngay, không đợi server (bị từ chối thì chỉ là anim suông)
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

        /// <summary>Dữ liệu cấp chiêu đang có (tầm, chakra, hồi chiêu...) — GAME_DATA_SKILLS; chưa có → null.</summary>
        public static Data.SkillLevel LevelOf(int skillId)
        {
            if (!Data.GameData.Skills.TryGetValue(skillId, out var tpl)) return null;
            Data.GameData.MySkills.TryGetValue(skillId, out int point);
            return tpl.Level(point) ?? (tpl.levels.Count > 0 ? tpl.levels[0] : null);
        }

        /// <summary>Tầm chiêu ở cấp đang có.</summary>
        public static float SkillRange(int skillId)
        {
            var lv = LevelOf(skillId);
            return lv != null && lv.range > 0 ? lv.range : DefaultRange;
        }

        /// <summary>Nhắc trong khung chat, tối đa 1 lần / 1,5 giây (bấm liên tục không bị tràn chat).</summary>
        private static void Warn(string msg)
        {
            if (Time.time < _nextWarn) return;
            ChatBox.AddSystem(msg);
            _nextWarn = Time.time + 1.5f;
        }
        private static void Swing(PlayerMovement local, int skillId, ITargetable target = null)
        {
            _nextAttackTime = Time.time + LocalAttackGap;
            var tt = target?.GetTransform();
            if (tt != null) local.FaceTowards(tt.position);
            if (local.TryGetComponent(out PlayerVisualController vc)) vc.PlaySkill(skillId);
        }
    }
}
