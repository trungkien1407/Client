using Assets.Script.Interfaces;
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
        public LayerMask targetableLayer;

       
        private Transform playerTransform;

        [Tooltip("Khoảng cách tối đa trước khi tự động mất mục tiêu")]
        public float maxTargetDistance = 6f;

        [Tooltip("Bán kính tìm kiếm mục tiêu khi ấn Tab")]
        public float tabSearchRadius = 5f;

        private PlayerControls controls;

        private void Awake()
        {
            controls = new PlayerControls();
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
            if (playerTransform == null) return;
            if (controls.Player.TargetClick.WasPerformedThisFrame())
            {
                SelectTargetFromMouse();
            }

            if (controls.Player.CancelTarget.WasPerformedThisFrame())
            {
                ClearTarget();
            }

            if (controls.Player.TabTarget.WasPerformedThisFrame())
            {
                TargetNextNearby();
            }

            CheckTargetDistance();
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
            if (currentTarget == newTarget) return;
            ClearTarget();

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

                currentTarget.OnDeselected();
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

        /// <summary>Mục tiêu đang chọn vừa chết/biến mất -> bỏ chọn.</summary>
        public void ClearIfTarget(ITargetable t)
        {
            if (currentTarget != null && ReferenceEquals(currentTarget, t)) ClearTarget();
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