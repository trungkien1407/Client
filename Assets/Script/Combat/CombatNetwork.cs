using System;
using Assets.Script.Constants;
using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.Combat
{
    /// <summary>
    /// ĐỒNG BỘ CHIẾN ĐẤU với server (server là trọng tài, client chỉ gửi ý định + vẽ kết quả):
    ///   Gửi:  USE_SKILL (phím J / nút AttackBtn / bấm ô skill), REVIVE (phím R), HEARTBEAT (20s/lần cho khỏi bị kick).
    ///   Nhận: BROADCAST_ATTACK, MOB_ATTACK, PLAYER_HEAL, PLAYER_EXP_UPDATE, PLAYER_STATS,
    ///         PLAYER_DIE, REVIVE. (Túi đồ/trang bị/nhiệm vụ → Data/GameDataNetwork.)
    ///
    /// Không cần kéo vào scene: GameplayBootstrap tự tạo khi game chạy.
    /// </summary>
    public class CombatNetwork : NetworkListener
    {
        public static CombatNetwork Instance { get; private set; }

        [Tooltip("Bán kính tự tìm quái khi bấm đánh mà chưa chọn mục tiêu")]
        public float autoTargetRadius = 5f;

        private const float HeartbeatInterval = 20f;
        private const float LocalAttackGap = 0.3f; // chống spam phía client; cooldown thật do server quyết

        private float _lastHeartbeat;
        private float _nextAttackTime;
        private PlayerTargeting _targeting;
        private bool _attackButtonHooked;
        private float _nextHookTry;

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

        protected override void Update()
        {
            base.Update(); // NetworkListener: đăng ký handler khi dispatcher sẵn sàng

            bool inGame = LocalPlayerState.Id >= 0 && NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            if (!inGame) return;

            // HEARTBEAT: server ngắt kết nối nếu 60s không nhận gói nào (vd đứng AFK)
            if (Time.unscaledTime - _lastHeartbeat > HeartbeatInterval)
            {
                _lastHeartbeat = Time.unscaledTime;
                NetworkManager.Instance.Send(Cmd.HEARTBEAT, null);
            }

            if (!_attackButtonHooked && Time.unscaledTime >= _nextHookTry) HookAttackButton();

            var kb = Keyboard.current;
            if (kb == null || ChatBox.IsTyping) return;

            if (kb.jKey.wasPressedThisFrame && SkillBarManager.Instance != null)
                TryUseSkill(SkillBarManager.Instance.GetSelectedSkillId());

            if (kb.rKey.wasPressedThisFrame && LocalPlayerState.IsDead)
                SendRevive();
        }

        /// <summary>
        /// Nút "AttackBtn" có sẵn trên HUD (cho điện thoại) nhưng chưa nối sự kiện -> nối ở runtime.
        /// HUD chỉ bật sau khi đăng nhập nên thử lại mỗi giây tới khi tìm thấy.
        /// </summary>
        private void HookAttackButton()
        {
            _nextHookTry = Time.unscaledTime + 1f;
            var go = GameObject.Find("AttackBtn");
            if (go == null || !go.TryGetComponent(out UnityEngine.UI.Button btn)) return;
            btn.onClick.AddListener(() =>
            {
                if (SkillBarManager.Instance != null) TryUseSkill(SkillBarManager.Instance.GetSelectedSkillId());
            });
            _attackButtonHooked = true;
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
            Listen(Cmd.LOGOUT, _ => { });
        }

        // ==========================================
        // GỬI LÊN SERVER
        // ==========================================

        /// <summary>
        /// Đánh mục tiêu đang chọn bằng skill {skillTemplateId}. Chưa chọn ai -> tự chọn quái gần nhất.
        /// Client KHÔNG tự trừ máu: chỉ gửi USE_SKILL, đợi server trả BROADCAST_ATTACK mới vẽ số.
        /// </summary>
        public void TryUseSkill(int skillTemplateId)
        {
            if (skillTemplateId < 0 || LocalPlayerState.IsDead) return;
            if (Time.time < _nextAttackTime) return;

            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local == null) return;

            if (_targeting == null) _targeting = FindAnyObjectByType<PlayerTargeting>();
            ITargetable target = _targeting != null ? _targeting.currentTarget : null;

            // Mục tiêu cũ đã chết/bị thu hồi -> bỏ
            if (target is MobController deadMob && (deadMob == null || deadMob.IsDead)) target = null;

            if (target == null && NetworkMobManager.Instance != null)
            {
                var nearest = NetworkMobManager.Instance.FindNearestAlive(local.transform.position, autoTargetRadius);
                if (nearest != null)
                {
                    target = nearest;
                    _targeting?.SelectTarget(nearest);
                }
            }
            if (target == null) return;

            byte targetType;
            switch (target.GetTargetType())
            {
                case TargetType.Mob: targetType = 0; break;
                case TargetType.Player: targetType = 1; break;
                case TargetType.NPC:
                    // "Đánh" vào NPC = nói chuyện (tiện cho điện thoại: chạm NPC rồi bấm nút tấn công)
                    Data.GameActions.NpcTalk(target.GetId());
                    return;
                default: return; // Item không đánh được
            }

            _nextAttackTime = Time.time + LocalAttackGap;

            var w = new MessageWriter();
            w.WriteInt(skillTemplateId);
            w.WriteByte(targetType);
            w.WriteInt(target.GetId());
            NetworkManager.Instance.Send(Cmd.USE_SKILL, w.ToArray());
            w.Cleanup();

            // Diễn anim ngay cho mượt (nếu server từ chối do cooldown/hết mana thì chỉ là anim suông)
            if (local.TryGetComponent(out PlayerVisualController vc)) vc.PlayActionOnce("Punch_Combo");
        }

        public void SendRevive()
        {
            NetworkManager.Instance?.Send(Cmd.REVIVE, null);
        }

        // ==========================================
        // NHẬN TỪ SERVER
        // ==========================================

        /// <summary>BROADCAST_ATTACK: int attackerId, byte targetType, int targetId, int skillId, int damage, int hpRemain, byte dead</summary>
        private void OnBroadcastAttack(byte[] data)
        {
            var r = new MessageReader(data);
            int attackerId = r.ReadInt();
            byte targetType = r.ReadByte();
            int targetId = r.ReadInt();
            r.ReadInt(); // skillId (chưa dùng: sau này chọn hiệu ứng theo skill)
            int damage = r.ReadInt();
            int hpRemain = r.ReadInt();
            r.ReadByte(); // dead: quái chết có MOB_DIE riêng, người chết có PLAYER_DIE riêng
            r.Cleanup();

            bool mine = attackerId == LocalPlayerState.Id;
            if (!mine) NetworkPlayerManager.Instance?.GetRemotePlayer(attackerId)?.PlayAttack();

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
            LocalPlayerState.Exp = exp;
            LocalPlayerState.Level = level;
            LocalPlayerState.SetHpMp(hp, maxHp, mp, maxMp);
            Data.GameData.Me.exp = exp;
            Data.GameData.Me.level = level;
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
            LocalPlayerState.BonusDamage = r.ReadInt();
            LocalPlayerState.Yen = r.ReadInt();
            r.Cleanup();
            LocalPlayerState.SetHpMp(hp, maxHp, mp, maxMp);
            Data.GameData.Me.yen = LocalPlayerState.Yen;
            Data.GameData.Me.maxHp = maxHp;
            Data.GameData.Me.maxMp = maxMp;
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
                    $"Bạn đã bị hạ gục! Mất {expLost} EXP.\nBấm OK (hoặc phím R) để hồi sinh.", SendRevive);
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

        // ==========================================
        private static Transform GetPlayerTransform(int playerId)
        {
            var npm = NetworkPlayerManager.Instance;
            if (npm == null) return null;
            if (playerId == LocalPlayerState.Id) return npm.localPlayer != null ? npm.localPlayer.transform : null;
            var rp = npm.GetRemotePlayer(playerId);
            return rp != null ? rp.transform : null;
        }
    }
}
