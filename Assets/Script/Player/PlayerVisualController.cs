using Assets.Script.Manager;
using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        public bool IsActionLocked => Time.time < _actionLockUntil;
        /// <summary>Còn bao lâu nữa hết động tác đang diễn (giây, 0 = rảnh).</summary>
        public float ActionLockRemaining => Mathf.Max(0f, _actionLockUntil - Time.time);
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
            isdead = false;                     // cờ chết của người dùng trước (thanh máu đứng yên, ẩn hào quang)
            _ghostUntil = 0f;
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
                FxSet = Fx.CharFx.SetFor(charData);
                Fx.CharFx.Preload(FxSet);   // bộ hiệu ứng riêng (vật bay, nổ ở mục tiêu) — tải sẵn
                
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

        // Tốc độ riêng theo tên anim (khi không truyền tốc độ) — đấm / đá gốc chậm, chạy nhanh gấp đôi
        private static readonly Dictionary<string, float> AnimSpeeds = new()
        {
            { AnimNames.Punch,2f },
            { AnimNames.Kick,2f }
        };

        private TrackEntry SetAnimWithSpeed(Spine.Animation anim, bool loop, float speed = 0f)
        {
            TrackEntry entry = mainSkeleton.AnimationState.SetAnimation(0, anim, loop);
            entry.TimeScale = speed > 0f ? speed : AnimSpeeds.TryGetValue(anim.Name, out var s) ? s : 1f;
            return entry;
        }
        public void PlayAnimation(string animName, bool loop)
        {
            if (mainSkeleton == null || mainVisualObj == null || !mainVisualObj.activeSelf) return;
            if (Time.time < _actionLockUntil) return;
            // Tránh chơi đè lại cùng 1 animation đang chạy
            if (mainSkeleton.AnimationName == animName) return;
            var anim = mainSkeleton.Skeleton?.Data?.FindAnimation(animName);
            if (anim == null) return;
            SetAnimWithSpeed(anim, loop);
        }

        /// <summary>
        /// Diễn 1 động tác KHÔNG lặp (vd "Punch_Combo" khi đánh) rồi tự quay về Idle.
        /// Trong lúc diễn, các lệnh PlayAnimation của di chuyển bị bỏ qua. speed = 0 → tốc độ theo AnimSpeeds.
        /// </summary>
        public bool PlayActionOnce(string animName, float speed = 0f)
        {
            if (mainSkeleton == null || mainVisualObj == null || !mainVisualObj.activeSelf) return false;
            var anim = mainSkeleton.Skeleton?.Data?.FindAnimation(animName);
            if (anim == null) return false;

            var entry = SetAnimWithSpeed(anim, false, speed);
            mainSkeleton.AnimationState.AddAnimation(0, idleAnimation, true, 0f);
            _actionLockUntil = Time.time + (anim.Duration/entry.TimeScale);
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

        // ---- CHIÊU → ANIM (dùng chung cho mình và người khác) ----
        // Mỗi chiêu diễn 1 anim trong bộ Spine của nhân vật — các anim SkillN / JutsuN đã VẼ SẴN hiệu ứng (lửa, khói, đường chém,
        // hào quang... là attachment FX trong Spine), nên không phát thêm hiệu ứng ra chiêu theo dữ liệu (HasBuiltInCastFx).
        // Chọn anim theo thứ tự (ResolveMotion):
        //   1. bảng SkillAnim (hệ, chiêu) — chọn tay theo ảnh chụp anim (Tools/VocVach/Spine/Chụp bảng anim)
        //   2. đòn cơ bản (chiêu 1 / 2 / 3) → BasicMotion của hệ (đấm – đá luân phiên; Sakura đánh xa → ném shuriken)
        //   3. chiêu chưa có trong bảng (vd chiêu mới thêm ở AdminTool) → anim dự phòng theo LOẠI chiêu: đánh → AttackFallback,
        //      hỗ trợ / buff → BuffFallback; bộ Spine thiếu anim nào thì thử anim kế tiếp
        //   4. cuối cùng: đấm – đá.
        // Hiệu ứng ở xa (vật bay, vụ nổ ở mục tiêu) nằm ở bộ xương thứ 2 "<Tên>FX" (Fx.CharFx) — khai trong SkillMotion: fly / impact.
        // Anim chưa dùng: Naruto Jutsu2 · Sakura Skill20(FX hồi máu) / Skill40(FX) · Sasuke Skill80 / Skill100_A (Susanoo).

        /// <summary>
        /// Cách diễn 1 chiêu: anim Spine, tốc độ (1 = gốc), mốc trúng đòn (0..1 độ dài anim: lúc nắm đấm chạm / lúc BUÔNG vật bay).
        /// fly = anim vật bay trong bộ FX (bay từ tay tới mục tiêu, số sát thương hiện lúc tới) · impact = anim nổ ở mục tiêu ·
        /// impactOnce = chiêu diện rộng chỉ nổ 1 lần ở mục tiêu chính (vụ nổ lớn) · ground = đặt vụ nổ ở chân thay vì giữa thân.
        /// </summary>
        public readonly struct SkillMotion
        {
            public readonly string anim, fly, impact;
            public readonly float speed, hit;
            public readonly bool impactOnce, ground;
            public SkillMotion(string anim, float speed = SkillSpeed, float hit = DefaultHit, string fly = null, string impact = null,
                               bool impactOnce = false, bool ground = false)
            {
                this.anim = anim; this.speed = speed; this.hit = hit;
                this.fly = fly; this.impact = impact; this.impactOnce = impactOnce; this.ground = ground;
            }
            public bool HasFx => fly != null || impact != null;
        }
        public const float SkillSpeed = 1.5f;   // anim chiêu gốc dài 1,2–2 giây → nhanh 1,5 lần cho đỡ đứng im lâu
        public const float DefaultHit = 0.35f;

        private static readonly HashSet<int> BasicSkills = new HashSet<int> { 1, 2, 3 };
        private static readonly SkillMotion[] PunchKick = { new SkillMotion(AnimNames.Punch, 2f, 0.2f), new SkillMotion(AnimNames.Kick, 2f, 0.2f) };
        private static readonly Dictionary<int, SkillMotion[]> BasicMotion = new Dictionary<int, SkillMotion[]>
        {
            { 1, PunchKick },                                               // Naruto: đấm – đá (cận chiến 1,8 ô)
            { 2, new[] { new SkillMotion(AnimNames.Throw, 1.7f, 0.45f, fly: "Skill10_Shuriken", impact: "Skill10_Impact") } },  // Sakura: Phi Tiêu đánh xa 5 ô → shuriken bay
            { 3, PunchKick },                                               // Sasuke: đòn nhanh cận chiến
        };
        private static readonly Dictionary<(int cls, int skill), SkillMotion> SkillAnim = new Dictionary<(int, int), SkillMotion>
        {
            // Đấu sĩ — Naruto
            { (1, 6), new SkillMotion("Skill10", 1.5f, 0.3f) },  // Trảm Phong: liên hoàn chém diện rộng
            { (1, 10), new SkillMotion(AnimNames.Jutsu1) },      // Thiết Thể: kết ấn gồng mình
            { (1, 4), new SkillMotion("Skill20") },              // Phân Thân Chi Thuật: phân thân hiện trong khói
            { (1, 12), new SkillMotion("Skill40", 1.5f, 0.35f, impact: "Skill40_hit1") },                   // Tam Liên Trảm: lao tới chém 3 nhát
            { (1, 13), new SkillMotion("Skill50", 1.5f, 0.4f, impact: "Skill50") },                         // Rasengan: ép cầu chakra
            { (1, 14), new SkillMotion("Skill80", 1.5f, 0.55f, impact: "Skill80", impactOnce: true) },      // Rasen Shuriken: nổ đĩa gió
            { (1, 15), new SkillMotion("Skill100", 1.5f, 0.4f, fly: "Skill100", impact: "Skill101", impactOnce: true) }, // Vĩ Thú Ngọc: Cửu Vĩ bắn cầu tím
            // Hỗ trợ — Sakura
            { (2, 7), new SkillMotion("Skill40") },              // Y Thuật: Hồi Phục — vầng sáng xanh lá
            { (2, 9), new SkillMotion("Skill30", 1.2f) },        // Cổ Vũ: hào quang vàng
            { (2, 4), new SkillMotion(AnimNames.Jutsu2) },       // Phân Thân Chi Thuật: khói
            { (2, 16), new SkillMotion("Skill20", 1.5f, 0.5f, impact: "Skill10_Impact") },                  // Anh Hoa Quyền: nắm đấm chakra xanh
            { (2, 17), new SkillMotion("Skill50", 1.5f, 0.45f, fly: "Skill50_Shuriken", impact: "Skill50_Impact") }, // Băng Tiêu: phi tiêu băng bay
            { (2, 18), new SkillMotion("Skill80", 1.5f, 0.45f, impact: "Skill80") },                        // Băng Phong Trảm: sóng băng
            { (2, 19), new SkillMotion(AnimNames.Jutsu1, 1.5f, 0.5f, impact: "Skill30", impactOnce: true, ground: true) }, // Thiên Lôi Phiến: cột sét
            // Sát thủ — Sasuke
            { (3, 8), new SkillMotion("Skill10", 1.3f, 0.3f, impact: "Skill10") },  // Ám Sát: nhát chém kiếm + tia sáng ở mục tiêu
            { (3, 11), new SkillMotion("Skill50", 1.4f) },       // Ảnh Bộ: hoà vào bóng tối, mở Sharingan
            { (3, 4), new SkillMotion(AnimNames.Jutsu1) },       // Phân Thân Chi Thuật: khói
            { (3, 20), new SkillMotion("Skill20", 1.5f, 0.65f, impact: "Skill20") },                        // Hào Hỏa Cầu: thổi lửa → nổ
            { (3, 21), new SkillMotion("Skill40", 1.5f, 0.4f, impact: "Skill40") },                         // Chidori: tay sấm sét
            { (3, 22), new SkillMotion("Skill80_Amaterasu", 1.5f, 0.5f, impact: "Skill80_Amaterasu", ground: true) }, // Amaterasu: lửa đen
            { (3, 23), new SkillMotion("Skill100_B", 1.5f, 0.55f, fly: "Skill100_B_Arrow", impact: "Skill100_B", impactOnce: true, ground: true) }, // Mũi Tên Indra
        };
        private static readonly SkillMotion[] AttackFallback = { new SkillMotion("Skill10", 1.5f, 0.3f), new SkillMotion("Skill40"), new SkillMotion("Skill50") };
        private static readonly SkillMotion[] BuffFallback = { new SkillMotion(AnimNames.Jutsu1), new SkillMotion(AnimNames.Jutsu2), new SkillMotion("Skill30") };

        private int _comboStep;

        /// <summary>Tên anim Spine trong BẢNG chiêu cho hệ {cls} (null = đòn cơ bản / chiêu chưa có trong bảng).</summary>
        public static string SkillAnimFor(int cls, int skillId) => SkillAnim.TryGetValue((cls, skillId), out var m) ? m.anim : null;

        private bool HasAnim(string n) => n != null && mainSkeleton != null && mainSkeleton.Skeleton?.Data?.FindAnimation(n) != null;

        /// <summary>
        /// Chọn cách diễn chiêu {skillId} cho bộ Spine đang dùng (thứ tự ở chú thích đầu mục). advanceCombo = true → đòn cơ bản
        /// lần sau đổi đấm ↔ đá. Không có anim nào → anim = null.
        /// </summary>
        public SkillMotion ResolveMotion(int skillId, bool advanceCombo = false)
        {
            bool basicSkill = BasicSkills.Contains(skillId);
            if (!basicSkill && SkillAnim.TryGetValue((ClassType, skillId), out var m) && HasAnim(m.anim)) return m;
            if (!basicSkill)
            {
                int type = Data.GameData.Skills.TryGetValue(skillId, out var tpl) ? tpl.type : 1;
                foreach (var f in type == 1 ? AttackFallback : BuffFallback) if (HasAnim(f.anim)) return f;
            }
            var basic = BasicMotion.TryGetValue(ClassType, out var b) ? b : PunchKick;
            for (int i = 0; i < basic.Length; i++)
            {
                var c = basic[(_comboStep + i) % basic.Length];
                if (!HasAnim(c.anim)) continue;
                if (advanceCombo) _comboStep += i + 1;
                return c;
            }
            foreach (var c in PunchKick) if (HasAnim(c.anim)) return c;
            return default;
        }

        /// <summary>Chiêu này có anim riêng trong BẢNG (kèm hiệu ứng vẽ sẵn) và bộ Spine đang dùng có anim đó.</summary>
        public bool HasSkillAnim(int skillId) => HasAnim(SkillAnimFor(ClassType, skillId));

        /// <summary>
        /// Người ra chiêu đã có hiệu ứng vẽ sẵn trong Spine → KHÔNG phát hiệu ứng ra chiêu (fxCast) theo dữ liệu:
        /// mọi chiêu tìm được anim (bảng, đòn cơ bản hoặc dự phòng). Hiệu ứng trúng đòn ở mục tiêu (fxHit) vẫn phát.
        /// </summary>
        public bool HasBuiltInCastFx(int skillId) => ResolveMotion(skillId).anim != null;

        /// <summary>Lúc anim chiêu vừa diễn chạm mục tiêu (Time.time) — CombatNetwork giữ số sát thương / hiệu ứng trúng tới mốc này.</summary>
        public float HitTime { get; private set; }

        /// <summary>Tên bộ hiệu ứng riêng của nhân vật ("NarutoFX"...) — Fx.CharFx.</summary>
        public string FxSet { get; private set; }

        private int _lastSkill = -1;
        private SkillMotion _lastMotion;

        /// <summary>
        /// Cách diễn của chiêu {skillId} vừa ra (đúng cú đấm / đá đã diễn, không xoay vòng combo) — CombatNetwork lấy vật bay / vụ nổ.
        /// Chưa diễn chiêu này (VD gói tới trước khi diễn) → chọn lại theo bảng.
        /// </summary>
        public SkillMotion MotionOf(int skillId) => skillId == _lastSkill ? _lastMotion : ResolveMotion(skillId);

        /// <summary>Cỡ hiệu ứng Spine riêng theo nhân vật này.</summary>
        public float FxScale => Fx.CharFx.ScaleFor(mainSkeleton, FxSet);

        /// <summary>
        /// Diễn anim của chiêu skillId (bấm đánh, kể cả khi đòn không gửi lên server). Đòn cơ bản: tốc độ tự tăng để anim
        /// xong trong thời gian hồi chiêu (Sasuke 0,5 giây) — không thì khoá đứng yên lâu hơn nhịp đánh.
        /// </summary>
        public void PlaySkill(int skillId)
        {
            var m = ResolveMotion(skillId, true);
            _lastSkill = skillId; _lastMotion = m;
            HitTime = Time.time;
            if (!HasAnim(m.anim)) return;
            var anim = mainSkeleton.Skeleton.Data.FindAnimation(m.anim);
            float speed = m.speed > 0f ? m.speed : 1f;
            if (BasicSkills.Contains(skillId) && Data.GameData.Skills.TryGetValue(skillId, out var tpl) && tpl.levels.Count > 0 && tpl.levels[0].coolDown > 0)
                speed = Mathf.Max(speed, anim.Duration / (tpl.levels[0].coolDown / 1000f));
            if (PlayActionOnce(m.anim, speed)) HitTime = Time.time + anim.Duration * m.hit / speed;
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