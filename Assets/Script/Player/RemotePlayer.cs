using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Models;
using Assets.Script.Player;
using Assets.Script.UI;
using System;
using UnityEngine;

namespace Assets.Script.Player
{
    [RequireComponent(typeof(PlayerVisualController))] // Ép buộc phải có Component này
    public class RemotePlayer : MonoBehaviour, ITargetable, IHasHealth
    {
        public string poolKey;
        public PlayerData myData;

        private PlayerVisualController visualCtrl; // [MỚI]

        private Vector2 targetPos;
        private float moveSpeed;
        private byte currentState = 0;

        private const float TELEPORT_SQR_THRESHOLD = 25.0f;

        [Header("Thông tin HP")]
        public int currentHp;
        public int maxHp;

        [Header("Targeting UI")]
        private GameObject hpBarContainer;
        private Transform hpFillTransform;

        public void Initialize(PlayerData dataFromServer)
        {
            this.myData = dataFromServer;
            this.moveSpeed = myData.moveSpeed > 0 ? myData.moveSpeed : 6f;
            this.targetPos = new Vector2(myData.x, myData.y);
            transform.position = this.targetPos;

            this.maxHp = dataFromServer.maxHp;
            this.currentHp = dataFromServer.hp;

            visualCtrl = GetComponent<PlayerVisualController>(); // Lấy Component

            UIHealthBar hpUI = GetComponentInChildren<UIHealthBar>(true);
            if (hpUI != null)
            {
                hpBarContainer = hpUI.gameObject;
                hpFillTransform = hpUI.fillTransform;
                hpBarContainer.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[RemotePlayer] Không tìm thấy script 'UIHealthBar' bên trong Prefab {myData.id}!");
            }

            UpdateUI();
            Assets.Script.UI.NameTag.Attach(this); // tên + gia tộc trên đầu (màu theo PK)
        }

        public void UpdateNetworkData(float serverX, float serverY, byte dir, byte state)
        {
            Vector2 newTarget = new Vector2(serverX, serverY);

            // Bắt sự kiện vừa mới nhảy để bật FX
            if (state == 2 && currentState != 2)
            {
                // visualCtrl?.PlayJumpFX();
            }

            currentState = state;

            // Cập nhật hướng nhìn
            bool isLeft = (dir == 1);
            visualCtrl?.SetFlip(isLeft);

            if ((newTarget - (Vector2)transform.position).sqrMagnitude > TELEPORT_SQR_THRESHOLD)
            {
                transform.position = newTarget;
            }
            targetPos = newTarget;
        }

        void Update()
        {
            if (myData == null) return;

            Vector2 offset = targetPos - (Vector2)transform.position;
            float sqrDistance = offset.sqrMagnitude;

            // 1. Nếu khoảng cách đủ xa thì di chuyển mượt mà tới đó
            if (sqrDistance > 0.001f)
            {
                float currentSpeed = moveSpeed * 1.2f;
                if (sqrDistance > 1.0f) currentSpeed *= 1.5f;

                transform.position = Vector2.MoveTowards(transform.position, targetPos, currentSpeed * Time.deltaTime);
            }
            // 2. Nếu đã ở rất gần (sqrDistance <= 0.001f) mà vị trí chưa hoàn toàn khớp -> Ép dính luôn
            else if (transform.position != (Vector3)targetPos)
            {
                transform.position = targetPos;
            }

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (visualCtrl == null) return;

            if (currentState == 1) visualCtrl.PlayAnimation("Run", true);
            else if (currentState == 2 || currentState == 3) visualCtrl.PlayAnimation("Jump_Falling", true);

            else visualCtrl.PlayAnimation("Idle", true);
        }

        // ==========================================
        // HÀM CẬP NHẬT HP
        // ==========================================
        public void UpdateHp(int newHp)
        {
            if (visualCtrl != null && visualCtrl.isdead) return;

            currentHp = newHp;
            UpdateUI(); // Cập nhật thanh máu trên đỉnh đầu

            // [MỚI] Phát loa thông báo: "Máu tao đổi rồi nhé!"
            OnHpChanged?.Invoke(currentHp, maxHp);

            //   if (currentHp <= 0) Die();
        }

        /// <summary>Server báo người này chết (PLAYER_DIE) hoặc sống lại (REVIVE).</summary>
        public void SetDead(bool dead, int hp = 0)
        {
            visualCtrl?.SetDeathState(dead);
            currentHp = dead ? 0 : hp;
            UpdateUI();
            OnHpChanged?.Invoke(currentHp, maxHp);
        }

        public void SetMaxHp(int newMaxHp)
        {
            maxHp = newMaxHp;
            UpdateUI();
        }

        /// <summary>Server báo người này vừa ra chiêu → diễn anim của chiêu đó.</summary>
        public void PlayAttack(int skillId) => visualCtrl?.PlaySkill(skillId);

        /// <summary>Server đặt lại vị trí (REVIVE) -> nhảy thẳng tới, không trượt mượt.</summary>
        public void TeleportTo(float x, float y)
        {
            targetPos = new Vector2(x, y);
            transform.position = targetPos;
        }

        private void UpdateUI()
        {
            if (hpFillTransform != null && maxHp > 0)
            {
                hpFillTransform.localScale = new Vector3(Mathf.Clamp01((float)currentHp / maxHp), 1f, 1f);
            }
        }
        // ==========================================
        // HIỆN THỰC INTERFACE ITARGETABLE
        // ==========================================


        public event Action<int, int> OnHpChanged;
        public int GetId() => myData != null ? myData.id : -1;
        public TargetType GetTargetType() => TargetType.Player;
        public Transform GetTransform() => transform;
        public void OnTargeted() { if (hpBarContainer != null) hpBarContainer.SetActive(true); }
        public void OnDeselected() { if (hpBarContainer != null) hpBarContainer.SetActive(false); }
        public string GetTargetName() => myData.name;
        public int GetCurrentHp() => currentHp;
        public int GetMaxHp() => maxHp;

    }
}
