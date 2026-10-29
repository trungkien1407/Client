using Assets.Script.Constants;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// NHẬN KẾT QUẢ CHIẾN ĐẤU từ server (server là trọng tài) → cập nhật máu / chết / hồi sinh + hiện số:
    ///   BROADCAST_ATTACK, MOB_ATTACK, PLAYER_HEAL, PLAYER_EXP_UPDATE, PLAYER_STATS, PLAYER_DIE, REVIVE.
    /// Gửi chiêu: SkillCaster · phím: CombatInput · heartbeat: Network/Heartbeat. GameplayBootstrap tự tạo.
    /// </summary>
    public class CombatNetwork : NetworkListener
    {
        public static CombatNetwork Instance { get; private set; }
        /// <summary>Số đòn của MÌNH trúng quái (server xác nhận) — AutoTestRunner đo tự động có đánh không.</summary>
        public static int MyMobHits { get; private set; }

        private static readonly Color MyHitColor = new Color(1f, 0.85f, 0.2f);    // vàng: mình gây
        private static readonly Color OtherHitColor = Color.white;                 // trắng: người khác gây
        private static readonly Color HurtColor = new Color(1f, 0.3f, 0.3f);       // đỏ: mình bị đánh
        private static readonly Color HealColor = new Color(0.4f, 1f, 0.4f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected override void RegisterHandlers()
        {
            Listen(Cmd.BROADCAST_ATTACK, OnBroadcastAttack);
            Listen(Cmd.MOB_ATTACK, OnMobAttack);
            Listen(Cmd.PLAYER_HEAL, OnPlayerHeal);
            Listen(Cmd.PLAYER_EXP_UPDATE, OnExpUpdate);
            Listen(Cmd.PLAYER_STATS, OnPlayerStats);
            Listen(Cmd.PLAYER_DIE, OnPlayerDie);
            Listen(Cmd.REVIVE, OnRevive);
            Listen(Cmd.SKILL_COOLDOWN, OnSkillCooldown);
        }

        /// <summary>
        /// BROADCAST_ATTACK: int attackerId, byte targetType (0 quái · 1 người · 2 chiêu hỗ trợ), int targetId, int skillId, int damage, int hpRemain, byte dead
        /// MÁU cập nhật NGAY (server đã tính xong — trọng tài); SỐ sát thương + hiệu ứng trúng (fxHit) giữ lại tới KHUNG TRÚNG ĐÒN
        /// của anim người ra chiêu (PlayerVisualController.HitTime, tối đa MaxHitDelay) → số bật ra đúng lúc nắm đấm / shuriken chạm.
        /// </summary>
        private void OnBroadcastAttack(byte[] data)
        {
            var r = new MessageReader(data);
            int attackerId = r.ReadInt();
            var targetType = (AttackTarget)r.ReadByte();
            int targetId = r.ReadInt();
            int skillId = r.ReadInt();
            int damage = r.ReadInt();
            int hpRemain = r.ReadInt();
            r.ReadByte(); // dead: quái chết có MOB_DIE riêng, người chết có PLAYER_DIE riêng
            r.Cleanup();

            bool mine = attackerId == LocalPlayerState.Id;
            Transform from = GetPlayerTransform(attackerId);
            Transform to = targetType == AttackTarget.Mob ? NetworkMobManager.Instance?.GetMob(targetId)?.transform
                         : targetType == AttackTarget.Player ? GetPlayerTransform(targetId) : from;
            // Người khác ra chiêu: quay mặt về mục tiêu + diễn anim (mình đã diễn lúc bấm — SkillCaster)
            if (!mine) NetworkPlayerManager.Instance?.GetRemotePlayer(attackerId)?.PlayAttack(skillId, targetType == AttackTarget.Self ? null : to);
            PlayCastFx(attackerId, from, to, skillId);
            if (targetType == AttackTarget.Self) return;   // chiêu hỗ trợ: chỉ diễn anim, số hồi máu đến bằng PLAYER_HEAL

            if (targetType == AttackTarget.Mob)
            {
                if (mine) MyMobHits++;
                var mob = NetworkMobManager.Instance?.GetMob(targetId);
                if (mob == null) return;
                mob.UpdateHp(hpRemain);
            }
            else if (targetId == LocalPlayerState.Id) LocalPlayerState.SetHp(hpRemain);
            else NetworkPlayerManager.Instance?.GetRemotePlayer(targetId)?.UpdateHp(hpRemain);

            Color color = targetId == LocalPlayerState.Id && targetType == AttackTarget.Player ? HurtColor : mine ? MyHitColor : OtherHitColor;
            Vector3 lastPos = to != null ? to.position : Vector3.zero;
            float delay = HitDelay(from);
            AtHit(delay, () =>
            {
                if (to != null && to.gameObject.activeInHierarchy) lastPos = to.position;   // quái chết / bị thu hồi → số hiện ở chỗ cuối
                if (to != null || lastPos != Vector3.zero) DamagePopup.Show(lastPos, DamageText(damage), color);
                PlayHitFx(from, to, skillId);
            });
        }

        /// <summary>Tối đa giữ số sát thương bao lâu (giây) — anim dài cũng không để số chậm quá so với máu.</summary>
        private const float MaxHitDelay = 0.4f;

        /// <summary>Còn bao lâu tới khung trúng đòn của anim người ra chiêu (0 = hiện ngay).</summary>
        private static float HitDelay(Transform attacker)
        {
            if (attacker == null || !attacker.TryGetComponent(out PlayerVisualController pvc)) return 0f;
            return Mathf.Clamp(pvc.HitTime - Time.time, 0f, MaxHitDelay);
        }

        /// <summary>Chạy {action} sau {delay} giây (≈ 0 → chạy ngay).</summary>
        private void AtHit(float delay, System.Action action)
        {
            if (delay <= 0.02f) { action(); return; }
            StartCoroutine(AtHitLater(delay, action));
        }

        private static System.Collections.IEnumerator AtHitLater(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action();
        }
        /// <summary>MOB_ATTACK: int mobId, int playerId, int damage, int playerHpRemain</summary>
        private void OnMobAttack(byte[] data)
        {
            var r = new MessageReader(data);
            int mobId = r.ReadInt();
            int playerId = r.ReadInt();
            int damage = r.ReadInt();
            int hpRemain = r.ReadInt();
            r.Cleanup();

            Transform victim = GetPlayerTransform(playerId);
            var mob = NetworkMobManager.Instance?.GetMob(mobId);
            if (mob != null && victim != null) mob.PlayAttack(victim.position);

            ApplyPlayerHp(playerId, hpRemain, damage);
        }

        /// <summary>Cập nhật máu 1 người chơi (mình hoặc người khác) + hiện số sát thương.</summary>
        private void ApplyPlayerHp(int playerId, int hpRemain, int damage)
        {
            if (playerId == LocalPlayerState.Id)
            {
                LocalPlayerState.SetHp(hpRemain);
                var t = GetPlayerTransform(playerId);
                if (t != null) DamagePopup.Show(t.position, DamageText(damage), HurtColor);
                return;
            }
            var rp = NetworkPlayerManager.Instance?.GetRemotePlayer(playerId);
            if (rp == null) return;
            rp.UpdateHp(hpRemain);
            DamagePopup.Show(rp.transform.position, DamageText(damage), OtherHitColor);
        }

        /// <summary>SKILL_COOLDOWN: short n, [int skillId, int remainingMs] × n — chiêu còn đang hồi lúc vào game.</summary>
        private void OnSkillCooldown(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            for (int i = 0; i < n; i++) SkillCooldowns.Set(r.ReadInt(), r.ReadInt());
            r.Cleanup();
        }

        /// <summary>PLAYER_HEAL: int id, int hpHeal, int mpHeal, int hp, int maxHp, int mp, int maxMp (hồi máu mỗi giây / dùng bình)</summary>
        private void OnPlayerHeal(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            int hpHeal = r.ReadInt();
            r.ReadInt(); // mpHeal
            int hp = r.ReadInt();
            int maxHp = r.ReadInt();
            int mp = r.ReadInt();
            int maxMp = r.ReadInt();
            r.Cleanup();

            if (id == LocalPlayerState.Id)
            {
                LocalPlayerState.SetHpMp(hp, maxHp, mp, maxMp);
                // Hồi tự nhiên +1/giây thì thôi không hiện, chỉ hiện khi uống bình (hồi nhiều)
                if (hpHeal >= 10)
                {
                    var t = GetPlayerTransform(id);
                    if (t != null) DamagePopup.Show(t.position, "+" + hpHeal, HealColor);
                }
                return;
            }
            var rp = NetworkPlayerManager.Instance?.GetRemotePlayer(id);
            if (rp == null) return;
            rp.SetMaxHp(maxHp);
            rp.UpdateHp(hp);
        }

        /// <summary>PLAYER_EXP_UPDATE: long exp, short level, int maxHp, int maxMp, int hp, int mp</summary>
        private void OnExpUpdate(byte[] data)
        {
            var r = new MessageReader(data);
            long exp = r.ReadLong();
            short level = r.ReadShort();
            int maxHp = r.ReadInt();
            int maxMp = r.ReadInt();
            int hp = r.ReadInt();
            int mp = r.ReadInt();
            r.Cleanup();

            bool levelUp = level > LocalPlayerState.Level;
            Data.GameData.Me.exp = exp;
            Data.GameData.Me.level = level;
            LocalPlayerState.SetHpMp(hp, maxHp, mp, maxMp);
            Data.GameData.Notify(Data.DataKind.Character);

            if (levelUp)
            {
                var t = GetPlayerTransform(LocalPlayerState.Id);
                if (t != null) DamagePopup.Show(t.position + Vector3.up * 0.6f, "LÊN CẤP " + level + "!", new Color(1f, 0.75f, 0f), 6f);
            }
#if UNITY_EDITOR
            Debug.Log($"[EXP] Level {level} — EXP {exp}");   // chỉ trong Editor: bản build ghi log kèm stack trace mỗi lần hạ quái
#endif
        }

        /// <summary>PLAYER_STATS: int maxHp, int maxMp, int hp, int mp, int bonusDamage, long yen (sau khi mặc đồ / mua bán / thưởng)</summary>
        private void OnPlayerStats(byte[] data)
        {
            var r = new MessageReader(data);
            int maxHp = r.ReadInt();
            int maxMp = r.ReadInt();
            int hp = r.ReadInt();
            int mp = r.ReadInt();
            Data.GameData.Me.bonusDamage = r.ReadInt();
            Data.GameData.Me.yen = r.ReadLong();
            r.Cleanup();
            LocalPlayerState.SetHpMp(hp, maxHp, mp, maxMp);
            Data.GameData.Notify(Data.DataKind.Character);
        }

        /// <summary>PLAYER_DIE: int playerId, long expLost</summary>
        private void OnPlayerDie(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            long expLost = r.ReadLong();
            r.Cleanup();

            if (id == LocalPlayerState.Id)
            {
                LocalPlayerState.IsDead = true;
                LocalPlayerState.ClearEffects();   // chết = mất mọi hiệu ứng (server cũng xoá) → tắt icon trên HUD
                LocalPlayerState.SetHp(0);
                var local = NetworkPlayerManager.Instance?.localPlayer;
                if (local != null)
                {
                    local.SetDead(true);
                    if (local.TryGetComponent(out PlayerVisualController vc)) vc.SetDeathState(true);
                }
                PopupAndLoad.Instance?.ShowPopup(
                    $"Bạn đã bị hạ gục! Mất {expLost} EXP.\nBấm OK (hoặc phím R) để hồi sinh.", SkillCaster.Revive);
                return;
            }
            NetworkPlayerManager.Instance?.GetRemotePlayer(id)?.SetDead(true);
        }

        /// <summary>REVIVE (S→C): int playerId, float x, float y, int hp, int maxHp, int mp, int maxMp</summary>
        private void OnRevive(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            float x = r.ReadFloat();
            float y = r.ReadFloat();
            int hp = r.ReadInt();
            int maxHp = r.ReadInt();
            int mp = r.ReadInt();
            int maxMp = r.ReadInt();
            r.Cleanup();

            if (id == LocalPlayerState.Id)
            {
                LocalPlayerState.IsDead = false;
                LocalPlayerState.SetHpMp(hp, maxHp, mp, maxMp);
                var local = NetworkPlayerManager.Instance?.localPlayer;
                if (local != null)
                {
                    local.SnapTo(new Vector2(x, y));
                    local.SetDead(false);
                    if (local.TryGetComponent(out PlayerVisualController vc)) vc.SetDeathState(false);
                }
                PopupAndLoad.Instance?.HidePopup();
                return;
            }
            var rp = NetworkPlayerManager.Instance?.GetRemotePlayer(id);
            if (rp == null) return;
            rp.TeleportTo(x, y);
            rp.SetMaxHp(maxHp);
            rp.SetDead(false, hp);
        }

        /// <summary>Sát thương 0 = mục tiêu né được (server tính theo Thân pháp).</summary>
        private static string DamageText(int damage) => damage <= 0 ? "Né" : "-" + damage;

        // Chiêu diện rộng: mỗi quái trúng 1 gói → hiệu ứng ra chiêu chỉ diễn 1 lần
        private int _lastCastAttacker, _lastCastSkill;
        private float _lastCastTime;

        /// <summary>
        /// Hiệu ứng RA CHIÊU (SkillTpl.fxCast) ở người ra chiêu — số hiệu ứng do server cấu hình; phát ngay khi nhận gói.
        /// GỐC = GIỮA THÂN (không phải chân): dữ liệu hiệu ứng của game gốc chỉ lệch vài điểm quanh tâm thân — đặt ở chân thì
        /// đường chém / vụ nổ chìm xuống đất. Hiệu ứng ra chiêu bám theo người ra chiêu (chạy vẫn theo).
        /// </summary>
        private void PlayCastFx(int attackerId, Transform from, Transform to, int skillId)
        {
            if (!Data.GameData.Skills.TryGetValue(skillId, out var tpl) || tpl.fxCast < 0) return;
            bool flip = from != null && to != null && to.position.x < from.position.x;
            bool sameCast = attackerId == _lastCastAttacker && skillId == _lastCastSkill && Time.time - _lastCastTime < 0.2f;
            // Người ra chiêu có anim trong Spine (hiệu ứng vẽ sẵn) → không phát thêm fxCast theo dữ liệu (tránh chồng 2 hiệu ứng)
            bool spineFx = from != null && from.TryGetComponent(out PlayerVisualController pvc) && pvc.HasBuiltInCastFx(skillId);
            if (from != null && !sameCast && !spineFx) Fx.EffectPlayer.Play(tpl.fxCast, BodyCenter(from) - from.position, flip, from);
            _lastCastAttacker = attackerId; _lastCastSkill = skillId; _lastCastTime = Time.time;
        }

        /// <summary>Hiệu ứng TRÚNG ĐÒN (SkillTpl.fxHit) ở giữa thân mục tiêu — gọi đúng khung trúng của anim (OnBroadcastAttack).</summary>
        private static void PlayHitFx(Transform from, Transform to, int skillId)
        {
            if (to == null || !to.gameObject.activeInHierarchy) return;
            if (!Data.GameData.Skills.TryGetValue(skillId, out var tpl) || tpl.fxHit < 0) return;
            bool flip = from != null && to.position.x < from.position.x;
            Fx.EffectPlayer.Play(tpl.fxHit, BodyCenter(to), flip);
        }

        /// <summary>Giữa thân (world): quái → nửa khung va chạm; người → tâm BoxCollider2D; không biết → cao hơn chân 0,6 ô.</summary>
        public static Vector3 BodyCenter(Transform t)
        {
            if (t.TryGetComponent(out Entities.MobController mob)) return mob.BodyCenter;
            if (t.TryGetComponent(out BoxCollider2D box)) return new Vector3(t.position.x, box.bounds.center.y, t.position.z);
            return t.position + Vector3.up * 0.6f;
        }

        private static Transform GetPlayerTransform(int playerId) => NetworkPlayerManager.Instance?.GetPlayerTransform(playerId);
    }
}
