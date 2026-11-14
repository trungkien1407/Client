using Assets.Script.Constants;
using Assets.Script.Data;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// TỰ ĐI TỚI NƠI LÀM NHIỆM VỤ (nút "Tự đi" trong sổ tay nhiệm vụ, phím L).
    ///
    /// Client không biết bản đồ cả thế giới, nên hỏi server TỪNG BƯỚC:
    ///   1. Begin(questId) → gửi AUTO_PATH_REQ.
    ///   2. Server trả AUTO_PATH: "chạy tới cổng (x, y)" hoặc "đã ở map đích, chạy tới (x, y)" hoặc "không đi được".
    ///   3. Chạy tới cổng → server tự chuyển map → vào map mới (MAP_INFO) thì hỏi lại → lặp tới map đích.
    ///   4. Ở map đích: thấy NPC / quái cần tìm thì giao cho ActionInput (như bấm Enter: tự chạy tới rồi nói chuyện / đánh).
    /// Thôi tự đi khi: bấm phím di chuyển, chết, kẹt không qua được, quá lâu, hoặc bấm "Tự đi" nhiệm vụ khác.
    /// Cách chạy giống ActionInput: đặt PlayerMovement.AutoMoveX về phía đích, không nhích được thì nhảy / rơi xuyên bục.
    /// </summary>
    public class AutoPath : NetworkListener
    {
        private const float StepTimeout = 60f;    // mỗi bước (1 map) tối đa 60 giây
        private const float StuckSeconds = 0.6f;  // đứng yên quá lâu = vướng → nhảy
        private const int MaxTries = 6;           // số lần nhảy / rơi liên tiếp mà vẫn không nhích được → bỏ
        private const float FindRadius = 14f;     // ở map đích: thấy NPC / quái trong bán kính này thì giao cho ActionInput

        private static AutoPath _instance;

        private bool _active;            // đang tự đi
        private int _questId;            // nhiệm vụ cần tới; số ÂM = -mapId (đi tới 1 map chọn trên bản đồ thế giới)
        private bool _waiting;           // đã hỏi server, chờ trả lời
        private byte _kind;              // bước hiện tại: 1 tới cổng / 2 ở map đích
        private Vector2 _point;          // đích của bước hiện tại
        private byte _targetType;        // 1 NPC / 2 quái
        private int _targetTpl;
        private float _stepUntil, _stuckSince, _lastX, _nextFind, _askAt, _waitUntil;
        private int[] _targetTpls = new int[0];   // mọi mẫu quái hợp lệ (nhiệm vụ thu thập: nhiều loại quái cùng rơi 1 món)
        private int _tries;

        /// <summary>Đang tự đi (AutoTestRunner đọc).</summary>
        public static bool IsActive => _instance != null && _instance._active;
        /// <summary>Chữ server gửi ở bước gần nhất, vd "Sói Rừng — Rừng Trúc (còn 2 map)" (AutoTestRunner đọc).</summary>
        public static string LastText { get; private set; } = "";
        /// <summary>Kiểu bước gần nhất: 0 không đi được / 1 tới cổng / 2 ở map đích.</summary>
        public static int LastKind { get; private set; } = -1;

        private void Awake()
        {
            _instance = this;
            GameData.OnSessionReset += OnSessionReset;   // đăng xuất / mất kết nối / đổi nhân vật → bỏ việc đang tự đi
        }

        protected override void OnDestroy()
        {
            GameData.OnSessionReset -= OnSessionReset;
            base.OnDestroy();
        }

        private void OnSessionReset() => Stop();

        protected override void RegisterHandlers() => Listen(Cmd.AUTO_PATH, OnAutoPath);

        // ---------------------------------------------------------------- bắt đầu / dừng
        /// <summary>Tự đi tới 1 map (bấm map trên bản đồ thế giới, phím B). Server nhận -mapId trong AUTO_PATH_REQ.</summary>
        public static void BeginMap(int mapId) => Begin(-mapId);

        /// <summary>Bắt đầu tự đi tới nơi làm nhiệm vụ questId (questId âm = -mapId: tới map đó).</summary>
        public static void Begin(int questId)
        {
            if (_instance == null || LocalPlayerState.IsDead) return;
            // Chỉ 1 thứ được lái nhân vật: tắt Tự động (Z) và thôi chạy tới mục tiêu (Enter)
            if (AutoPlay.IsOn) AutoPlay.Instance.SetOn(false, "Tắt Tự động để Tự đi.");
            ActionInput.Cancel();
            _instance._questId = questId;
            _instance._active = true;
            _instance.Ask();
            ChatBox.AddSystem("Đang tìm đường...");
        }

        /// <summary>Thôi tự đi (reason != null → báo lên khung chat).</summary>
        public static void Stop(string reason = null)
        {
            if (_instance == null || !_instance._active) return;
            _instance._active = false;
            _instance._kind = 0;
            _instance._waiting = false;
            var me = Me();
            if (me != null) me.AutoMoveX = 0f;
            if (!string.IsNullOrEmpty(reason)) ChatBox.AddSystem(reason);
        }

        /// <summary>EventNetwork gọi khi vào map / khu mới (MAP_INFO): đang tự đi thì hỏi bước tiếp.</summary>
        public static void OnEnteredMap()
        {
            if (!IsActive) return;
            _instance._kind = 0;                                   // dừng chạy theo bước của map cũ
            _instance._askAt = Time.time + 0.3f;                   // chờ nhân vật đặt xong vị trí mới rồi mới hỏi
            var me = Me();
            if (me != null) me.AutoMoveX = 0f;
        }

        private void Ask()
        {
            _waiting = true;
            _waitUntil = Time.time + 5f;   // server không trả (gói bị bỏ khi vừa đổi khu...) → thôi, không đứng chờ mãi
            _askAt = 0f;
            GameActions.AutoPath(_questId);
        }

        // ---------------------------------------------------------------- gói server
        /// <summary>AUTO_PATH: byte kiểu, float x, float y, byte loại đích, int mẫu, UTF chữ.</summary>
        private void OnAutoPath(byte[] data)
        {
            var r = new MessageReader(data);
            byte kind = r.ReadByte();
            float x = r.ReadFloat(), y = r.ReadFloat();
            byte type = r.ReadByte();
            int tpl = r.ReadInt();
            string text = r.ReadUTF();
            var tpls = new int[r.Available() > 0 ? r.ReadByte() : 0];   // đuôi: mọi mẫu hợp lệ (server cũ không gửi)
            for (int i = 0; i < tpls.Length; i++) tpls[i] = r.ReadInt();
            r.Cleanup();
            LastKind = kind;
            LastText = text;
            if (!_active) return;   // đã huỷ trong lúc chờ
            _waiting = false;
            if (kind == 0) { Stop(text); return; }

            _kind = kind;
            _point = new Vector2(x, y);
            _targetType = type;
            _targetTpl = tpl;
            _targetTpls = tpls.Length > 0 ? tpls : new[] { tpl };
            _stepUntil = Time.time + StepTimeout;
            _stuckSince = Time.time;
            _tries = 0;
            _nextFind = 0f;
            var me = Me();
            _lastX = me != null ? me.transform.position.x : 0f;
            ChatBox.AddSystem((kind == 1 ? "Tự đi: " : "Đã tới nơi: ") + text);
        }

        // ---------------------------------------------------------------- mỗi khung hình
        protected override void Update()
        {
            base.Update();   // NetworkListener đăng ký nghe gói AUTO_PATH ở đây — thiếu dòng này là không nhận được gói
            if (!_active) return;
            var me = Me();
            if (me == null) return;
            if (LocalPlayerState.IsDead) { Stop("Đã thôi tự đi."); return; }
            if (Mathf.Abs(me.ManualInput.x) > 0.01f) { Stop("Đã thôi tự đi (bạn tự di chuyển)."); return; }
            if (_askAt > 0f && Time.time >= _askAt) { Ask(); return; }
            if (_waiting) { if (Time.time > _waitUntil) Stop("Không tìm được đường — thử lại sau."); return; }
            if (_kind == 0) return;
            if (Time.time > _stepUntil) { Stop("Không tới được — hãy tự đi tiếp."); return; }
            if (LocalPlayerState.IsStunned || me.IsActionLocked || UI.Kit.UIRoot.GameplayBlocked)
            {
                me.AutoMoveX = 0f;
                _stuckSince = Time.time;        // đứng yên có lý do, không phải vướng
                _stepUntil += Time.deltaTime;   // mở cửa sổ / bị choáng không tính vào hạn 60 giây
                return;
            }

            // Ở map đích: thấy NPC / quái cần tìm → giao cho ActionInput (chọn làm mục tiêu + "bấm Enter")
            if (_kind == 2 && Time.time >= _nextFind)
            {
                _nextFind = Time.time + 0.3f;
                var target = FindTarget(me.transform.position);
                if (target != null)
                {
                    Stop();
                    var targeting = FindAnyObjectByType<PlayerTargeting>();
                    if (targeting != null) targeting.SelectTarget(target);
                    ActionInput.Act();
                    return;
                }
            }

            Vector2 pos = me.transform.position;
            float dx = _point.x - pos.x, dy = _point.y - pos.y;
            if (_kind == 2 && Mathf.Abs(dx) < 0.8f && Mathf.Abs(dy) < 2f)
            {
                Stop(_targetType == 2 ? "Đã tới chỗ quái ra — chờ quái hồi sinh." : "Đã tới nơi.");
                return;
            }
            me.AutoMoveX = Mathf.Abs(dx) > 0.2f ? Mathf.Sign(dx) : 0f;   // ngay trên / dưới đích → chỉ còn cách nhảy / rơi

            // Không nhích được (vướng bậc, cổng / đích ở trên bục) → nhảy; đích ở dưới → rơi xuyên bục 1 chiều
            if (Mathf.Abs(pos.x - _lastX) > 0.05f) { _lastX = pos.x; _stuckSince = Time.time; _tries = 0; }
            else if (Time.time - _stuckSince > StuckSeconds && me.IsGrounded)
            {
                if (_tries >= MaxTries) { Stop("Bị kẹt, không tới được — hãy tự đi tiếp."); return; }
                if (dy < -0.5f && me.TryDropThrough()) { }
                else me.Jump();
                _tries++;
                _stuckSince = Time.time;
            }
        }

        /// <summary>NPC / quái (còn sống) đúng mẫu cần tìm, trong bán kính FindRadius.</summary>
        private ITargetable FindTarget(Vector3 from)
        {
            if (_targetType == 1 && NetworkNpcManager.Instance != null)
            {
                var npc = NetworkNpcManager.Instance.FindByTemplate(_targetTpl);
                return npc != null && ((Vector2)(npc.transform.position - from)).sqrMagnitude <= FindRadius * FindRadius ? npc : null;
            }
            if (_targetType == 2 && NetworkMobManager.Instance != null)
            {
                Entities.MobController best = null;
                float bestSqr = float.MaxValue;
                foreach (int tpl in _targetTpls)   // con gần nhất trong mọi loại quái hợp lệ
                {
                    var m = NetworkMobManager.Instance.FindNearestAlive(from, FindRadius, tpl);
                    if (m == null) continue;
                    float sqr = ((Vector2)(m.transform.position - from)).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; best = m; }
                }
                return best;
            }
            return null;
        }

        private static PlayerMovement Me() => NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
    }
}
