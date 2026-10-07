using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Assets.Script.UI
{
    public class UISetup : MonoBehaviour
    {
        public static UISetup Instance { get; private set; }

        [Header("UI Elements - Avatar")]
        public Image avatarSlot;

        [Header("UI Elements - Stats Player")]
        public Image hpFillImage;  // Image Type: Filled
        public Image mpFillImage;  // Image Type: Filled
        public TMP_Text hpText;
        public TMP_Text mpText;

        [Header("UI Elements - Target")]
        public GameObject targetPanel;
        public GameObject targetHPOnly;
        public TMP_Text hpTextTarget;
        public Image targetHpFill;
        public TMP_Text targetName;

        [Header("Assets References")]
        [Tooltip("[CẦN ĐIỀN] Kéo Assets/Atlas/HUD.spriteatlasv2 (chứa ảnh avatar img2713/img2768/img2716 theo hệ 1/2/3)")]
        public AssetReferenceT<SpriteAtlas> avatarAtlasRef;

        // Biến lưu trữ Handle để quản lý vòng đời Addressables trong Single Scene
        private AsyncOperationHandle<SpriteAtlas> _atlasHandle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            // Khung mục tiêu bật sẵn trong scene → ẩn đi, chỉ hiện khi đã chọn quái/người (PlayerTargeting.SetTarget)
            HideTarget();
        }

        // ===============================================
        // QUẢN LÝ AVATAR (FIX LỖI ADDRESSABLES)
        // ===============================================
        public void SetupAvatar(int classId)
        {
            if (classId <= 0) return;

            string avatarSpriteName = classId switch
            {
                1 => "img2713",
                2 => "img2768",
                3 => "img2716",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(avatarSpriteName))
            {
                Debug.LogWarning($"Chưa cấu hình avatar cho classId: {classId}");
                return;
            }

            // 1. Nếu Handle đã load thành công trước đó
            if (_atlasHandle.IsValid() && _atlasHandle.Status == AsyncOperationStatus.Succeeded)
            {
                ApplySpriteFromAtlas(_atlasHandle.Result, avatarSpriteName);
                return;
            }

            // 2. Nếu Handle đang lỗi hoặc không hợp lệ, giải phóng để load lại
            if (_atlasHandle.IsValid() && _atlasHandle.Status == AsyncOperationStatus.Failed)
            {
                Addressables.Release(_atlasHandle);
            }

            // 3. Chỉ Load nếu Handle chưa được khởi tạo hoặc đã bị Release
            if (!_atlasHandle.IsValid())
            {
                _atlasHandle = avatarAtlasRef.LoadAssetAsync<SpriteAtlas>();
                _atlasHandle.Completed += (handle) =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        ApplySpriteFromAtlas(handle.Result, avatarSpriteName);
                    }
                    else
                    {
                        Debug.LogError("Tải Sprite Atlas thất bại!");
                    }
                };
            }
        }

        private void ApplySpriteFromAtlas(SpriteAtlas atlas, string spriteName)
        {
            if (atlas == null || avatarSlot == null) return;

            Sprite avatarSprite = atlas.GetSprite(spriteName);
            if (avatarSprite != null)
            {
                avatarSlot.sprite = avatarSprite;
            }
            else
            {
                Debug.LogWarning($"Không tìm thấy sprite tên {spriteName} trong Atlas!");
            }
        }

        // ===============================================
        // QUẢN LÝ STATS PLAYER
        // ===============================================
        public void SetupHealth(int currentHp, int maxHp, int currentMp, int maxMp)
        {
            if (maxHp <= 0) maxHp = 1;
            if (maxMp <= 0) maxMp = 1;

            if (hpText != null) hpText.text = $"{currentHp}/{maxHp}";
            if (mpText != null) mpText.text = $"{currentMp}/{maxMp}";

            if (hpFillImage != null) hpFillImage.fillAmount = (float)currentHp / maxHp;
            if (mpFillImage != null) mpFillImage.fillAmount = (float)currentMp / maxMp;
        }

        // ===============================================
        // QUẢN LÝ TARGET UI
        // ===============================================
        public void ShowTarget(string name, int hp, int maxhp)
        {
            if (targetPanel != null) targetPanel.SetActive(true);
            if (targetHPOnly != null) targetHPOnly.SetActive(true);

            if (targetName != null) targetName.text = name;
            UpdateTargetHP(hp, maxhp);
        }

        public void HideTarget()
        {
            if (targetPanel != null) targetPanel.SetActive(false);
        }

        public void ShowTargetNameOnly(string name)
        {
            if (targetPanel != null) targetPanel.SetActive(true);
            if (targetHPOnly != null) targetHPOnly.SetActive(false);
            if (targetName != null) targetName.text = name;
            if (hpTextTarget != null) hpTextTarget.text = string.Empty;
        }

        public void UpdateTargetHP(int currentHP, int maxHp)
        {
            if (maxHp <= 0) maxHp = 1;

            if (hpTextTarget != null) hpTextTarget.text = $"{currentHP}/{maxHp}";
            if (targetHpFill != null) targetHpFill.fillAmount = (float)currentHP / maxHp;
        }

        // ===============================================
        // DỌN DẸP KHI LOGOUT / DISCONNECT (CHO SINGLE SCENE)
        // ===============================================
        public void ClearUI()
        {
            // Giải phóng handle Addressables
            if (_atlasHandle.IsValid())
            {
                Addressables.Release(_atlasHandle);
            }

            // Reset UI về trạng thái trống
            if (avatarSlot != null) avatarSlot.sprite = null;
            if (hpText != null) hpText.text = "0/0";
            if (mpText != null) mpText.text = "0/0";
            if (hpFillImage != null) hpFillImage.fillAmount = 0;
            if (mpFillImage != null) mpFillImage.fillAmount = 0;

            HideTarget();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Giải phóng handle khi script thực sự bị destroy
            if (_atlasHandle.IsValid())
            {
                Addressables.Release(_atlasHandle);
            }
        }
    }
}