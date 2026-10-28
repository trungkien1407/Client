using UnityEngine;
using Assets.Script.Models;
using Assets.Script.Constants;
using Assets.Script.Player;
using Assets.Script.Network;
using Assets.Script.Manager;

namespace Assets.Script.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(PlayerVisualController))]
    public class PlayerMovement : MonoBehaviour
    {
        public PlayerData myData;

        [Header("Components")]
        private Rigidbody2D rb;
        private BoxCollider2D col;
        private PlayerVisualController visualCtrl;

        [Header("Movement Stats")]
        private float moveSpeed;
        private float jumpForce;

        private float baseMoveSpeed;
        private float baseGravity;

        [Header("Networking")]
        public float syncInterval = 0.05f;
        private float lastSyncTime;
        private int lastSentState = -1;
        private int lastSentDir = -1;

        private Vector2 lastSentPos;
        private float lastVelocityY;
        private const float MIN_MOVE_DIST_SQR = 0.0001f;

        private PlayerControls controls;
        private Vector2 moveInput;
        /// <summary>Tự chạy (ActionInput — Enter khi mục tiêu ở xa): -1 trái · 0 tắt · 1 phải. Bấm phím di chuyển là thôi tự chạy.</summary>
        [System.NonSerialized] public float AutoMoveX;
        /// <summary>Hướng người chơi đang tự bấm (phím / joystick) — ActionInput đọc để huỷ tự chạy.</summary>
        public Vector2 ManualInput { get; private set; }
        public bool IsGrounded => isGrounded;
        // Chết → khoá điều khiển + ngừng gửi PLAYER_MOVE. Đọc cờ chung (CombatNetwork đặt khi PLAYER_DIE / REVIVE).
        private static bool isDead => Assets.Script.Player.LocalPlayerState.IsDead;
        private bool isGrounded;
        private bool isFacingLeft;

        // ---- NƯỚC (object "Water" của map: TilemapCollider2D Is Trigger + tag "Water" — Tools → Naruto → Map → 5) ----
        // Đang chạy vào nước → ĐI TRÊN MẶT NƯỚC (ninja dồn chakra xuống chân). Đứng yên quá sinkDelay → CHÌM: chậm lại, nặng nề;
        // trong nước vẫn nhảy được để lên bờ. Server coi ô nước là "đứng được" (không bắt bay); quái thì không bước lên nước.
        [Header("Water Settings")]
        public float sinkDelay = 0.6f;          // đứng yên trên mặt nước chừng này giây thì chìm
        public float waterSpeedDebuff = 0.5f;   // tốc độ khi đã chìm
        private bool isInWater = false;
        private bool isOnWaterSurface = false;
        private float timeStandingStill = 0f;
        private float waterSurfaceY = 0f;
        private UnityEngine.Tilemaps.Tilemap waterMap;   // tilemap nước đang ở trong (để biết dưới chân còn nước không)
        private bool isJumping = false;

        // Bục 1 chiều: collider đang đứng lên + hạn bỏ qua va chạm khi bấm xuống để rơi xuyên
        private Collider2D groundCollider;
        private Collider2D droppingThrough;
        private float dropUntil;
        private const float DROP_TIME = 0.35f;      // bỏ va chạm tối thiểu
        private const float DROP_MAX_TIME = 1.5f;   // tối đa (còn kẹt trong bục quá lâu thì vẫn bật lại)

        /// <summary>Đang diễn đòn → đứng yên, không nhảy; hết đòn tự đi tiếp.</summary>
        public bool IsActionLocked => visualCtrl != null && visualCtrl.IsActionLocked;
        // Input tạo 1 lần, bật/tắt theo object, Dispose khi huỷ (tránh rò bộ nhớ)
        private void Awake()
        {
            controls = new PlayerControls();
            controls.Player.Jump.performed += ctx => OnJump();
        }

        private void OnEnable() => controls?.Enable();
        private void OnDisable() => controls?.Disable();
        private void OnDestroy() => controls?.Dispose();

        private bool initialized;

        // Đặt sẵn trong scene thử (Design.unity) thì tự khởi tạo mặc định. Nhân vật thật được NetworkPlayerManager
        // AddComponent rồi Initialize(dữ liệu server) NGAY — Start chạy frame sau, không được ghi đè lại (trước đây ghi đè
        // tốc chạy / lực nhảy / trọng lực của server bằng mặc định).
        private void Start()
        {
            if (!initialized) Initialize(new PlayerData(), 1);
        }

        public void Initialize(PlayerData dataFromServer, int dir)
        {
            initialized = true;
            this.myData = dataFromServer;

            this.baseMoveSpeed = myData.moveSpeed > 0 ? myData.moveSpeed : 6f;
            this.jumpForce = myData.jumpForce > 0 ? myData.jumpForce : 15f;
            this.baseGravity = myData.gravity > 0 ? myData.gravity : 3f; // khớp GamePlayConfig.DEFAULT_GRAVITY bên server

            this.moveSpeed = baseMoveSpeed;

            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<BoxCollider2D>();
            visualCtrl = GetComponent<PlayerVisualController>();

            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.gravityScale = baseGravity;
            }

            isFacingLeft = (dir == 1);
            visualCtrl.SetFlip(isFacingLeft);

            lastSentPos = transform.position;
            lastVelocityY = 0f;
        }

        void Update()
        {
            if (controls == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            // Chết, đang gõ chat, bị choáng hoặc đang mở cửa sổ (túi, shop...) -> không nhận phím di chuyển
            bool blocked = isDead || Assets.Script.Combat.ChatBox.IsTyping || Assets.Script.Player.LocalPlayerState.IsStunned
                           || Assets.Script.UI.Kit.UIRoot.GameplayBlocked;
            ManualInput = blocked ? Vector2.zero : controls.Player.Move.ReadValue<Vector2>();
            moveInput = ManualInput;
            if (blocked) AutoMoveX = 0f;
            else if (Mathf.Abs(moveInput.x) < 0.01f && AutoMoveX != 0f) moveInput.x = AutoMoveX;   // không tự bấm → đi theo tự chạy
            if (IsActionLocked) moveInput.x = 0f;
            if (isOnWaterSurface)
            {
                if (Mathf.Abs(moveInput.x) > 0.01f)
                {
                    timeStandingStill = 0f;
                }
                else
                {
                    timeStandingStill += Time.deltaTime;
                    if (timeStandingStill >= sinkDelay)
                    {
                        SinkIntoWater();
                    }
                }
            }

            if (isDead) return;
            HandleDropThrough();
            UpdateVisuals();
            HandleNetworkSync();
        }

        private void HandleDropThrough()
        {
            // Chỉ bật lại va chạm khi thân đã RA KHỎI bục (bật lại lúc còn đè lên bục → vật lý đẩy ngược lên mặt bục)
            if (droppingThrough != null && Time.time >= dropUntil && (!StillInside(droppingThrough) || Time.time >= dropUntil + DROP_MAX_TIME))
            {
                Physics2D.IgnoreCollision(col, droppingThrough, false);
                droppingThrough = null;
            }
            if (moveInput.y < -0.5f) TryDropThrough();
        }

        /// <summary>Đang đứng trên bục 1 chiều → tạm bỏ va chạm với bục để rơi xuyên qua (bấm xuống).</summary>
        public bool TryDropThrough()
        {
            if (droppingThrough != null || !isGrounded || groundCollider == null) return false;
            if (!groundCollider.TryGetComponent<PlatformEffector2D>(out _)) return false;

            droppingThrough = groundCollider;
            dropUntil = Time.time + DROP_TIME;
            Physics2D.IgnoreCollision(col, droppingThrough, true);
            isGrounded = false;
            groundCollider = null;
            // Đứng yên lâu thì Rigidbody2D "ngủ": bỏ va chạm xong vẫn treo trên bục, không rơi (lỗi "không nhảy xuống được").
            // Đánh thức + đẩy nhẹ xuống để rơi ngay.
            if (rb != null)
            {
                rb.WakeUp();
                rb.velocity = new Vector2(rb.velocity.x, Mathf.Min(rb.velocity.y, -2f));
            }
            return true;
        }

        /// <summary>Thân nhân vật còn đè lên collider c không.</summary>
        private bool StillInside(Collider2D c)
        {
            if (c == null || col == null) return false;
            var d = col.Distance(c);
            return d.isValid && d.isOverlapped;
        }

        /// <summary>
        /// Server ép vị trí (FORCE_MOVE / hồi sinh / đổi map): đặt qua Rigidbody2D + xoá vận tốc
        /// (chỉ gán transform thì vật lý kéo đi tiếp), coi như "đã gửi" để khỏi gửi ngược.
        /// </summary>
        public void SnapTo(Vector2 pos)
        {
            if (rb != null)
            {
                rb.position = pos;
                rb.velocity = Vector2.zero;
            }
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
            lastSentPos = pos;
            lastVelocityY = 0f;
        }

        /// <summary>Chết: khoá điều khiển. Hồi sinh: mở lại.</summary>
        public void SetDead(bool dead)
        {
            if (dead && rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
            if (!dead) lastSentState = -1; // ép gửi 1 gói trạng thái mới sau khi sống lại
        }

        /// <summary>Nhảy từ code (tự chạy gặp bậc / bục cao).</summary>
        public void Jump() => OnJump();

        void OnJump()
        {
            if (isDead || Assets.Script.Combat.ChatBox.IsTyping || Assets.Script.Player.LocalPlayerState.IsStunned || IsActionLocked
                || Assets.Script.UI.Kit.UIRoot.GameplayBlocked) return;
            // Giữ xuống + nhảy trên bục 1 chiều = nhảy xuống (kiểu NSO)
            if (ManualInput.y < -0.5f && TryDropThrough()) return;
            if (isGrounded || isOnWaterSurface || isInWater)
            {
                if (isOnWaterSurface)
                {
                    // Nhảy từ mặt nước: thoát hẳn trạng thái nước (thân đã ở trên trigger — không có sự kiện rời nữa,
                    // để isInWater thì nhảy tiếp giữa không trung được). Rơi lại xuống nước → OnTriggerEnter2D tính lại.
                    ExitWater();
                    rb.gravityScale = baseGravity;
                }

                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                isGrounded = false;
                isJumping = true;
            }
        }

        void FixedUpdate()
        {
            if (rb == null) return;

            if (isOnWaterSurface)
            {
                rb.gravityScale = 0f;
                rb.velocity = new Vector2(moveInput.x * moveSpeed * LocalPlayerState.SpeedFactor, 0f);

                float offsetToFeet = rb.position.y - col.bounds.min.y;
                rb.position = new Vector2(rb.position.x, waterSurfaceY + offsetToFeet + 0.01f);   // chân ngang mặt nước — ra bờ không vướng góc
                if (!WaterBelowFeet()) ExitWater();   // đã đi hết hồ (lên bờ / ra khoảng trống) → đi bình thường
            }
            else
            {
                rb.gravityScale = baseGravity;
                rb.velocity = new Vector2(moveInput.x * moveSpeed * LocalPlayerState.SpeedFactor, rb.velocity.y);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Water"))
            {
                isInWater = true;
                waterMap = collision.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                waterSurfaceY = WaterTopAt(collision, col.bounds.center.x, col.bounds.min.y);
                float playerFeetY = col.bounds.min.y;

                if (playerFeetY >= waterSurfaceY - 0.3f && Mathf.Abs(moveInput.x) > 0.01f)
                {
                    isOnWaterSurface = true;
                    timeStandingStill = 0f;
                }
                else
                {
                    SinkIntoWater();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Water"))
            {
                // Đi trên mặt nước: chân ngang mặt nước nên thân có thể vừa "rời" trigger dù vẫn đang ở trên hồ → bỏ qua,
                // FixedUpdate tự thôi đi trên nước khi dưới chân hết ô nước.
                if (isOnWaterSurface && WaterBelowFeet()) return;
                ExitWater();
            }
        }

        /// <summary>
        /// Mặt nước tại cột x: đi từ ô nước gần chân lên tới ô nước trên cùng liền nhau → mép trên ô đó.
        /// Không phải Tilemap (vùng nước vẽ bằng collider thường) → đỉnh khung collider.
        /// </summary>
        private static float WaterTopAt(Collider2D water, float x, float feetY)
        {
            var tm = water.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (tm == null) return water.bounds.max.y;
            var cell = tm.WorldToCell(new Vector3(x, feetY - 0.1f, 0f));
            if (!tm.HasTile(cell)) cell = tm.WorldToCell(new Vector3(x, feetY + 0.5f, 0f));   // vừa chạm mép trên
            if (!tm.HasTile(cell)) return water.bounds.max.y;
            while (tm.HasTile(cell + Vector3Int.up)) cell += Vector3Int.up;
            return tm.CellToWorld(cell + Vector3Int.up).y;
        }

        /// <summary>Ngay dưới chân (giữa thân) còn ô nước không.</summary>
        private bool WaterBelowFeet()
        {
            if (waterMap == null) return true;   // vùng nước không phải tilemap → để trigger tự lo
            return waterMap.HasTile(waterMap.WorldToCell(new Vector3(col.bounds.center.x, waterSurfaceY - 0.5f, 0f)));
        }

        private void SinkIntoWater()
        {
            isOnWaterSurface = false;
            moveSpeed = baseMoveSpeed * waterSpeedDebuff;
            rb.drag = 2f;
        }

        private void ExitWater()
        {
            waterMap = null;
            isInWater = false;
            isOnWaterSurface = false;
            moveSpeed = baseMoveSpeed;
            rb.drag = 0f;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (rb != null && rb.velocity.y > 0.1f) return;

            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    isGrounded = true;
                    groundCollider = collision.collider;
                    break;
                }
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            isGrounded = false;
            if (collision.collider == groundCollider) groundCollider = null;
        }

        void UpdateVisuals()
        {
            if (visualCtrl == null) return;

            if (isGrounded || isOnWaterSurface)
            {
                isJumping = false;

                if (Mathf.Abs(rb.velocity.x) > 0.1f) visualCtrl.PlayAnimation(AnimNames.Run, true);
                else visualCtrl.PlayAnimation(AnimNames.Idle, true);
            }
            else
            {
                if (isJumping)
                {
                    visualCtrl.PlayAnimation(AnimNames.Fall, true);
                }
                else
                {
                    visualCtrl.PlayAnimation(AnimNames.FallDown, true);
                }
            }

            if (moveInput.x > 0.1f) isFacingLeft = false;
            else if (moveInput.x < -0.1f) isFacingLeft = true;

            visualCtrl.SetFlip(isFacingLeft);
        }

        /// <summary>Gửi PLAYER_MOVE khi đổi trạng thái/hướng, lúc lên đỉnh cú nhảy, hoặc mỗi syncInterval nếu có di chuyển.</summary>
        void HandleNetworkSync()
        {
            MoveState currentState;
            if (!isGrounded && !isOnWaterSurface) currentState = rb.velocity.y > 0.1f ? MoveState.JumpUp : MoveState.Fall;
            else currentState = Mathf.Abs(rb.velocity.x) > 0.1f ? MoveState.Run : MoveState.Idle;

            byte currentDir = (byte)(isFacingLeft ? 1 : 0);
            bool reachedApex = (!isGrounded && !isOnWaterSurface && lastVelocityY > 0 && rb.velocity.y <= 0);
            lastVelocityY = rb.velocity.y;

            bool stateOrDirChanged = ((int)currentState != lastSentState || currentDir != lastSentDir);
            float sqrDist = ((Vector2)transform.position - lastSentPos).sqrMagnitude;
            bool positionChanged = sqrDist > MIN_MOVE_DIST_SQR;

            if (stateOrDirChanged || reachedApex || (Time.time - lastSyncTime > syncInterval && positionChanged))
            {
                SendMovePacket(currentState, currentDir);
            }
        }

        void SendMovePacket(MoveState state, byte dir)
        {
            float sendY = transform.position.y;
            if (isGrounded || isOnWaterSurface) sendY += 0.05f;   // nhấc nhẹ khỏi mặt đất để server không coi là lún
            Assets.Script.Data.GameActions.Move(transform.position.x, sendY, dir, state);

            lastSentState = (int)state;
            lastSentDir = dir;
            lastSentPos = transform.position;
            lastSyncTime = Time.time;
        }
        private const float FaceDeadZone = 0.05f;

        /// <summary>Quay mặt về phía targetPos (chỉ trục X). Ghi vào isFacingLeft → UpdateVisuals giữ hướng, HandleNetworkSync gửi hướng mới.</summary>
        public void FaceTowards(Vector3 targetPos)
        {
            float dx = targetPos.x - transform.position.x;
            if (Mathf.Abs(dx) < FaceDeadZone) return;
            isFacingLeft = dx < 0f;
            visualCtrl.SetFlip(isFacingLeft);
        }
    }
}
