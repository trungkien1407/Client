using System.Collections.Generic;
using Assets.Script.Core;
using Assets.Script.Player;
using Spine.Unity;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// THÚ CƯỠI (GĐ28 — trước là thú cưng đi theo, GĐ27) — gắn trên thân người chơi (mình + người khác), giống BodyAura.
    ///   PetMount.Set(thân, art, dáng, đang cưỡi) — SocialNetwork.OnPvpInfo (đuôi PLAYER_PVP_INFO) · art 0 = không mang thú.
    /// Mang thú = cưỡi: thú đứng dưới chân, nhân vật diễn anim "ride" và được nâng lên lưng thú (PlayerVisualController.SetRide).
    /// Đánh / bị đánh → server cho xuống (riding = 0) → thú biến mất, nhân vật đứng xuống đất. Phím R / nút "Cưỡi" lên lại.
    /// Hình: bộ Spine "pet{art}" (anim idle / run / jump / fall, skin lv1..lv4 = dáng theo cấp thú).
    ///   Tải: Addressables "Pet_{art}" → Resources "Pets/pet{art}/pet{art}_SkeletonData" (demo: Tools → Naruto → Demo art).
    ///   Không có hình → không vẽ thú, nhân vật đi bộ như thường (tốc độ cưỡi vẫn tính — server quyết).
    /// Tốc độ: đang cưỡi chạy nhanh × SpeedBonus (server: PetRules.RIDE_SPEED — đổi thì đổi cả 2).
    ///
    /// TỐI ƯU HIỂN THỊ:
    ///   - Ra ngoài màn hình → Spine chỉ chạy đồng hồ anim, không dựng lưới (updateWhenInvisible = OnlyAnimationStatus).
    ///   - Thú NGƯỜI KHÁC: tắt được (Cài đặt → "Thú người khác"), và chỉ vẽ tối đa {MaxOthers} con gần mình nhất;
    ///     không được vẽ thì người đó đi bộ bình thường (không lơ lửng). Thú của mình luôn vẽ.
    ///   - Object thú dùng lại theo bộ hình (hàng chờ) — người ra vào khu liên tục không tạo / huỷ object.
    /// </summary>
    public class PetMount : MonoBehaviour
    {
        public const string PrefShowOthers = "pet_others";
        /// <summary>Số thú người khác tối đa được vẽ cùng lúc (gần mình nhất).</summary>
        public static int MaxOthers = 8;
        /// <summary>Đang cưỡi chạy nhanh gấp mấy lần (khớp server PetRules.RIDE_SPEED).</summary>
        public const float SpeedBonus = 1.3f;
        // ---- CỠ + CHỖ NGỒI (2026-11-12) ----
        // Mỗi bộ thú vẽ một cỡ, có con tai / sừng / đuôi vểnh cao. Nên không phóng theo đỉnh hình mà theo MẶT LƯNG: đo điểm cao nhất của
        // thân thú ngay tại chỗ người ngồi (dải hẹp giữa thân — MeasureBack, bỏ lớp hiệu ứng cộng sáng) rồi phóng cho mặt lưng mọi con
        // cao bằng BackHeight → người luôn ngồi đúng trên lưng, cùng độ cao; tai / sừng / đuôi cao thấp tuỳ con.
        /// <summary>Mặt lưng thú cao bao nhiêu (đơn vị thế giới; nhân vật cao ~1,6).</summary>
        public const float BackHeight = 0.95f;
        /// <summary>Thú quá dài (rồng...) thì giới hạn bề ngang này; quá cao (sừng, tai) thì giới hạn đỉnh này.</summary>
        public const float MaxWidth = 3f, MaxTop = 2.4f;
        /// <summary>Hông nhân vật trong anim "ride" cao hơn chân bao nhiêu → chân đặt thấp hơn mặt lưng chừng ấy (chân buông hai bên sườn).</summary>
        public const float RiderHip = 0.28f;
        private const float CullEvery = 0.5f;

        // Chỉnh riêng từng bộ thú nếu đo tự động chưa đẹp (chụp cả 24 con: autotest "-mountsonly"): art → (hệ số cỡ, độ nâng thêm,
        // dời chỗ ngồi về phía ĐUÔI — đơn vị thế giới). Không có = (1, 0, 0).
        private static readonly Dictionary<int, (float size, float seat, float back)> Tune = new Dictionary<int, (float, float, float)>
        {
            { 524, (1.1f, -0.3f, 0.22f) },   // Cừu Tuyết: lông xoăn giữa lưng làm mặt lưng đo cao + yên lùi về sau → người ngồi trên đầu; dời về đuôi, hạ thấp
        };

        // BẢNG ĐO TỪNG BỘ THÚ (2026-11-12): art → (mặt lưng tại chỗ ngồi, đỉnh hình) — đơn vị xương (đã nhân tỉ lệ nhập), dáng 4, tư thế gốc.
        // Đo bằng ẢNH (autotest "-mountsonly" chụp thú không người + nền cùng chỗ → Tools/do_thu_cuoi.py tách hình thú, đo điểm ảnh).
        // Vài con có cổ / khói / đồ trang trí nhô ngay giữa thân → mặt lưng đo cao quá → chỉnh tay theo ảnh (ghi chú cuối dòng).
        // Thêm bộ thú mới: chạy lại 2 bước đó rồi dán dòng in ra. Bộ chưa có trong bảng → đo tự động trên khung xương (MeasureBack, kém chính xác hơn).
        private static readonly Dictionary<int, Vector2> BackTable = new Dictionary<int, Vector2>
        {
            { 443, new Vector2(1.109f, 1.405f) },   // Sư Tử Lửa
            { 485, new Vector2(1.228f, 2.276f) },   // Ưng Sư
            { 523, new Vector2(1.497f, 1.554f) },   // Heo Chiến
            { 524, new Vector2(1.500f, 2.077f) },   // Cừu Tuyết — chỉnh tay: đỉnh xoăn giữa lưng
            { 688, new Vector2(1.460f, 1.976f) },   // Khủng Long Đỏ
            { 689, new Vector2(1.126f, 2.019f) },   // Hồ Ly Lửa
            { 841, new Vector2(1.288f, 2.303f) },   // Bạch Kỳ Mã
            { 842, new Vector2(1.385f, 2.187f) },   // Ngọc Thố
            { 843, new Vector2(1.119f, 2.035f) },   // Hoả Long
            { 844, new Vector2(1.270f, 2.868f) },   // Băng Tuần Lộc — đo lại bỏ hạt lấp lánh
            { 1024, new Vector2(1.222f, 1.556f) },   // Sói Băng
            { 1025, new Vector2(1.917f, 3.132f) },   // Bạch Hổ
            { 1036, new Vector2(1.893f, 2.442f) },   // Trâu Tím
            { 1037, new Vector2(1.332f, 2.465f) },   // Bọ Giáp
            { 1088, new Vector2(1.268f, 1.594f) },   // Gấu Mèo
            { 1089, new Vector2(1.329f, 1.783f) },   // Kỵ Sĩ Xương
            { 1090, new Vector2(1.301f, 1.890f) },   // Chuột Lang Nước
            { 1091, new Vector2(1.537f, 1.995f) },   // Đào Tiên
            { 1092, new Vector2(1.083f, 1.952f) },   // Mèo Nhím
            { 1118, new Vector2(1.350f, 2.445f) },   // Song Diện — chỉnh tay: khói giữa thân
            { 1119, new Vector2(1.750f, 3.182f) },   // Gấu Mây — chỉnh tay
            { 1120, new Vector2(1.445f, 2.219f) },   // Quỷ Thủ
            { 1121, new Vector2(2.000f, 2.603f) },   // Ngân Hạnh — chỉnh tay
            { 1122, new Vector2(1.200f, 2.797f) },   // Hà Đồng — chỉnh tay: phần nhô hồng giữa thân
        };

        private static readonly List<PetMount> All = new List<PetMount>();
        private static readonly List<PetMount> Others = new List<PetMount>();   // dùng lại khi chọn (khỏi tạo list mới)
        private static Vector3 _me;
        private static readonly System.Comparison<PetMount> ByDistance =
            (a, b) => (a.transform.position - _me).sqrMagnitude.CompareTo((b.transform.position - _me).sqrMagnitude);
        private static readonly Dictionary<int, Stack<SkeletonAnimation>> Pool = new Dictionary<int, Stack<SkeletonAnimation>>();
        private static float _nextCull;

        private int _art, _stage;
        private bool _riding;
        private SkeletonAnimation _sa;
        private MeshRenderer _saRenderer;   // MeshRenderer của _sa (lấy 1 lần lúc mượn hình, không GetComponent mỗi khung)
        private int _saArt;
        private bool _local, _allowed = true;
        private Player.PlayerVisualController _owner;
        private Vector3 _lastOwner;
        private string _anim;
        private float _lift;

        /// <summary>Đang vẽ thú (autotest kiểm): có hình + đang cưỡi + được phép vẽ.</summary>
        public bool Showing => _sa != null && _sa.gameObject.activeSelf;
        public int Art => _art;
        public int Stage => _stage;
        /// <summary>Server báo đang cưỡi (kể cả khi không có hình để vẽ).</summary>
        public bool Riding => _riding && _art > 0;
        /// <summary>Nhân vật đang được nâng lên bao nhiêu (đơn vị thế giới) — NameTag nâng tên theo.</summary>
        public float Lift => _lift;
        public SkeletonAnimation Skeleton => _sa;
        /// <summary>Số thú đang vẽ (autotest / cửa sổ Cài đặt).</summary>
        public static int VisibleCount { get; private set; }

        public static bool ShowOthers
        {
            get { try { return PlayerPrefs.GetInt(PrefShowOthers, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(PrefShowOthers, value ? 1 : 0); } catch { } _nextCull = 0f; }
        }

        /// <summary>Gắn / đổi / bỏ thú trên thân {body}. art = 0 → bỏ. riding = server báo đang ngồi trên thú.</summary>
        public static void Set(Component body, int art, int stage, bool riding)
        {
            if (body == null) return;
            if (!body.TryGetComponent(out PetMount f))
            {
                if (art <= 0) return;
                f = body.gameObject.AddComponent<PetMount>();
            }
            f._local = body.GetComponent<Player.PlayerMovement>() != null;
            bool newArt = art != f._art;
            f._art = art;
            f._stage = Mathf.Clamp(stage, 1, 4);
            f._riding = riding && art > 0;
            if (f._local) LocalPlayerState.Riding = f._riding;
            if (art <= 0 || !f._riding) { f.ReturnSkeleton(); if (art > 0) Preload(art); return; }
            if (newArt) f.ReturnSkeleton();
            Preload(art);
            _nextCull = 0f;
        }

        /// <summary>Nhân vật của mình: lên / xuống lấy trạng thái hiện tại.</summary>
        public static PetMount Mine()
        {
            var me = Manager.NetworkPlayerManager.Instance != null ? Manager.NetworkPlayerManager.Instance.GetPlayerTransform(LocalPlayerState.Id) : null;
            return me != null && me.TryGetComponent(out PetMount m) ? m : null;
        }

        /// <summary>Phím R / nút "Cưỡi": gửi MOUNT_RIDE ngược trạng thái hiện tại (server báo lại bằng PLAYER_PVP_INFO).</summary>
        public static void ToggleLocal()
        {
            if (LocalPlayerState.IsDead) return;
            if (Data.GameData.MyPet == null)
            {
                Combat.ChatBox.AddSystem("Chưa mang thú cưỡi — mở Thú cưng (phím P) để chọn con mang theo.");
                return;
            }
            Data.GameActions.MountRide(!LocalPlayerState.Riding);
        }

        /// <summary>Mình vừa ra đòn: xuống thú ngay cho đỡ trễ (server cũng cho xuống rồi báo lại).</summary>
        public static void PredictDismount()
        {
            if (!LocalPlayerState.Riding) return;
            var m = Mine();
            if (m != null) Set(m, m._art, m._stage, false);
            LocalPlayerState.Riding = false;
        }

        /// <summary>Thân lấy lại từ pool (người khác dùng): bỏ thú của người trước.</summary>
        public static void ResetOn(Component body)
        {
            if (body != null && body.TryGetComponent(out PetMount f)) { f._art = 0; f._riding = false; f.ReturnSkeleton(); }
        }

        /// <summary>Độ nâng hiện tại của thân {t} (0 = không cưỡi) — NameTag dùng.</summary>
        public static float LiftOf(Component t) => t != null && t.TryGetComponent(out PetMount f) ? f._lift : 0f;

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

        // ==========================================
        // CHỌN THÚ NÀO ĐƯỢC VẼ (2 lần / giây, chung cho mọi thân)
        // ==========================================
        private static void Cull()
        {
            _nextCull = Time.unscaledTime + CullEvery;
            bool haveMe = false;
            foreach (var f in All) if (f._local) { _me = f.transform.position; haveMe = true; break; }
            bool others = ShowOthers;
            Others.Clear();
            foreach (var f in All)
            {
                if (f._local) { f._allowed = true; continue; }
                f._allowed = false;
                if (others && f.Riding) Others.Add(f);
            }
            if (haveMe && Others.Count > MaxOthers) Others.Sort(ByDistance);
            for (int i = 0; i < Others.Count && i < MaxOthers; i++) Others[i]._allowed = true;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextCull) Cull();
            if (_owner == null && !TryGetComponent(out _owner)) return;
            bool hidden = !Riding || !_allowed || _owner.AuraHidden;
            if (hidden) { if (_sa != null) ReturnSkeleton(); return; }

            var data = AssetSource.Cached<SkeletonDataAsset>(Address(_art));
            if (data == null) { Preload(_art); return; }     // chưa tải xong / không có hình
            if (_sa == null) TakeSkeleton(data);
            if (_sa == null) return;

            // 1. Thú đứng ngay dưới chân chủ (cùng hướng nhìn); chủ được nâng lên lưng thú
            Vector3 owner = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 v = (owner - _lastOwner) / dt;
            if ((owner - _lastOwner).sqrMagnitude > 4f) v = Vector3.zero;   // dịch chuyển / đổi map
            _lastOwner = owner;
            _sa.transform.position = owner + new Vector3(0f, -_owner.WaterSink, 0f);
            bool ownerLeft = _owner.mainSkeleton != null && _owner.mainSkeleton.skeleton != null && _owner.mainSkeleton.skeleton.ScaleX > 0;
            Tune.TryGetValue(_art, out var tune);
            float sizeK = tune.size > 0f ? tune.size : 1f;
            // Cỡ: mặt lưng cao BackHeight (dài / cao quá thì theo MaxWidth / MaxTop); dáng cao hơn to thêm chút (dáng 1 ×0,95 … dáng 4 ×1,1)
            float top = Mathf.Max(0.05f, _bTop), wide = Mathf.Max(0.05f, _bWidth), back = Mathf.Clamp(_bBack, 0.05f, top);
            float s = Mathf.Min(BackHeight / back, Mathf.Min(MaxWidth / wide, MaxTop / top)) * (0.9f + 0.05f * _stage) * sizeK;
            _sa.transform.localScale = new Vector3(ownerLeft ? -s : s, s, 1f);   // cùng hướng nhìn với nhân vật (2026-11-12: trước bị ngược)
            // Người ĐI THEO XƯƠNG LƯNG thú (chọn 1 lần — FindSeat): anim chạy làm thân nhún / xô tới lui → người nhún theo,
            // không đứng im lơ lửng / lòi ra khỏi lưng. Lệch ngang = vị trí xương (gần tâm thân), kẹp để anim lạ không kéo người đi xa.
            float dx = 0f, dy = 0f;
            var bones = _sa.Skeleton.Bones;
            if (_seatIdx >= 0 && _seatIdx < bones.Count)
            {
                var bone = bones.Items[_seatIdx];
                dx = Mathf.Clamp(bone.WorldX * s, -0.4f, 0.4f) * (ownerLeft ? -1f : 1f);
                dy = Mathf.Clamp((bone.WorldY - _seatSetupY) * s, -0.3f, 0.3f);
            }
            dx += tune.back * (ownerLeft ? 1f : -1f);   // thú quay trái thì đuôi ở bên phải (+x)
            _lift = Mathf.Max(0.15f, back * s - RiderHip + tune.seat + dy);
            _owner.SetRide(true, _lift, dx);

            // 2. Anim thú theo chuyển động của chủ: lên → jump · xuống → fall · chạy ngang → run · còn lại idle
            string anim = v.y > 1.5f ? "jump" : v.y < -1.5f ? "fall" : Mathf.Abs(v.x) > 0.4f ? "run" : "idle";
            if (anim != _anim && _sa.Skeleton.Data.FindAnimation(anim) != null)
            {
                _anim = anim;
                _sa.AnimationState.SetAnimation(0, anim, true);
            }
            if (_sa.AnimationState.GetCurrent(0) is Spine.TrackEntry te) te.TimeScale = anim == "run" ? SpeedBonus : 1f;

            // 3. Vẽ thú ngay sau nhân vật (cùng lớp, order − 1): người ngồi đè lên lưng thú
            var mainR = _owner.MainRenderer;
            var mr = _saRenderer;
            if (mainR != null && mr != null)
            {
                if (mr.sortingLayerID != mainR.sortingLayerID) mr.sortingLayerID = mainR.sortingLayerID;
                if (mr.sortingOrder != mainR.sortingOrder - 1) mr.sortingOrder = mainR.sortingOrder - 1;
            }
        }

        // Khung hình thật của bộ thú ở tư thế gốc + dáng hiện tại (đơn vị xương đã nhân tỉ lệ nhập) — đo 1 lần mỗi (bộ, dáng)
        private static readonly Dictionary<int, Vector3> Measured = new Dictionary<int, Vector3>();
        private static float[] _vb = new float[256];
        private float _bTop = 1f, _bWidth = 1f, _bBack = 0.6f;
        // Xương "chỗ ngồi" của từng (bộ, dáng): chỉ số trong Skeleton.Bones + độ cao ở tư thế gốc (đơn vị xương)
        private static readonly Dictionary<int, Vector2> SeatCache = new Dictionary<int, Vector2>();
        private int _seatIdx = -1;
        private float _seatSetupY;

        /// <summary>
        /// Chọn xương làm CHỖ NGỒI: ở tư thế gốc, xương gần điểm giữa lưng nhất (x ≈ 0, y ≈ 0,85 × mặt lưng), ưu tiên tên kiểu thân / lưng / hông.
        /// Bỏ xương gốc (ở chân, không nhún). Gọi khi skeleton đang ở tư thế gốc (trong Measure).
        /// </summary>
        private static Vector2 FindSeat(Spine.Skeleton sk, float back)
        {
            int best = -1; float bestScore = float.MaxValue, bestY = 0f;
            var bones = sk.Bones;
            for (int i = 0; i < bones.Count; i++)
            {
                var b = bones.Items[i];
                if (b.Parent == null) continue;
                float score = Mathf.Abs(b.WorldX) + Mathf.Abs(b.WorldY - back * 0.85f);
                string n = b.Data.Name.ToLowerInvariant();
                if (n.Contains("body") || n.Contains("spine") || n.Contains("hip") || n.Contains("pelvis") || n.Contains("torso")
                    || n.Contains("chest") || n.Contains("back") || n.Contains("than") || n.Contains("lung")) score -= back * 0.3f;
                if (b.WorldY < back * 0.35f) score += back;   // xương chân / gần đất: không nhún theo thân
                if (score < bestScore) { bestScore = score; best = i; bestY = b.WorldY; }
            }
            return new Vector2(best, bestY);
        }

        /// <summary>Đo đỉnh (tính từ chân, y = 0) + bề ngang phần hình đang vẽ của thú (Skeleton.GetBounds).</summary>
        private void Measure()
        {
            if (_sa == null) return;
            int key = _art * 10 + _stage;
            if (!Measured.TryGetValue(key, out var m))
            {
                var sk = _sa.Skeleton;
                sk.SetBonesToSetupPose();
                sk.UpdateWorldTransform();
                sk.GetBounds(out float x, out float y, out float w, out float h, ref _vb);
                float topY = Mathf.Max(0.01f, y + h);
                float back;
                if (BackTable.TryGetValue(_art, out var bt)) { back = bt.x; topY = bt.y; }   // đo bằng ảnh — chính xác
                else back = MeasureBack(sk, Mathf.Max(0.02f, w * 0.04f));
                m = new Vector3(topY, Mathf.Max(0.01f, w), back > 0.01f ? back : topY * 0.6f);
                Measured[key] = m;
                SeatCache[key] = FindSeat(sk, m.z);
                _sa.AnimationState.Apply(sk);
            }
            _bTop = m.x;
            _bWidth = m.y;
            _bBack = m.z;
            var seat = SeatCache.TryGetValue(key, out var st) ? st : new Vector2(-1, 0);
            _seatIdx = (int)seat.x;
            _seatSetupY = seat.y;
        }

        /// <summary>
        /// Mặt lưng: điểm cao nhất của các mảnh hình thường (bỏ mảnh cộng sáng / hiệu ứng) trong dải |x| ≤ band quanh tâm bộ xương
        /// (chỗ người ngồi). Mỗi mảnh là đa giác (ô chữ nhật / tam giác lưới) → lấy giao của các cạnh với đường x = ±band, 0 và các đỉnh nằm trong dải.
        /// </summary>
        private static float MeasureBack(Spine.Skeleton sk, float band)
        {
            float best = 0f;
            var order = sk.DrawOrder.Items;
            for (int i = 0; i < sk.DrawOrder.Count; i++)
            {
                var slot = order[i];
                if (slot.Attachment == null || slot.Data.BlendMode != Spine.BlendMode.Normal || slot.A < 0.5f) continue;
                int n;
                int[] tris = null;
                if (slot.Attachment is Spine.RegionAttachment ra)
                {
                    if (_vb.Length < 8) _vb = new float[8];
                    ra.ComputeWorldVertices(slot.Bone, _vb, 0);
                    n = 4;
                }
                else if (slot.Attachment is Spine.MeshAttachment ma)
                {
                    if (_vb.Length < ma.WorldVerticesLength) _vb = new float[ma.WorldVerticesLength];
                    ma.ComputeWorldVertices(slot, 0, ma.WorldVerticesLength, _vb, 0);
                    n = ma.WorldVerticesLength / 2;
                    tris = ma.Triangles;
                }
                else continue;
                if (tris == null)
                {
                    for (int k = 0; k < 4; k++) best = Edge(best, band, _vb[k * 2], _vb[k * 2 + 1], _vb[(k + 1) % 4 * 2], _vb[(k + 1) % 4 * 2 + 1]);
                }
                else
                {
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                        for (int e = 0; e < 3; e++)
                        {
                            int a = tris[t + e], b = tris[t + (e + 1) % 3];
                            if (a >= n || b >= n) continue;
                            best = Edge(best, band, _vb[a * 2], _vb[a * 2 + 1], _vb[b * 2], _vb[b * 2 + 1]);
                        }
                }
            }
            return best;
        }

        /// <summary>Cạnh (x1,y1)–(x2,y2): điểm cao nhất của nó nằm trong dải |x| ≤ band.</summary>
        private static float Edge(float best, float band, float x1, float y1, float x2, float y2)
        {
            if (Mathf.Abs(x1) <= band) best = Mathf.Max(best, y1);
            if (Mathf.Abs(x2) <= band) best = Mathf.Max(best, y2);
            if (Mathf.Approximately(x1, x2)) return best;
            foreach (float cx in new[] { -band, 0f, band })
                if ((x1 - cx) * (x2 - cx) <= 0f) best = Mathf.Max(best, y1 + (y2 - y1) * (cx - x1) / (x2 - x1));
            return best;
        }

        /// <summary>Autotest: kích thước thú đang vẽ (đơn vị thế giới: cao, rộng).</summary>
        public Vector2 DrawnSize => _sa == null ? Vector2.zero : new Vector2(_bTop, _bWidth) * Mathf.Abs(_sa.transform.localScale.y);
        /// <summary>Autotest: mặt lưng đang vẽ cao bao nhiêu (đơn vị thế giới).</summary>
        public float DrawnBack => _sa == null ? 0f : _bBack * Mathf.Abs(_sa.transform.localScale.y);

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
            _saRenderer = sa.GetComponent<MeshRenderer>();
            _saArt = _art;
            _stage = Mathf.Clamp(_stage, 1, 4);
            ApplySkin();
            _anim = null;
            _lastOwner = transform.position;
            _sa.transform.position = transform.position;
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
            Measure();
        }

        private void Update()
        {
            if (_sa != null && _skinApplied != _stage) ApplySkin();   // lên dáng mới (server gửi PLAYER_PVP_INFO)
        }

        private void ReturnSkeleton()
        {
            if (_lift != 0f || (_owner != null && _owner.IsRiding)) { _lift = 0f; if (_owner != null || TryGetComponent(out _owner)) _owner.SetRide(false, 0f); }
            if (_sa == null) return;
            _sa.gameObject.SetActive(false);
            if (!Pool.TryGetValue(_saArt, out var st)) Pool[_saArt] = st = new Stack<SkeletonAnimation>();
            st.Push(_sa);
            _sa = null;
            _saRenderer = null;
            _anim = null;
            _skinApplied = -1;
            VisibleCount = Mathf.Max(0, VisibleCount - 1);
        }

        /// <summary>Hết phiên / đổi map: không cần gì — thân bị cất thì OnDisable tự cất thú.</summary>
        private void OnDestroy() { ReturnSkeleton(); }
    }
}
