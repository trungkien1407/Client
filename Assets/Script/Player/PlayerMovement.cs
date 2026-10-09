using UnityEngine;
using Assets.Script.Models;
using Assets.Script.Constants;
using Assets.Script.Player;
using Assets.Script.Network;

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
    // Chết → khoá điều khiển + ngừng gửi PLAYER_MOVE. Đọc cờ chung (CombatNetwork đặt khi PLAYER_DIE / REVIVE).
    private static bool isDead => Assets.Script.Player.LocalPlayerState.IsDead;
    private bool isGrounded;
    private bool isFacingLeft;

    [Header("Water Settings")]
    public float sinkDelay = 0.2f;
    public float waterSpeedDebuff = 0.5f;
    private bool isInWater = false;
    private bool isOnWaterSurface = false;
    private float timeStandingStill = 0f;
    private float waterSurfaceY = 0f;
    private bool isJumping = false;

    // Bục 1 chiều: collider đang đứng lên + hạn bỏ qua va chạm khi bấm xuống để rơi xuyên
    private Collider2D groundCollider;
    private Collider2D droppingThrough;
    private float dropUntil;
    private const float DROP_TIME = 0.35f;

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
        this.baseGravity = myData.gravity > 0 ? myData.gravity : 3.5f;

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

        // Chết hoặc đang gõ chat -> không nhận phím di chuyển
        bool blocked = isDead || Assets.Script.Combat.ChatBox.IsTyping || Assets.Script.Player.LocalPlayerState.IsStunned;
        moveInput = blocked ? Vector2.zero : controls.Player.Move.ReadValue<Vector2>();

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
        if (droppingThrough != null && Time.time >= dropUntil)
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
        return true;
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

    void OnJump()
    {
        if (isDead || Assets.Script.Combat.ChatBox.IsTyping) return;
        if (isGrounded || isOnWaterSurface || isInWater)
        {
            if (isOnWaterSurface)
            {
                isOnWaterSurface = false;
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
            rb.velocity = new Vector2(moveInput.x * moveSpeed, 0f);

            float offsetToFeet = rb.position.y - col.bounds.min.y;
            rb.position = new Vector2(rb.position.x, waterSurfaceY + offsetToFeet - 0.05f);
        }
        else
        {
            rb.gravityScale = baseGravity;
            rb.velocity = new Vector2(moveInput.x * moveSpeed, rb.velocity.y);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Water"))
        {
            isInWater = true;
            waterSurfaceY = collision.bounds.max.y;
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
            ExitWater();
        }
    }

    private void SinkIntoWater()
    {
        isOnWaterSurface = false;
        moveSpeed = baseMoveSpeed * waterSpeedDebuff;
        rb.drag = 2f;
    }

    private void ExitWater()
    {
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

            if (Mathf.Abs(rb.velocity.x) > 0.1f) visualCtrl.PlayAnimation("Run", true);
            else visualCtrl.PlayAnimation("Idle", true);
        }
        else
        {
            if (isJumping)
            {
                visualCtrl.PlayAnimation("Jump_Falling", true);
            }
            else
            {
                visualCtrl.PlayAnimation("Jump_Down", true);
            }
        }

        if (moveInput.x > 0.1f) isFacingLeft = false;
        else if (moveInput.x < -0.1f) isFacingLeft = true;

        visualCtrl.SetFlip(isFacingLeft);
    }

    /// <summary>Gửi PLAYER_MOVE khi đổi trạng thái/hướng, lúc lên đỉnh cú nhảy, hoặc mỗi syncInterval nếu có di chuyển.</summary>
    void HandleNetworkSync()
    {
        byte currentState = 0;

        if (!isGrounded && !isOnWaterSurface)
        {
            if (rb.velocity.y > 0.1f) currentState = 2;
            else currentState = 3;
        }
        else if (Mathf.Abs(rb.velocity.x) > 0.1f) currentState = 1;
        else currentState = 0;

        byte currentDir = (byte)(isFacingLeft ? 1 : 0);
        bool reachedApex = (!isGrounded && !isOnWaterSurface && lastVelocityY > 0 && rb.velocity.y <= 0);
        lastVelocityY = rb.velocity.y;

        bool stateOrDirChanged = (currentState != lastSentState || currentDir != lastSentDir);
        float sqrDist = ((Vector2)transform.position - lastSentPos).sqrMagnitude;
        bool positionChanged = sqrDist > MIN_MOVE_DIST_SQR;

        if (stateOrDirChanged || reachedApex || (Time.time - lastSyncTime > syncInterval && positionChanged))
        {
            SendMovePacket(currentState, currentDir);
        }
    }

    void SendMovePacket(byte state, byte dir)
    {
        float sendY = transform.position.y;
        if (isGrounded || isOnWaterSurface) sendY += 0.05f;   // nhấc nhẹ khỏi mặt đất để server không coi là lún
        Assets.Script.Data.GameActions.Move(transform.position.x, sendY, dir, state);

        lastSentState = state;
        lastSentDir = dir;
        lastSentPos = transform.position;
        lastSyncTime = Time.time;
    }
}