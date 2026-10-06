using UnityEngine;
using Spine.Unity;
using System.Collections;

namespace Assets.Script.Player
{
    public class PlayerVisualController : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private GameObject mainVisualObj;      // Object chứa SkeletonAnimation nhân vật
        [SerializeField] private GameObject jumpFXObj;          // Object chứa SkeletonAnimation hiệu ứng nhảy (để trống nếu chưa dùng)
        [SerializeField] private GameObject deathVisualObj;     // Object chứa SpriteRenderer ảnh chết

        [Header("Spine Renderers")]
        public SkeletonAnimation mainSkeleton;
        public SkeletonAnimation jumpFXSkeleton;                // (để trống nếu chưa dùng)
        public SpriteRenderer deathSprite;
        

        [Header("Settings")]
        public string idleAnimation = "Idle";
        public string walkAnimation = "Run";
        public string jumpAnimation = "Jump";
        public string jumpFXAnimation = "play"; // Tên anim của cái Jump FX
        public bool isdead;

        private void Awake()
        {
            // Đảm bảo trạng thái ban đầu chuẩn
            if (jumpFXObj != null) jumpFXObj.SetActive(false);
            if (deathVisualObj != null) deathVisualObj.SetActive(false);
            if (mainVisualObj != null) mainVisualObj.SetActive(true);
        }

        /// <summary>
        /// Khởi tạo hình ảnh cho nhân vật (Gọi từ NetworkPlayerManager)
        /// </summary>
        public void SetupVisual(SkeletonDataAsset charData, Sprite customDeathImg = null)
        {
            // 1. Reset trạng thái sống
            if (mainVisualObj != null) mainVisualObj.SetActive(true);
            if (deathVisualObj != null) deathVisualObj.SetActive(false);
            if (jumpFXObj != null) jumpFXObj.SetActive(false);

            // 2. Nạp dữ liệu Spine cho nhân vật
            if (charData != null && mainSkeleton != null)
            {
                mainSkeleton.skeletonDataAsset = charData;
                mainSkeleton.initialSkinName = "Normal";
                mainSkeleton.Initialize(true);
                
                PlayAnimation(idleAnimation, true);
            }

            // 3. Nạp ảnh chết nếu có
            if (customDeathImg != null && deathSprite != null)
            {
                deathSprite.sprite = customDeathImg;
            }
        }

        #region Animation Logic

        // Đang diễn 1 động tác 1 lần (đánh, tung chiêu) -> tạm khoá anim di chuyển tới thời điểm này
        private float _actionLockUntil;

        public void PlayAnimation(string animName, bool loop)
        {
            if (mainSkeleton == null || mainVisualObj == null || !mainVisualObj.activeSelf) return;
            if (Time.time < _actionLockUntil) return;

            // Tránh chơi đè lại cùng 1 animation đang chạy
            if (mainSkeleton.AnimationName == animName) return;

            mainSkeleton.AnimationState.SetAnimation(0, animName, loop);
        }

        /// <summary>
        /// Diễn 1 động tác KHÔNG lặp (vd "Punch_Combo" khi đánh) rồi tự quay về Idle.
        /// Trong lúc diễn, các lệnh PlayAnimation của di chuyển bị bỏ qua.
        /// </summary>
        public void PlayActionOnce(string animName, float maxSeconds = 0.6f)
        {
            if (mainSkeleton == null || mainVisualObj == null || !mainVisualObj.activeSelf) return;
            var anim = mainSkeleton.Skeleton?.Data?.FindAnimation(animName);
            if (anim == null) return;

            mainSkeleton.AnimationState.SetAnimation(0, anim, false);
            mainSkeleton.AnimationState.AddAnimation(0, idleAnimation, true, 0f);
            _actionLockUntil = Time.time + Mathf.Min(anim.Duration, maxSeconds);
        }

        /// <summary>
        /// Kích hoạt hiệu ứng nhảy
        /// </summary>
        public void PlayJumpFX()
        {
            if (jumpFXObj == null || jumpFXSkeleton == null) return;

            jumpFXObj.SetActive(true);
            jumpFXSkeleton.AnimationState.SetAnimation(0, jumpFXAnimation, false);

            // Tự động tắt FX sau khi chạy xong để tiết kiệm tài nguyên
            StopAllCoroutines();
            StartCoroutine(DisableJumpFXAfterPlay());
        }

        private IEnumerator DisableJumpFXAfterPlay()
        {
            yield return new WaitForSeconds(0.8f);
            if (jumpFXObj != null) jumpFXObj.SetActive(false);
        }

        #endregion

        #region State Logic

        /// <summary>
        /// Chuyển sang trạng thái chết
        /// </summary>
        public void SetDeathState(bool isDead)
        {
            if (isDead)
            {
                if (mainVisualObj != null) mainVisualObj.SetActive(false);
                if (jumpFXObj != null) jumpFXObj.SetActive(false);
                if (deathVisualObj != null) deathVisualObj.SetActive(true);
                this.isdead = true;
            }
            else
            {
                this.isdead = false;
                // Hồi sinh
                if (mainVisualObj != null) mainVisualObj.SetActive(true);
                if (deathVisualObj != null) deathVisualObj.SetActive(false);
                PlayAnimation(idleAnimation, true);
            }
        }

        /// <summary>
        /// Đổi hướng nhìn (Flip) - Đã sửa lỗi ngược hướng!
        /// </summary>
        public void SetFlip(bool isLeft)
        {
            if (mainSkeleton != null && mainSkeleton.skeleton != null)
            {
                // Bản vẽ gốc quay Trái.
                // Nếu isLeft = true -> Cần quay trái -> ScaleX = 1 (Giữ nguyên gốc)
                // Nếu isLeft = false -> Cần quay phải -> ScaleX = -1 (Lật ngược gốc)
                mainSkeleton.skeleton.ScaleX = isLeft ? 1f : -1f;
            }
        }

        #endregion
    }
}