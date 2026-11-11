using System.Collections.Generic;
using Assets.Script.Core;
using Spine.Unity;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// THÚ CƯNG ĐI THEO NHÂN VẬT (GĐ27) — gắn trên thân người chơi (mình + người khác), giống BodyAura.
    ///   PetFollower.Set(thân, art, dáng)   — SocialNetwork.OnPvpInfo (server gửi đuôi PLAYER_PVP_INFO) · art 0 = không mang.
    /// Hình: bộ Spine "pet{art}" (anim idle / run / jump / fall, skin lv1..lv4 = dáng theo cấp thú).
    ///   Tải: Addressables "Pet_{art}" → Resources "Pets/pet{art}/pet{art}_SkeletonData" (demo: Tools → Naruto → Demo art → 4).
    ///   Không có hình → không vẽ gì (chỉ số thú vẫn tính ở server).
    /// Thú là object RIÊNG (không làm con thân): chạy đuổi theo chủ có độ trễ, đứng sau lưng chủ, chủ nhảy thì nhảy theo.
    ///
    /// TỐI ƯU HIỂN THỊ:
    ///   - Ra ngoài màn hình → Spine chỉ chạy đồng hồ anim, không dựng lưới (updateWhenInvisible = OnlyAnimationStatus).
    ///   - Thú NGƯỜI KHÁC: tắt hẳn được (Cài đặt → "Thú người khác"), và chỉ vẽ tối đa {MaxOthers} con gần mình nhất
    ///     (khu đông người không bị 50 bộ xương cùng chạy). Thú của mình luôn vẽ.
    ///   - Object thú dùng lại theo bộ hình (hàng chờ) — người ra vào khu liên tục không tạo / huỷ object.
    /// </summary>
    public class PetFollower : MonoBehaviour
    {
        public const string PrefShowOthers = "pet_others";
        /// <summary>Số thú người khác tối đa được vẽ cùng lúc (gần mình nhất).</summary>
        public static int MaxOthers = 8;
        /// <summary>Cỡ thú so với nhân vật (cùng tỉ lệ bộ xương, ×hệ số này).</summary>
        public const float SizeFactor = 2f;   // bộ thú vẽ nhỏ hơn bộ nhân vật ~2,5 lần (cùng tỉ lệ xương) → phóng cho cao ~ nửa người
        private const float Behind = 1.2f, TeleportDist = 8f, CullEvery = 0.5f;

        private static readonly List<PetFollower> All = new List<PetFollower>();
        private static readonly List<PetFollower> Others = new List<PetFollower>();   // dùng lại khi chọn (khỏi tạo list mới)
        private static Vector3 _me;
        private static readonly System.Comparison<PetFollower> ByDistance =
            (a, b) => (a.transform.position - _me).sqrMagnitude.CompareTo((b.transform.position - _me).sqrMagnitude);
        private static readonly Dictionary<int, Stack<SkeletonAnimation>> Pool = new Dictionary<int, Stack<SkeletonAnimation>>();
        private static float _nextCull;

        private int _art, _stage;
        private SkeletonAnimation _sa;
        private int _saArt;
        private bool _local, _allowed = true;
        private Player.PlayerVisualController _owner;
        private Vector3 _pos, _vel;
        private bool _faceRight;
        private string _anim;

        /// <summary>Đang hiện thú (autotest kiểm): có hình + đang vẽ.</summary>
        public bool Showing => _sa != null && _sa.gameObject.activeSelf;
        public int Art => _art;
        public int Stage => _stage;
        public SkeletonAnimation Skeleton => _sa;
        /// <summary>Số thú đang vẽ (autotest / cửa sổ Cài đặt).</summary>
        public static int VisibleCount { get; private set; }

        public static bool ShowOthers
        {
            get { try { return PlayerPrefs.GetInt(PrefShowOthers, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(PrefShowOthers, value ? 1 : 0); } catch { } _nextCull = 0f; }
        }

        /// <summary>Gắn / đổi / bỏ thú trên thân {body}. art = 0 → bỏ.</summary>
        public static void Set(Component body, int art, int stage)
        {
            if (body == null) return;
            if (!body.TryGetComponent(out PetFollower f))
            {
                if (art <= 0) return;
                f = body.gameObject.AddComponent<PetFollower>();
            }
            f._local = body.GetComponent<Player.PlayerMovement>() != null;
            bool newArt = art != f._art;
            f._art = art;
            f._stage = Mathf.Clamp(stage, 1, 4);
            if (art <= 0) { f.ReturnSkeleton(); return; }
            if (newArt) { f.ReturnSkeleton(); f.Snap(); }
            Preload(art);
            _nextCull = 0f;
        }

        /// <summary>Thân lấy lại từ pool (người khác dùng): bỏ thú của người trước.</summary>
        public static void ResetOn(Component body)
        {
            if (body != null && body.TryGetComponent(out PetFollower f)) { f._art = 0; f.ReturnSkeleton(); }
        }

        public static void Preload(int art)
        {
            if (art > 0) AssetSource.Load<SkeletonDataAsset>(Address(art), $"Pets/pet{art}/pet{art}_SkeletonData", null);
        }

        private static string Address(int art) => "Pet_" + art;

        private void OnEnable() { All.Add(this); }
        private void OnDisable()
        {
            All.Remove(this);
            ReturnSkeleton();   // thân cất vào pool / ẩn → cất thú
        }

        private void Snap()
        {
            _pos = transform.position + new Vector3(Behind, 0f, 0f);
            _vel = Vector3.zero;
        }

        // ==========================================
        // CHỌN THÚ NÀO ĐƯỢC VẼ (2 lần / giây, chung cho mọi thân)
        // ==========================================
        private static void Cull()
        {
            _nextCull = Time.unscaledTime + CullEvery;
            bool haveMe = false;
            foreach (var f in All) if (f._local) { _me = f.transform.position; haveMe = true; break; }
            bool others = ShowOthers;
            // Thú người khác: sắp theo khoảng cách tới mình, giữ MaxOthers con gần nhất
            Others.Clear();
            foreach (var f in All)
            {
                if (f._local) { f._allowed = true; continue; }
                f._allowed = false;
                if (others && f._art > 0) Others.Add(f);
            }
            if (haveMe && Others.Count > MaxOthers) Others.Sort(ByDistance);
            for (int i = 0; i < Others.Count && i < MaxOthers; i++) Others[i]._allowed = true;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextCull) Cull();
            if (_owner == null && !TryGetComponent(out _owner)) return;
            bool hidden = _art <= 0 || !_allowed || _owner.AuraHidden;
            if (hidden) { if (_sa != null) ReturnSkeleton(); return; }

            var data = AssetSource.Cached<SkeletonDataAsset>(Address(_art));
            if (data == null) { Preload(_art); return; }     // chưa tải xong / không có hình
            if (_sa == null) TakeSkeleton(data);
            if (_sa == null) return;

            // 1. Đuổi theo chủ: điểm đích sau lưng chủ (theo hướng chủ đang nhìn), cách xa quá thì nhảy tới luôn
            Vector3 owner = transform.position;
            bool ownerLeft = _owner.mainSkeleton != null && _owner.mainSkeleton.skeleton != null && _owner.mainSkeleton.skeleton.ScaleX > 0;
            Vector3 target = owner + new Vector3(ownerLeft ? Behind : -Behind, -_owner.WaterSink, 0f);
            if ((target - _pos).sqrMagnitude > TeleportDist * TeleportDist) { _pos = target; _vel = Vector3.zero; }
            Vector3 before = _pos;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _pos.x = Mathf.SmoothDamp(_pos.x, target.x, ref _vel.x, 0.18f, 20f, dt);
            _pos.y = Mathf.SmoothDamp(_pos.y, target.y, ref _vel.y, 0.10f, 30f, dt);
            _sa.transform.position = _pos;
            Vector3 v = (_pos - before) / dt;

            // 2. Hướng nhìn: theo hướng chạy; đứng yên thì nhìn cùng hướng chủ (hình gốc vẽ quay trái → quay phải thì lật)
            if (Mathf.Abs(v.x) > 0.3f) _faceRight = v.x > 0f;
            else if (Mathf.Abs(target.x - _pos.x) < 0.2f) _faceRight = !ownerLeft;
            float s = Scale(data);
            _sa.transform.localScale = new Vector3(_faceRight ? -s : s, s, 1f);

            // 3. Anim: lên → jump · xuống → fall · chạy ngang → run · còn lại idle
            string anim = v.y > 1.5f ? "jump" : v.y < -1.5f ? "fall" : Mathf.Abs(v.x) > 0.4f ? "run" : "idle";
            if (anim != _anim && _sa.Skeleton.Data.FindAnimation(anim) != null)
            {
                _anim = anim;
                _sa.AnimationState.SetAnimation(0, anim, true);
            }

            // 4. Vẽ ngay sau lưng chủ (cùng lớp, order − 1)
            var mainR = _owner.MainRenderer;
            var mr = _sa.GetComponent<MeshRenderer>();
            if (mainR != null && mr != null)
            {
                if (mr.sortingLayerID != mainR.sortingLayerID) mr.sortingLayerID = mainR.sortingLayerID;
                if (mr.sortingOrder != mainR.sortingOrder - 1) mr.sortingOrder = mainR.sortingOrder - 1;
            }
        }

        /// <summary>Cùng tỉ lệ bộ xương với nhân vật chủ (bộ thú nhập với scale khác) × SizeFactor.</summary>
        private float Scale(SkeletonDataAsset data)
        {
            var ch = _owner != null ? _owner.mainSkeleton : null;
            float charScale = ch != null && ch.skeletonDataAsset != null ? Mathf.Abs(ch.transform.lossyScale.y) * ch.skeletonDataAsset.scale : 0.004f;
            return data.scale > 0f ? charScale / data.scale * SizeFactor : 1f;
        }

        // ==========================================
        // HÀNG CHỜ OBJECT THÚ (theo bộ hình)
        // ==========================================
        private void TakeSkeleton(SkeletonDataAsset data)
        {
            SkeletonAnimation sa = null;
            if (Pool.TryGetValue(_art, out var st)) while (st.Count > 0 && sa == null) sa = st.Pop();
            if (sa == null)
            {
                sa = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
                sa.gameObject.name = "Pet_" + _art;
                DontDestroyOnLoad(sa.gameObject);
                sa.updateWhenInvisible = UpdateMode.OnlyAnimationStatus;   // ngoài màn hình: không dựng lưới
                sa.Initialize(true);
            }
            sa.gameObject.SetActive(true);
            _sa = sa;
            _saArt = _art;
            _stage = Mathf.Clamp(_stage, 1, 4);
            ApplySkin();
            _anim = null;
            if (_pos == Vector3.zero) Snap();
            _sa.transform.position = _pos;
            VisibleCount++;
        }

        private int _skinApplied = -1;

        private void ApplySkin()
        {
            if (_sa == null) return;
            var skin = _sa.Skeleton.Data.FindSkin("lv" + _stage) ?? _sa.Skeleton.Data.DefaultSkin;
            _sa.Skeleton.SetSkin(skin);
            _sa.Skeleton.SetSlotsToSetupPose();
            _sa.AnimationState.Apply(_sa.Skeleton);
            _skinApplied = _stage;
        }

        private void Update()
        {
            if (_sa != null && _skinApplied != _stage) ApplySkin();   // lên dáng mới (server gửi PLAYER_PVP_INFO)
        }

        private void ReturnSkeleton()
        {
            if (_sa == null) return;
            _sa.gameObject.SetActive(false);
            if (!Pool.TryGetValue(_saArt, out var st)) Pool[_saArt] = st = new Stack<SkeletonAnimation>();
            st.Push(_sa);
            _sa = null;
            _anim = null;
            _skinApplied = -1;
            VisibleCount = Mathf.Max(0, VisibleCount - 1);
        }

        /// <summary>Hết phiên / đổi map: không cần gì — thân bị cất thì OnDisable tự cất thú.</summary>
        private void OnDestroy() { ReturnSkeleton(); }
    }
}
