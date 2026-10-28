using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Assets.Script.Player
{
    public class PlayerTargeting : MonoBehaviour
    {
        [Header("Targeting Settings")]
        public ITargetable currentTarget;
        [Tooltip("[CẦN ĐIỀN] Chọn các Layer chứa quái/người/NPC có Collider2D (click chuột & phím Tab chỉ bắt được object thuộc layer này)")]
        public LayerMask targetableLayer;

       
        private Transform playerTransform;

        [Tooltip("Khoảng cách tối đa trước khi tự động mất mục tiêu")]
        public float maxTargetDistance = 6f;

        [Tooltip("Bán kính tìm kiếm mục tiêu khi ấn Tab")]
        public float tabSearchRadius = 5f;

        // ---- TỰ CHỌN MỤC TIÊU (kiểu NSO) ----
        // Chưa chọn ai / quái đang chọn chết / ra khỏi tầm → tự chuyển sang quái sống gần nhất.
        [Tooltip("Tự chọn quái gần nhất trong bán kính này")]
        public float autoTargetRadius = 6f;
        public bool autoTarget = true;
        private const float AutoScanInterval = 0.25f;   // không quét mỗi khung hình
        private const float CancelPause = 5f;           // bấm bỏ chọn → tạm ngừng tự chọn chừng này giây
        private bool _autoPicked;                       // mục tiêu hiện tại do máy chọn (người bấm chọn thì giữ xa hơn)
        private float _nextAutoScan, _autoPausedUntil;

        /// <summary>Mục tiêu hiện tại do máy tự chọn (AutoTestRunner đọc).</summary>
        public bool IsAutoPicked => currentTarget != null && _autoPicked;

        private PlayerControls controls;

        private void Awake()
        {
            controls = new PlayerControls();
            // Enter khi mục tiêu ở xa thì tự chạy lại (ActionInput) → giữ mục tiêu xa hơn trước (giá trị cũ 6 có thể nằm trong prefab)
            maxTargetDistance = Mathf.Max(maxTargetDistance, 12f);
        }

        private void Start()
        {
            // Nếu quên không kéo Player vào Inspector, tự động lấy GameObject hiện tại làm gốc
            if (playerTransform == null)
            {
                playerTransform = this.transform;
            }
        }

        public void SetPlayer(Transform newPlayer)
        {
            playerTransform = newPlayer;
        }

        private void OnEnable()
        {
            controls.Enable();
        }

        private void OnDisable()
        {
            controls?.Disable();
        }

        private void Update()
        {
            // Mục tiêu đã bị huỷ / trả về pool (quái biến mất, người rời khu, đổi map...) → bỏ chọn
            if (currentTarget != null && (!(currentTarget is MonoBehaviour mb) || mb == null || !mb.gameObject.activeInHierarchy))
                ClearTarget();
            if (playerTransform == null) return;
            if (controls.Player.TargetClick.WasPerformedThisFrame())
            {
                SelectTargetFromMouse();
            }

            if (controls.Player.CancelTarget.WasPerformedThisFrame())
            {
                ClearTarget();
                _autoPausedUntil = Time.time + CancelPause;   // vừa bỏ chọn thì đừng chọn lại ngay
            }

            if (controls.Player.TabTarget.WasPerformedThisFrame())
            {
                TargetNextNearby();
            }

            CheckTargetDistance();
            AutoTargetTick();
        }

        // ==========================================
        // TỰ CHỌN / TỰ CHUYỂN MỤC TIÊU
        // ==========================================
        /// <summary>
        /// Quái do máy chọn: ra ngoài autoTargetRadius → đổi sang con gần hơn. Quái người chơi tự chọn: giữ tới maxTargetDistance
        /// (để Enter còn tự chạy lại). Đang chọn NPC / người → để yên. Không còn con nào khác → giữ con cũ (quá 12 ô thì CheckTargetDistance bỏ).
        /// </summary>
        private void AutoTargetTick()
        {
            if (!autoTarget || Time.time < _nextAutoScan) return;
            _nextAutoScan = Time.time + AutoScanInterval;
            if (Time.time < _autoPausedUntil || LocalPlayerState.IsDead || NetworkMobManager.Instance == null) return;
            if (Combat.ActionInput.IsRunning) return;   // đang tự chạy tới mục tiêu → không đổi giữa đường
            if (Combat.AutoPlay.IsOn) return;           // tự động tự chọn quái (AutoPlay) → không giành mục tiêu

            Vector3 me = playerTransform.position;
            if (currentTarget != null)
            {
                if (!(currentTarget is MobController mob)) return;
                float keep = _autoPicked ? autoTargetRadius : maxTargetDistance;
                if (mob != null && !mob.IsDead && mob.DistanceFrom(me) <= keep) return;   // vẫn ổn → giữ
            }

            var next = NetworkMobManager.Instance.FindNearestAlive(me, autoTargetRadius);
            if (next == null || ReferenceEquals(next, currentTarget)) return;
            SetTarget(next);
            _autoPicked = true;
        }

        // ==========================================
        // TỰ ĐỘNG HỦY MỤC TIÊU NẾU QUÁ XA
        // ==========================================
        private void CheckTargetDistance()
        {
            if (currentTarget == null) return;

            Component targetObj = currentTarget as Component;
            if (targetObj != null)
            {
                // Dùng vị trí của PLAYER (playerTransform) thay vì vị trí của Script
                Vector2 offset = targetObj.transform.position - playerTransform.position;
                float sqrDistance = offset.sqrMagnitude;

                if (sqrDistance > maxTargetDistance * maxTargetDistance)
                {
                    ClearTarget();
                }
            }
        }

        // ==========================================
        // XỬ LÝ CHUYỂN MỤC TIÊU BẰNG PHÍM TAB
        // ==========================================
        private void TargetNextNearby()
        {
            // Lấy tâm quét vòng tròn là vị trí của PLAYER
            Vector3 playerPos = playerTransform.position;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(playerPos, tabSearchRadius, targetableLayer);
            List<ITargetable> validTargets = new List<ITargetable>();

            foreach (var col in colliders)
            {
                // Bỏ qua nếu lỡ quét trúng bản thân Player
                if (col.gameObject == playerTransform.gameObject) continue;

                ITargetable targetable = col.GetComponent<ITargetable>();
                if (targetable != null)
                {
                    validTargets.Add(targetable);
                }
            }

            if (validTargets.Count == 0) return;

            // Sắp xếp mục tiêu dựa trên khoảng cách tới vị trí hiện tại của PLAYER
            validTargets.Sort((a, b) => {
                float sqrDistA = (((Component)a).transform.position - playerPos).sqrMagnitude;
                float sqrDistB = (((Component)b).transform.position - playerPos).sqrMagnitude;
                return sqrDistA.CompareTo(sqrDistB);
            });

            int currentIndex = validTargets.IndexOf(currentTarget);
            int nextIndex = (currentIndex + 1) % validTargets.Count;

            SetTarget(validTargets[nextIndex]);
        }

        // ==========================================
        // KIỂM TRA CLICK VÀO UI 
        // ==========================================
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                return EventSystem.current.IsPointerOverGameObject(touch.fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        // ==========================================
        // XỬ LÝ CHỌN MỤC TIÊU TỪ CHUỘT
        // ==========================================
        private void SelectTargetFromMouse()
        {
            if (IsPointerOverUI()) return;

            if (Pointer.current == null) return;
            Vector2 screenPos = Pointer.current.position.ReadValue();
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero, Mathf.Infinity, targetableLayer);
            bool foundTarget = false;

            foreach (var hit in hits)
            {
                if (hit.collider != null)
                {
                    // Tránh việc tự target bản thân
                    if (hit.collider.gameObject == playerTransform.gameObject) continue;

                    ITargetable targetable = hit.collider.GetComponent<ITargetable>();
                    if (targetable != null)
                    {
                        SetTarget(targetable);
                        foundTarget = true;
                        break;
                    }
                }
            }

            if (!foundTarget) ClearTarget();
        }

        // ==========================================
        // XỬ LÝ CHỌN MỤC TIÊU (HỖ TRỢ ĐA HÌNH)
        // ==========================================
        private void SetTarget(ITargetable newTarget)
        {
            if (currentTarget == newTarget) { _autoPicked = false; return; }   // bấm lại đúng con máy chọn = người chọn
            ClearTarget();
            _autoPicked = false;   // AutoTargetTick đặt lại true sau khi gọi

            currentTarget = newTarget;
            currentTarget.OnTargeted();

            string targetName = currentTarget.GetTargetName();

            if (currentTarget is IHasHealth healthTarget)
            {
                int hp = healthTarget.GetCurrentHp();
                int maxHp = healthTarget.GetMaxHp();

                if (UISetup.Instance != null)
                {
                    UISetup.Instance.ShowTarget(targetName, hp, maxHp);
                }

                healthTarget.OnHpChanged += HandleTargetHpChanged;
            }
            else
            {
                if (UISetup.Instance != null)
                {
                    UISetup.Instance.ShowTargetNameOnly(targetName);
                }
            }
        }

        public void ClearTarget()
        {
            if (currentTarget != null)
            {
                if (currentTarget is IHasHealth healthTarget)
                {
                    healthTarget.OnHpChanged -= HandleTargetHpChanged;
                }

                if (currentTarget is MonoBehaviour alive && alive != null) currentTarget.OnDeselected();   // đã huỷ thì thôi
                currentTarget = null;

                if (UISetup.Instance != null)
                {
                    UISetup.Instance.HideTarget();
                }
            }
        }

        /// <summary>Chọn mục tiêu từ code (vd bấm đánh khi chưa chọn ai -> tự chọn quái gần nhất).</summary>
        public void SelectTarget(ITargetable t)
        {
            if (t != null) SetTarget(t);
        }

        /// <summary>Mục tiêu đang chọn vừa chết/biến mất -> bỏ chọn (lần quét kế tiếp tự chọn con gần nhất).</summary>
        public void ClearIfTarget(ITargetable t)
        {
            if (currentTarget != null && ReferenceEquals(currentTarget, t)) { ClearTarget(); _nextAutoScan = 0f; }
        }

        private void HandleTargetHpChanged(int currentHp, int maxHp)
        {
            if (UISetup.Instance != null)
            {
                UISetup.Instance.UpdateTargetHP(currentHp, maxHp);
            }
        }
    }
}