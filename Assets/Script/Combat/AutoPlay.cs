using System;
using System.Collections.Generic;
using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.Entities;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.Skill;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// TỰ ĐỘNG (phím Z / nút Auto trên HUD; tuỳ chọn ở AutoWindow — phím U). Mỗi khung hình, theo thứ tự:
    ///   1. Bình máu / chakra: máu (chakra) dưới ngưỡng % → uống bình hợp nhất (QuickPotion). Chạy cả khi TẮT auto
    ///      (tuỳ chọn riêng, mặc định bật).
    ///   2. Nhặt đồ: có đồ rơi trong PickSearch ô → đi tới, tới sát thì GroundItemNetwork tự nhặt.
    ///   3. Đánh quái: chọn quái (tự tìm khắp map / chỉ quanh chỗ bật auto) → ngoài tầm thì
    ///        có "Tự động luyện tập" còn hạn trong túi → xin server dịch chuyển tới cạnh quái (AUTO_TELE),
    ///        không có → tự chạy bộ (nhảy / rơi xuyên bục khi kẹt);
    ///      trong tầm → đánh bằng CHIÊU ĐANG CHỌN trên thanh phím.
    ///   Chiêu buff người chơi chọn trong AutoWindow (AutoSettings.Buff): hồi xong là dùng ngay (hồi máu chỉ khi máu dưới 75%).
    /// Bấm phím đi → nhường tay ManualPause giây. Chết / đổi map → tự tắt.
    /// Client chỉ "bấm hộ": tầm, hồi chiêu, dịch chuyển, nhặt đồ đều do server kiểm như khi chơi tay.
    /// </summary>
    public class AutoPlay : MonoBehaviour
    {
        public const float LocalRadius = 5f;      // tắt "tự tìm quái": chỉ đánh quái cách chỗ bật auto chừng này
        private const float PickSearch = 6f;      // đi nhặt đồ trong bán kính này
        private const float PickGiveUp = 4f;      // đi mãi không nhặt được (khác bục, túi đầy) → bỏ món đó 30 giây
        private const float TeleMinDist = 2.5f;   // gần hơn thì đi bộ cho tự nhiên
        private const float TeleGap = 1.2f;       // server cho 1 lần / giây
        private const float GiveUp = 5f;          // chừng này giây không lại gần mục tiêu hơn → bỏ con đó 15 giây
        private const float ManualPause = 2f;
        private const float PotionGap = 2.3f;     // server: bình 2 giây / lần (dư chút cho trễ mạng — sớm quá server báo "chờ")
        private const float HotPotionGap = 10.3f; // đan hồi dần: 10 giây / lần
        private const float HealBelow = 0.75f;    // chiêu hồi máu chỉ dùng khi máu dưới 75%

        public static AutoPlay Instance { get; private set; }
        public static bool IsOn => Instance != null && Instance._on;
        /// <summary>Bật / tắt (HUD đổi nút).</summary>
        public static event Action<bool> OnToggled;
        /// <summary>Số lần đã xin server dịch chuyển (AutoTestRunner đọc).</summary>
        public static int TeleRequests { get; private set; }

        private bool _on;
        private Vector3 _anchor;
        private MobController _mob;
        private float _mobSince, _bestDist;
        private int _itemId = -1;
        private float _itemSince;
        private readonly Dictionary<int, float> _skipMob = new Dictionary<int, float>(), _skipItem = new Dictionary<int, float>();
        private float _nextTele, _pauseUntil, _nextHp, _nextMp, _nextNoPotionWarn;
        private float _lastX, _stuckSince;
        private int _jumps;
        private PlayerTargeting _targeting;

        private void Awake()
        {
            Instance = this;
            Map.MapManager.OnMapLoaded += OnMapLoaded;
            GameData.OnSessionReset += OnSessionReset;
        }

        private void OnDestroy()
        {
            Map.MapManager.OnMapLoaded -= OnMapLoaded;
            GameData.OnSessionReset -= OnSessionReset;
        }

        private void OnMapLoaded(GameObject _) { if (_on) SetOn(false, "Tự động tắt khi đổi map."); }
        private void OnSessionReset() { if (_on) SetOn(false, null); }

        public static void Toggle() { if (Instance != null) Instance.SetOn(!Instance._on, null); }

        /// <summary>Bật / tắt + báo trong khung chat (msg = null → câu mặc định).</summary>
        public void SetOn(bool on, string msg)
        {
            var me = Me();
            if (on && (me == null || LocalPlayerState.IsDead)) return;
            _on = on;
            _mob = null; _itemId = -1; _skipMob.Clear(); _skipItem.Clear(); _pauseUntil = 0f;
            if (me != null) { me.AutoMoveX = 0f; _anchor = me.transform.position; }
            if (msg == null)
            {
                float left = GameData.AutoTrainSecondsLeft();
                msg = !on ? "Tự động: TẮT."
                    : left > 0 ? $"Tự động: BẬT — dịch chuyển tới quái (Tự động luyện tập còn {GameData.FormatDuration(left)})."
                    : "Tự động: BẬT — chạy bộ tới quái (mua \"Tự động luyện tập\" ở Hokage để dịch chuyển).";
            }
            if (!string.IsNullOrEmpty(msg)) ChatBox.AddSystem(msg);
            OnToggled?.Invoke(on);
        }

        private void Update()
        {
            if (LocalPlayerState.Id < 0) return;
            if (GameInput.Pressed(GameKey.AutoToggle)) Toggle();
            var me = Me();
            if (me == null) return;
            Potions();
            if (!_on) return;

            if (LocalPlayerState.IsDead) { SetOn(false, "Tự động tắt vì nhân vật bị hạ."); return; }
            if (UI.Kit.UIRoot.GameplayBlocked || LocalPlayerState.IsStunned) { me.AutoMoveX = 0f; return; }
            if (Mathf.Abs(me.ManualInput.x) > 0.01f) { _pauseUntil = Time.time + ManualPause; me.AutoMoveX = 0f; return; }
            if (Time.time < _pauseUntil || ActionInput.IsRunning) return;
            if (me.IsActionLocked) { me.AutoMoveX = 0f; _stuckSince = Time.time; return; }   // đang ra đòn: đứng yên, không phải kẹt

            if (PickStep(me)) return;
            FightStep(me);
        }

        // ---------------------------------------------------------------- 1. bình máu / chakra
        private void Potions()
        {
            if (LocalPlayerState.IsDead) return;
            if (AutoSettings.PotionHp && Time.time >= _nextHp && Below(LocalPlayerState.Hp, LocalPlayerState.MaxHp, AutoSettings.HpPercent))
                _nextHp = Time.time + Drink(true);
            if (AutoSettings.PotionMp && Time.time >= _nextMp && Below(LocalPlayerState.Mp, LocalPlayerState.MaxMp, AutoSettings.MpPercent))
                _nextMp = Time.time + Drink(false);
        }

        private static bool Below(int cur, int max, int pct) => max > 0 && cur * 100 < max * pct;

        /// <summary>Uống bình hợp nhất; trả số giây chờ tới lần thử sau (theo hồi chiêu loại bình).</summary>
        private float Drink(bool hp)
        {
            int id = QuickPotion.Pick(hp);
            if (id >= 0)
            {
                GameActions.UseItem(id);
                return GameData.Items.TryGetValue(id, out var t) && t.type >= 2 ? HotPotionGap : PotionGap;
            }
            if (Time.time >= _nextNoPotionWarn)   // hết bình: nhắc 1 lần / 30 giây
            {
                _nextNoPotionWarn = Time.time + 30f;
                ChatBox.AddSystem(hp ? "Tự động: hết bình máu!" : "Tự động: hết bình chakra!");
            }
            return PotionGap;
        }

        // ---------------------------------------------------------------- 2. nhặt đồ
        private bool PickStep(PlayerMovement me)
        {
            var g = GroundItemNetwork.Instance;
            if (!AutoSettings.PickItems || g == null || GameData.Inventory.Count >= GameData.BagCapacity) return false;
            if (_itemId >= 0 && Time.time - _itemSince > PickGiveUp) { _skipItem[_itemId] = Time.time + 30f; _itemId = -1; }
            if (!g.TryNearest(me.transform.position, PickSearch, id => Skipped(_skipItem, id), out int item, out var pos))
            {
                _itemId = -1;
                return false;
            }
            if (item != _itemId) { _itemId = item; _itemSince = Time.time; ResetStuck(me); }
            if (Mathf.Abs(pos.x - me.transform.position.x) < 0.5f && Mathf.Abs(pos.y - me.transform.position.y) < 1f) me.AutoMoveX = 0f;   // đứng sát → chờ tự nhặt
            else Steer(me, pos);
            return true;
        }

        // ---------------------------------------------------------------- 3. đánh quái
        private void FightStep(PlayerMovement me)
        {
            if (!Alive(_mob) || Skipped(_skipMob, _mob.GetId()))
            {
                _mob = ChooseMob(me);
                _mobSince = Time.time; _bestDist = float.MaxValue;
                ResetStuck(me);
                if (_mob == null) { me.AutoMoveX = 0f; return; }
            }

            // Buff đã chọn hồi xong → dùng ngay (lên bản thân, không cần tầm)
            int buff = ReadyBuff();
            if (buff >= 0) { me.AutoMoveX = 0f; SkillCaster.TryCast(buff); return; }

            int skill = AttackSkill();
            bool ready = Usable(skill);
            float range = SkillCaster.SkillRange(skill);
            float d = _mob.DistanceFrom(me.transform.position);
            if (d < _bestDist - 0.3f) { _bestDist = d; _mobSince = Time.time; }   // còn lại gần được → chưa bỏ

            if (d > range - 0.15f)
            {
                if (Time.time - _mobSince > GiveUp) { _skipMob[_mob.GetId()] = Time.time + 15f; _mob = null; me.AutoMoveX = 0f; return; }
                if (GameData.AutoTrainSecondsLeft() > 0f && d > TeleMinDist)
                {
                    me.AutoMoveX = 0f;
                    if (Time.time >= _nextTele) { _nextTele = Time.time + TeleGap; GameActions.AutoTele(_mob.GetId()); TeleRequests++; }
                }
                else Steer(me, _mob.transform.position);
                return;
            }

            me.AutoMoveX = 0f;
            if (!ready) return;   // chưa chiêu nào hồi → đứng chờ trong tầm
            if (_targeting == null) _targeting = FindAnyObjectByType<PlayerTargeting>();
            if (_targeting != null && !ReferenceEquals(_targeting.currentTarget, _mob)) _targeting.SelectTarget(_mob);
            SkillCaster.TryCast(skill);
        }

        private MobController ChooseMob(PlayerMovement me)
        {
            if (NetworkMobManager.Instance == null) return null;
            bool tele = GameData.AutoTrainSecondsLeft() > 0f;
            Vector3 p = me.transform.position;
            MobController best = null;
            float bestScore = float.MaxValue;
            foreach (var m in NetworkMobManager.Instance.All)
            {
                if (!Alive(m) || Skipped(_skipMob, m.GetId())) continue;
                if (!AutoSettings.FindMobs && m.DistanceFrom(_anchor) > LocalRadius) continue;
                Vector3 mp = m.transform.position;
                // Chạy bộ: ưu tiên quái cùng tầng (lên / xuống bục tốn công) — dịch chuyển thì chỉ cần gần
                float score = tele ? ((Vector2)(mp - p)).magnitude : Mathf.Abs(mp.x - p.x) + Mathf.Abs(mp.y - p.y) * 3f;
                if (score < bestScore) { bestScore = score; best = m; }
            }
            return best;
        }

        /// <summary>Chiêu đánh: luôn là chiêu ĐANG CHỌN trên thanh phím; chưa chọn / đang chọn chiêu hỗ trợ → đòn cơ bản (id = hệ).</summary>
        private static int AttackSkill()
        {
            int id = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
            if (id < 0 || IsSupport(id)) id = GameData.MySkills.ContainsKey(GameData.ClassType) ? GameData.ClassType : id;
            return id;
        }

        /// <summary>Chiêu buff người chơi bật trong AutoWindow, đã học, đã hồi, đủ chakra; chiêu hồi máu chỉ khi máu dưới 75%. -1 = không có.</summary>
        private static int ReadyBuff()
        {
            foreach (var id in GameData.MySkills.Keys)
            {
                if (!IsSupport(id) || !AutoSettings.Buff(id) || !Usable(id)) continue;
                var lv = SkillCaster.LevelOf(id);
                bool heal = lv != null && (lv.healBase > 0 || lv.healPct > 0f);
                if (!heal || LocalPlayerState.Hp < LocalPlayerState.MaxHp * HealBelow) return id;
            }
            return -1;
        }

        /// <summary>Chiêu hỗ trợ (buff / hồi máu) mà hệ này học được — AutoWindow liệt kê để chọn.</summary>
        public static List<SkillTpl> BuffSkills()
        {
            var list = new List<SkillTpl>();
            foreach (var t in GameData.Skills.Values)
                if (t.type == (int)Constants.SkillType.Support && (t.classId == 0 || t.classId == GameData.ClassType)) list.Add(t);
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        private static bool IsSupport(int id) => GameData.Skills.TryGetValue(id, out var t) && t.type == (int)Constants.SkillType.Support;

        /// <summary>Đã học, hồi xong, đủ chakra.</summary>
        private static bool Usable(int id)
        {
            if (!GameData.MySkills.ContainsKey(id) || !SkillCooldowns.Ready(id)) return false;
            var lv = SkillCaster.LevelOf(id);
            return lv == null || LocalPlayerState.Mp >= lv.manaUse;
        }

        // ---------------------------------------------------------------- di chuyển
        /// <summary>Chạy ngang về phía pos; đứng yên quá 0,5 giây (vướng bậc, đích ở tầng khác) → nhảy hoặc rơi xuyên bục.</summary>
        private void Steer(PlayerMovement me, Vector3 pos)
        {
            float dx = pos.x - me.transform.position.x, dy = pos.y - me.transform.position.y;
            me.AutoMoveX = Mathf.Abs(dx) > 0.2f ? Mathf.Sign(dx) : 0f;
            float x = me.transform.position.x;
            if (Mathf.Abs(x - _lastX) > 0.05f) { _lastX = x; _stuckSince = Time.time; return; }
            if (Time.time - _stuckSince < 0.5f || !me.IsGrounded) return;
            if (dy < -0.5f) me.TryDropThrough();
            else if (_jumps < 3 || dy > 0.5f) { me.Jump(); _jumps++; }
            _stuckSince = Time.time;
        }

        private void ResetStuck(PlayerMovement me) { _jumps = 0; _stuckSince = Time.time; _lastX = me.transform.position.x; }

        // ---------------------------------------------------------------- tiện ích
        private static bool Alive(MobController m) => m != null && m.gameObject.activeInHierarchy && !m.IsDead;

        private static bool Skipped(Dictionary<int, float> skip, int id) => skip.TryGetValue(id, out float until) && Time.time < until;

        private static PlayerMovement Me() => NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
    }
}
