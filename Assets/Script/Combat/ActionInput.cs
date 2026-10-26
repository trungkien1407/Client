using Assets.Script.Core;
using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.Skill;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// PHÍM ENTER = "LÀM VIỆC VỚI MỤC TIÊU":
    ///   quái  → đánh bằng chiêu đang chọn (GIỮ Enter = đánh liên tục)
    ///   NPC   → nói chuyện
    ///   người → mở bảng tương tác (giao dịch, nhóm, tỉ thí...) — muốn đánh người thì vẫn dùng J
    /// Chưa chọn mục tiêu → tự chọn quái gần nhất, không có quái thì NPC gần nhất.
    /// Mục tiêu ở XA → nhân vật tự chạy tới (PlayerMovement.AutoMoveX), tới nơi thì làm việc đó.
    /// Bấm phím di chuyển / mục tiêu mất / chết / quá 8 giây → thôi tự chạy.
    /// Client chỉ đỡ phải đi tay; tầm đánh / tầm nói chuyện thật vẫn do server kiểm.
    /// </summary>
    public class ActionInput : MonoBehaviour
    {
        private const float SearchRadius = 6f;       // tự chọn mục tiêu khi chưa chọn ai
        private const float RunTimeout = 8f;         // chạy quá lâu (đường vòng / bị chặn) → bỏ
        private const float ArriveMargin = 0.4f;     // dừng TRONG tầm một chút (server đo vị trí đã gửi, trễ vài khung)
        private const float StuckSeconds = 0.5f;     // đứng yên chừng này lúc đang chạy = bị chặn → thử nhảy
        private const int MaxJumps = 3;

        private static ActionInput _instance;
        private PlayerTargeting _targeting;

        // trạng thái tự chạy
        private ITargetable _runTarget;
        private float _runUntil, _stuckSince, _lastX;
        private int _jumps;

        /// <summary>Đang tự chạy tới mục tiêu (AutoTestRunner đọc).</summary>
        public static bool IsRunning => _instance != null && _instance._runTarget != null;

        private void Awake() => _instance = this;

        private void Update()
        {
            if (LocalPlayerState.Id < 0) { StopRun(); return; }
            if (GameInput.Pressed(GameKey.Action)) Act();
            else if (_runTarget == null && GameInput.Held(GameKey.Action)) KeepAttacking();
            if (_runTarget != null) StepRun();
        }

        /// <summary>Bấm Enter (cũng gọi được từ code / nút).</summary>
        public static void Act()
        {
            if (_instance == null) return;
            _instance.DoAct();
        }

        private void DoAct()
        {
            var me = Me();
            if (me == null || LocalPlayerState.IsDead) return;
            if (LocalPlayerState.IsStunned) { ChatBox.AddSystem("Đang bị choáng!"); return; }

            // Chiêu hỗ trợ (hồi máu / tăng sức mạnh) lên bản thân + đồng đội: không cần mục tiêu, không chạy
            int skill = SelectedSkill();
            if (Data.GameData.Skills.TryGetValue(skill, out var tpl) && tpl.type == (int)Constants.SkillType.Support)
            {
                CombatInput.CastSelected();
                return;
            }

            var target = CurrentOrNearest(me.transform.position);
            if (target == null) { CombatInput.CastSelected(); return; }   // không có gì quanh đây → vung suông như J

            if (InRange(me, target)) { StopRun(); DoActionOn(target); }
            else StartRun(target);
        }

        // ---------------------------------------------------------------- chọn mục tiêu
        private ITargetable CurrentOrNearest(Vector3 from)
        {
            if (_targeting == null) _targeting = FindAnyObjectByType<PlayerTargeting>();
            var t = _targeting != null ? _targeting.currentTarget : null;
            if (!Alive(t)) t = null;
            if (t != null) return t;

            ITargetable pick = NetworkMobManager.Instance != null ? NetworkMobManager.Instance.FindNearestAlive(from, SearchRadius) : null;
            if (pick == null && NetworkNpcManager.Instance != null)
            {
                float best = SearchRadius * SearchRadius;
                foreach (var npc in NetworkNpcManager.Instance.All)
                {
                    float sqr = ((Vector2)(npc.transform.position - from)).sqrMagnitude;
                    if (sqr < best) { best = sqr; pick = npc; }
                }
            }
            if (pick != null) _targeting?.SelectTarget(pick);
            return pick;
        }

        private static bool Alive(ITargetable t)
        {
            if (t == null || !(t is MonoBehaviour mb) || mb == null || !mb.gameObject.activeInHierarchy) return false;
            return !(t is MobController m && m.IsDead);
        }

        // ---------------------------------------------------------------- tầm + việc cần làm
        /// <summary>Tầm làm việc với mục tiêu: quái = tầm chiêu đang chọn; NPC = tầm nói chuyện; người = tầm giao dịch.</summary>
        private static float RangeOf(ITargetable t)
        {
            switch (t.GetTargetType())
            {
                case TargetType.Mob: return SkillCaster.SkillRange(SelectedSkill());
                case TargetType.NPC: return Data.NpcNetwork.TalkRange;
                default: return 3f;   // người: đứng gần mới giao dịch / mời được (server TRADE_RANGE 6)
            }
        }

        private static float DistanceTo(Component me, ITargetable t)
        {
            if (t is MobController m) return m.DistanceFrom(me.transform.position);   // tới mép khung va chạm, khớp server
            var tt = t.GetTransform();
            return tt != null ? Vector2.Distance(me.transform.position, tt.position) : float.MaxValue;
        }

        private static bool InRange(Component me, ITargetable t) => DistanceTo(me, t) <= RangeOf(t) - (t.GetTargetType() == TargetType.Mob ? 0.1f : 0f);

        private static void DoActionOn(ITargetable t)
        {
            switch (t.GetTargetType())
            {
                case TargetType.Mob: CombatInput.CastSelected(); break;
                case TargetType.NPC: Data.GameActions.NpcTalk(t.GetId()); break;
                case TargetType.Player: GameWindow.Get<PlayerActionWindow>().ShowFor(t.GetId(), t.GetTargetName()); break;
            }
        }

        /// <summary>Giữ Enter khi đang đứng trong tầm quái → đánh tiếp (SkillCaster tự giãn nhịp, hồi chiêu do server).</summary>
        private void KeepAttacking()
        {
            var me = Me();
            if (me == null || LocalPlayerState.IsDead || LocalPlayerState.IsStunned) return;
            var t = _targeting != null ? _targeting.currentTarget : null;
            if (Alive(t) && t.GetTargetType() == TargetType.Mob && InRange(me, t) && SkillCooldowns.Ready(SelectedSkill())) CombatInput.CastSelected();   // chưa hồi thì chờ, không nhắc
        }

        // ---------------------------------------------------------------- tự chạy
        private void StartRun(ITargetable t)
        {
            var me = Me();
            _runTarget = t;
            _runUntil = Time.time + RunTimeout;
            _stuckSince = Time.time;
            _lastX = me != null ? me.transform.position.x : 0f;
            _jumps = 0;
        }

        private void StopRun()
        {
            _runTarget = null;
            var me = Me();
            if (me != null) me.AutoMoveX = 0f;
        }

        private void StepRun()
        {
            var me = Me();
            if (me == null || !Alive(_runTarget) || LocalPlayerState.IsDead || LocalPlayerState.IsStunned
                || Mathf.Abs(me.ManualInput.x) > 0.01f || Time.time > _runUntil)
            {
                bool timedOut = me != null && Time.time > _runUntil;
                StopRun();
                if (timedOut) ChatBox.AddSystem("Không tới được mục tiêu.");
                return;
            }
            if (me.IsActionLocked) { _stuckSince = Time.time; return; }   // đứng yên có chủ đích, không phải bị kẹt
            if (InRange(me, _runTarget, ArriveMargin))
            {
                var t = _runTarget;
                StopRun();
                DoActionOn(t);
                return;
            }

            var tt = _runTarget.GetTransform();
            float dx = tt.position.x - me.transform.position.x;
            float dy = tt.position.y - me.transform.position.y;
            me.AutoMoveX = Mathf.Abs(dx) > 0.2f ? Mathf.Sign(dx) : 0f;   // ngay bên dưới / bên trên → chỉ còn cách nhảy

            // Không nhích được (vướng bậc, mục tiêu trên bục cao) → nhảy, tối đa vài lần
            float x = me.transform.position.x;
            if (Mathf.Abs(x - _lastX) > 0.05f) { _lastX = x; _stuckSince = Time.time; }
            else if (Time.time - _stuckSince > StuckSeconds && me.IsGrounded)
            {
                // mục tiêu ở dưới → thử rơi xuyên bục 1 chiều; ở trên / ngang → nhảy
                bool moved = dy < -0.5f ? me.TryDropThrough() : _jumps < MaxJumps;
                if (!moved) { StopRun(); ChatBox.AddSystem("Không tới được mục tiêu."); return; }
                if (dy >= -0.5f) me.Jump();
                _jumps++;
                _stuckSince = Time.time;
            }
        }

        private static bool InRange(Component me, ITargetable t, float margin) => DistanceTo(me, t) <= Mathf.Max(0.5f, RangeOf(t) - margin);

        // ---------------------------------------------------------------- tiện ích
        private static PlayerMovement Me() => NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;

        private static int SelectedSkill() => SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
    }
}
