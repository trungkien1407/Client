using UnityEngine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using Assets.Script.Manager;

namespace Assets.Script.Player
{
    public class PlayerVisualController : MonoBehaviour, Fx.IAuraBody
    {
        [Header("Visual Components")]
        [SerializeField] private GameObject mainVisualObj;      // Object chứa SkeletonAnimation nhân vật
        [Assets.Script.Core.Optional, SerializeField] private GameObject jumpFXObj;          // Object chứa SkeletonAnimation hiệu ứng nhảy (để trống nếu chưa dùng)
        [SerializeField] private GameObject deathVisualObj;     // Object chứa SpriteRenderer ảnh chết

        [Header("Spine Renderers")]
        public SkeletonAnimation mainSkeleton;
        [Assets.Script.Core.Optional] public SkeletonAnimation jumpFXSkeleton;                // (để trống nếu chưa dùng)
        public SpriteRenderer deathSprite;
        

        [Header("Settings")]
        public string idleAnimation = AnimNames.Idle;
        public string walkAnimation = AnimNames.Run;
        public string jumpAnimation = AnimNames.Jump;
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
        /// <summary>Hệ của nhân vật (1 Đấu sĩ = Naruto · 2 Hỗ trợ = Sakura · 3 Sát thủ = Sasuke) — chọn anim chiêu trong bảng SkillAnim.</summary>
        public int ClassType { get; private set; }

        public void SetupVisual(SkeletonDataAsset charData, Sprite customDeathImg = null, int classType = 0)
        {
            ClassType = classType;
            Fx.BodyAura.ResetOn(this, true);   // thân lấy lại từ pool: bỏ trạng thái + hào quang của người dùng trước
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
        public bool PlayActionOnce(string animName, float maxSeconds = 0.6f)
        {
            if (mainSkeleton == null || mainVisualObj == null || !mainVisualObj.activeSelf) return false;
            var anim = mainSkeleton.Skeleton?.Data?.FindAnimation(animName);
            if (anim == null) return false;

            mainSkeleton.AnimationState.SetAnimation(0, anim, false);
            mainSkeleton.AnimationState.AddAnimation(0, idleAnimation, true, 0f);
            _actionLockUntil = Time.time + Mathf.Min(anim.Duration, maxSeconds);
            return true;
        }

        /// <summary>
        /// Bỏ ngang động tác đang diễn (đổi map / khu): về đứng yên ngay + trả mọi slot Spine về mặc định (tắt attachment FX của chiêu).
        /// Không gọi thì: nhân vật bị ẩn lúc tải map → anim chiêu dừng giữa chừng → hiện lại ở map mới diễn tiếp phần còn lại
        /// (VD lốc xoáy đỏ của Trảm Phong "đi theo" sang map mới).
        /// </summary>
        public void CancelAction()
        {
            _actionLockUntil = 0f;
            if (mainSkeleton == null || mainSkeleton.AnimationState == null) return;
            mainSkeleton.AnimationState.ClearTracks();
            mainSkeleton.Skeleton?.SetToSetupPose();
            mainSkeleton.AnimationState.SetAnimation(0, idleAnimation, true);
        }

        // ---- HÀNH ĐỘNG → ANIM (dùng chung cho mình và người khác) ----
        // Mỗi chiêu diễn ĐÚNG anim chiêu trong bộ Spine của nhân vật — các anim SkillN / JutsuN đã VẼ SẴN hiệu ứng (lửa, khói,
        // đường chém, hào quang... là attachment FX/... trong Spine), nên không phát thêm hiệu ứng ra chiêu theo dữ liệu (HasSkillAnim).
        // Đòn cơ bản (chiêu 1 / 2 / 3) đấm – đá luân phiên (Punch_Combo / Kick_Combo cũng có hiệu ứng trúng đòn sẵn).
        // Anim chưa dùng (để dành chiêu mới): Naruto Skill20/40/50/80/100 · Sakura Skill10/20/40/50/80 · Sasuke Skill20/40*/80*/100*.
        private static readonly HashSet<int> BasicSkills = new HashSet<int> { 1, 2, 3 };
        private static readonly Dictionary<(int cls, int skill), string> SkillAnim = new Dictionary<(int, int), string>
        {
            // Đấu sĩ — Naruto
            { (1, 6), "Skill10" },    // Trảm Phong: chém mạnh diện rộng
            { (1, 10), "Jutsu1" },    // Thiết Thể: hào quang gồng mình
            { (1, 4), "Jutsu2" },     // Phân Thân Chi Thuật
            // Hỗ trợ — Sakura
            { (2, 7), "Jutsu1" },     // Y Thuật: Hồi Phục — tay phát sáng
            { (2, 9), "Skill30" },    // Cổ Vũ
            { (2, 4), "Jutsu2" },     // Phân Thân Chi Thuật
            // Sát thủ — Sasuke
            { (3, 8), "Skill10" },    // Ám Sát: nhát chém
            { (3, 11), "Skill50" },   // Ảnh Bộ: mở mắt, hoà vào bóng tối
            { (3, 4), "Jutsu1" },     // Phân Thân Chi Thuật: khói
        };
        private static readonly string[] Combo = { AnimNames.Punch, AnimNames.Kick };
        private int _comboStep;

        /// <summary>Tên anim Spine của chiêu cho hệ {cls} (null = đòn cơ bản / chưa có anim riêng).</summary>
        public static string SkillAnimFor(int cls, int skillId) => SkillAnim.TryGetValue((cls, skillId), out var n) ? n : null;

        /// <summary>Chiêu này có anim riêng (kèm hiệu ứng vẽ sẵn) trong bộ Spine đang dùng → không phát hiệu ứng ra chiêu theo dữ liệu.</summary>
        public bool HasSkillAnim(int skillId)
        {
            string n = SkillAnimFor(ClassType, skillId);
            return n != null && mainSkeleton != null && mainSkeleton.Skeleton?.Data?.FindAnimation(n) != null;
        }

        /// <summary>
        /// Người ra chiêu đã có hiệu ứng vẽ sẵn trong Spine → KHÔNG phát hiệu ứng ra chiêu (fxCast) theo dữ liệu:
        /// chiêu có anim riêng, hoặc đòn cơ bản (đấm / đá — fxCast của đòn cơ bản là đường chém kiếm xanh, không hợp nắm đấm).
        /// Hiệu ứng trúng đòn ở mục tiêu (fxHit) vẫn phát.
        /// </summary>
        public bool HasBuiltInCastFx(int skillId) => HasSkillAnim(skillId)
            || (BasicSkills.Contains(skillId) && mainSkeleton != null && mainSkeleton.Skeleton?.Data?.FindAnimation(AnimNames.Punch) != null);

        /// <summary>Diễn anim của chiêu skillId (bấm đánh, kể cả khi đòn không gửi lên server).</summary>
        public void PlaySkill(int skillId)
        {
            string name = BasicSkills.Contains(skillId) ? null : SkillAnimFor(ClassType, skillId);
            // Anim chiêu dài 1–2 giây (hiệu ứng vẽ sẵn chạy hết), chỉ khoá anim di chuyển tối đa 0,9 giây cho đỡ cứng tay
            if (name != null && PlayActionOnce(name, 0.9f)) return;
            name = Combo[_comboStep++ % Combo.Length];
            if (PlayActionOnce(name, 0.45f)) return;
            PlayActionOnce(AnimNames.Punch, 0.45f);
        }

        // ---- PHÂN THÂN (né tránh): nhân vật mờ, nhấp nháy nhẹ tới hết hiệu ứng ----
        private float _ghostUntil;

        /// <summary>Làm mờ nhân vật {seconds} giây (EFFECT phân thân — mình và người khác đều thấy).</summary>
        public void Ghost(float seconds) => _ghostUntil = Mathf.Max(_ghostUntil, Time.time + seconds);

        private void Update()
        {
            if (mainSkeleton == null || mainSkeleton.Skeleton == null) return;
            float a = Time.time < _ghostUntil && !isdead ? 0.45f + 0.15f * Mathf.Sin(Time.time * 12f) : 1f;
            if (!Mathf.Approximately(mainSkeleton.Skeleton.A, a)) mainSkeleton.Skeleton.A = a;
            var sk = mainSkeleton.Skeleton;
            if (sk.R != _statusTint.r || sk.G != _statusTint.g || sk.B != _statusTint.b) { sk.R = _statusTint.r; sk.G = _statusTint.g; sk.B = _statusTint.b; }
        }

        // ---- HIỆU ỨNG TRÊN THÂN (Fx.BodyAura vẽ choáng / chậm / bỏng / hào quang, hỏi thân qua các hàm này) ----
        private Color _statusTint = Color.white;
        private Collider2D _box;

        public bool AuraHidden => isdead || mainVisualObj == null || !mainVisualObj.activeInHierarchy;
        public Renderer MainRenderer => mainSkeleton != null ? mainSkeleton.GetComponent<MeshRenderer>() : null;
        public void SetStatusTint(Color c) => _statusTint = c;   // Update áp vào màu Spine (RGB), alpha vẫn do Ghost

        /// <summary>Khung thân = BoxCollider2D (0,6 × 1,6, gốc ở chân); không có thì ước lượng.</summary>
        public Bounds BodyBounds
        {
            get
            {
                if (_box == null) TryGetComponent(out _box);
                if (_box != null && _box.enabled) return _box.bounds;
                return new Bounds(transform.position + Vector3.up * 0.8f, new Vector3(0.6f, 1.6f, 0f));
            }
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
                Fx.BodyAura.ResetOn(this);   // chết → hết choáng / chậm / bỏng (hào quang giữ, tự ẩn khi chết)
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