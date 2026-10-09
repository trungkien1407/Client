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
        }

        /// <summary>BROADCAST_ATTACK: int attackerId, byte targetType (0 quái · 1 người · 2 chiêu hỗ trợ), int targetId, int skillId, int damage, int hpRemain, byte dead</summary>
        private void OnBroadcastAttack(byte[] data)
        {
            var r = new MessageReader(data);
            int attackerId = r.ReadInt();
            byte targetType = r.ReadByte();
            int targetId = r.ReadInt();
            int skillId = r.ReadInt();
            int damage = r.ReadInt();
            int hpRemain = r.ReadInt();
            r.ReadByte(); // dead: quái chết có MOB_DIE riêng, người chết có PLAYER_DIE riêng
            r.Cleanup();

            bool mine = attackerId == LocalPlayerState.Id;
            if (!mine) NetworkPlayerManager.Instance?.GetRemotePlayer(attackerId)?.PlayAttack(skillId);
            if (targetType == 2) return;   // chiêu hỗ trợ: chỉ diễn anim, số hồi máu đến bằng PLAYER_HEAL

            if (targetType == 0)
            {
                var mob = NetworkMobManager.Instance?.GetMob(targetId);
                if (mob == null) return;
                mob.UpdateHp(hpRemain);
                DamagePopup.Show(mob.transform.position, DamageText(damage), mine ? MyHitColor : OtherHitColor);
            }
            else
            {
                ApplyPlayerHp(targetId, hpRemain, damage);
            }
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
            Debug.Log($"[EXP] Level {level} — EXP {exp}");
        }

        /// <summary>PLAYER_STATS: int maxHp, int maxMp, int hp, int mp, int bonusDamage, int yen (sau khi mặc đồ / mua bán / thưởng)</summary>
        private void OnPlayerStats(byte[] data)
        {
            var r = new MessageReader(data);
            int maxHp = r.ReadInt();
            int maxMp = r.ReadInt();
            int hp = r.ReadInt();
            int mp = r.ReadInt();
            Data.GameData.Me.bonusDamage = r.ReadInt();
            Data.GameData.Me.yen = r.ReadInt();
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

        private static Transform GetPlayerTransform(int playerId) => NetworkPlayerManager.Instance?.GetPlayerTransform(playerId);
    }
}
