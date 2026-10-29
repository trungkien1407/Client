using System.Collections.Generic;
using Assets.Script.Core;
using Spine.Unity;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// HIỆU ỨNG SPINE RIÊNG CỦA NHÂN VẬT — bộ xương thứ 2 "<Tên>FX" đi kèm bộ nhân vật (NarutoFX, SakuraFX, SasukeFX):
    ///   vật bay (Rasengan, shuriken, mũi tên...) và vụ nổ ở mục tiêu. Anim nhân vật chỉ vẽ động tác + hiệu ứng quanh người.
    ///   CharFx.Play(bộ, anim, điểm, ...)                 — diễn 1 lần tại chỗ (nổ / trúng đòn)
    ///   CharFx.Fly(bộ, anim, từ, mục tiêu, chờ, bay...)  — chờ tới lúc buông tay → bay bám mục tiêu đúng {bay} giây → tự tắt
    /// Tải: Addressables "{bộ}" → Resources "CharFX/{bộ}/{bộ}_SkeletonData" (demo nội bộ, Tools → Naruto → Demo art).
    /// Không có bộ FX → không diễn gì (chiêu vẫn chạy bình thường). Đối tượng dùng lại qua hàng chờ.
    /// </summary>
    public class CharFx : MonoBehaviour
    {
        /// <summary>Đặt hiệu ứng: giữa khung hình vào giữa thân mục tiêu, hoặc đáy khung vào chân (cột sáng, sóng đất, lửa cháy).</summary>
        public enum Anchor { Center, Ground }

        private class Live
        {
            public SkeletonAnimation sa;
            public string set;
            public float start, until;     // bắt đầu hiện / hết hạn (Time.time)
            public bool flying;
            public Vector3 from, offset;   // vật bay: điểm xuất phát, lệch để giữa khung trùng điểm bay
            public Transform target;
            public Vector3 lastTarget;
            public float flight;
            public float scaleX;
        }

        private static CharFx _runner;
        private static readonly Dictionary<string, Stack<SkeletonAnimation>> Pool = new Dictionary<string, Stack<SkeletonAnimation>>();
        private static readonly Dictionary<(string, string), Vector3> Centers = new Dictionary<(string, string), Vector3>();
        private static readonly Dictionary<(string, string), Vector3> Bottoms = new Dictionary<(string, string), Vector3>();
        private readonly List<Live> _live = new List<Live>();

        /// <summary>Số vật bay / vụ nổ đã phát từ lúc mở game (AutoTestRunner đo).</summary>
        public static int Flights { get; private set; }
        public static int Impacts { get; private set; }

        /// <summary>Bộ FX đã tải xong (chưa thì bắt đầu tải).</summary>
        public static bool Loaded(string set) => Data(set) != null;

        /// <summary>Tên bộ FX theo bộ nhân vật: "Naruto_SkeletonData" → "NarutoFX".</summary>
        public static string SetFor(SkeletonDataAsset character) =>
            character == null ? null : character.name.Replace("_SkeletonData", "") + "FX";

        /// <summary>Tải trước (lúc dựng nhân vật) để lần ra chiêu đầu không bị trống.</summary>
        public static void Preload(string set)
        {
            if (!string.IsNullOrEmpty(set)) AssetSource.Load<SkeletonDataAsset>(set, $"CharFX/{set}/{set}_SkeletonData", null);
        }

        private static SkeletonDataAsset Data(string set)
        {
            if (string.IsNullOrEmpty(set)) return null;
            var d = AssetSource.Cached<SkeletonDataAsset>(set);
            if (d == null) Preload(set);   // chưa tải xong lần này thì bỏ qua, lần sau đã có
            return d;
        }

        /// <summary>Bộ FX đã tải và có anim {anim}.</summary>
        public static bool Has(string set, string anim)
        {
            var d = Data(set);
            return d != null && !string.IsNullOrEmpty(anim) && d.GetSkeletonData(true)?.FindAnimation(anim) != null;
        }

        /// <summary>
        /// Diễn {anim} 1 lần tại {at} (giữa thân hoặc chân mục tiêu, theo {anchor}). {scale} = cỡ theo nhân vật ra chiêu
        /// (ScaleFor), {faceRight} = người ra chiêu quay phải (hình gốc vẽ quay trái → lật).
        /// </summary>
        public static bool Play(string set, string anim, Vector3 at, float scale, bool faceRight, Anchor anchor = Anchor.Center)
        {
            if (!Has(set, anim)) return false;
            var l = Spawn(set, anim, false, scale, faceRight);
            var off = anchor == Anchor.Ground ? Bottoms[(set, anim)] : Centers[(set, anim)];
            l.sa.transform.position = at - Scaled(off, l.scaleX, scale);
            l.until = Time.time + l.sa.AnimationState.GetCurrent(0).Animation.Duration;
            Impacts++;
            return true;
        }

        /// <summary>
        /// Vật bay: chờ {delay} giây (tới khung buông tay của anim nhân vật) rồi bay từ {from} tới giữa thân {target} trong
        /// đúng {flight} giây (bám theo nếu mục tiêu chạy). Mục tiêu biến mất giữa đường → bay tới chỗ cuối cùng thấy.
        /// </summary>
        public static bool Fly(string set, string anim, Vector3 from, Transform target, float delay, float flight, float scale)
        {
            if (!Has(set, anim) || target == null) return false;
            Vector3 to = Combat.CombatNetwork.BodyCenter(target);
            var l = Spawn(set, anim, true, scale, to.x > from.x);
            l.flying = true;
            l.from = from;
            l.target = target;
            l.lastTarget = to;
            l.flight = Mathf.Max(0.05f, flight);
            l.offset = Centers[(set, anim)];
            l.start = Time.time + delay;
            l.until = l.start + l.flight;
            l.sa.gameObject.SetActive(delay <= 0f);
            _runner.Place(l, 0f);
            Flights++;
            return true;
        }

        /// <summary>Cỡ hiệu ứng theo bộ nhân vật đang dùng: bộ FX nhập với tỉ lệ khác bộ nhân vật (0,01 so với 0,004).</summary>
        public static float ScaleFor(SkeletonAnimation character, string set)
        {
            var d = Data(set);
            if (character == null || character.skeletonDataAsset == null || d == null || d.scale <= 0f) return 1f;
            return Mathf.Abs(character.transform.lossyScale.x) * character.skeletonDataAsset.scale / d.scale;
        }

        /// <summary>Tắt hết (đổi map / khu).</summary>
        public static void StopAll()
        {
            if (_runner == null) return;
            foreach (var l in _runner._live) Recycle(l);
            _runner._live.Clear();
        }

        private static Vector3 Scaled(Vector3 v, float sx, float scale) => new Vector3(v.x * sx, v.y * scale, 0f);

        private static Live Spawn(string set, string anim, bool loop, float scale, bool faceRight)
        {
            if (_runner == null)
            {
                _runner = new GameObject("CharFx").AddComponent<CharFx>();
                DontDestroyOnLoad(_runner.gameObject);
            }
            var data = Data(set);
            SkeletonAnimation sa = null;
            if (Pool.TryGetValue(set, out var st)) while (st.Count > 0 && sa == null) sa = st.Pop();
            if (sa == null)
            {
                sa = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
                sa.transform.SetParent(_runner.transform, false);
                var mr = sa.GetComponent<MeshRenderer>();
                mr.sortingLayerName = EffectPlayer.SortingLayer;
                mr.sortingOrder = EffectPlayer.SortingOrder;
                sa.Initialize(true);
            }
            sa.gameObject.SetActive(true);
            float sx = faceRight ? -scale : scale;
            sa.transform.localScale = new Vector3(sx, scale, 1f);
            sa.transform.rotation = Quaternion.identity;
            if (!Centers.ContainsKey((set, anim))) Measure(sa, set, anim);
            sa.Skeleton.SetToSetupPose();
            sa.AnimationState.ClearTracks();
            sa.AnimationState.SetAnimation(0, anim, loop);
            sa.Update(0f);
            var l = new Live { sa = sa, set = set, start = Time.time, scaleX = sx };
            _runner._live.Add(l);
            return l;
        }

        /// <summary>
        /// Đo khung hình của anim (gốc toạ độ mỗi hiệu ứng mỗi khác: có cái ở giữa, có cái lệch hẳn 1 phía) — lấy mẫu 3 thời điểm,
        /// nhớ lệch tâm / đáy so với gốc ở cỡ 1, quay trái. Nhờ vậy mọi hiệu ứng đặt đúng giữa thân / chân mục tiêu.
        /// </summary>
        private static void Measure(SkeletonAnimation sa, string set, string anim)
        {
            var mr = sa.GetComponent<MeshRenderer>();
            var a = sa.Skeleton.Data.FindAnimation(anim);
            var t = sa.transform;
            Vector3 keepPos = t.position, keepScale = t.localScale;
            t.position = Vector3.zero;
            t.localScale = Vector3.one;
            bool any = false;
            var b = new Bounds();
            foreach (float k in new[] { 0.25f, 0.5f, 0.75f })
            {
                sa.Skeleton.SetToSetupPose();
                sa.AnimationState.ClearTracks();
                sa.AnimationState.SetAnimation(0, a, false);
                sa.Update(a.Duration * k);
                sa.LateUpdate();
                var mb = mr.bounds;
                if (mb.size.sqrMagnitude < 1e-8f) continue;
                if (!any) { b = mb; any = true; } else b.Encapsulate(mb);
            }
            t.position = keepPos;
            t.localScale = keepScale;
            Centers[(set, anim)] = any ? new Vector3(b.center.x, b.center.y, 0f) : Vector3.zero;
            Bottoms[(set, anim)] = any ? new Vector3(b.center.x, b.min.y, 0f) : Vector3.zero;
        }

        private static void Recycle(Live l)
        {
            if (l.sa == null) return;
            l.sa.gameObject.SetActive(false);
            if (!Pool.TryGetValue(l.set, out var st)) Pool[l.set] = st = new Stack<SkeletonAnimation>();
            st.Push(l.sa);
        }

        private void Place(Live l, float u)
        {
            if (l.target != null && l.target.gameObject.activeInHierarchy) l.lastTarget = Combat.CombatNetwork.BodyCenter(l.target);
            Vector3 p = Vector3.Lerp(l.from, l.lastTarget, u);
            // Hình gốc vẽ hướng sang TRÁI: bay sang phải thì lật, rồi xoay theo độ dốc đường bay
            Vector3 d = l.lastTarget - l.from;
            bool right = d.x > 0f;
            float scale = Mathf.Abs(l.scaleX);
            l.scaleX = right ? -scale : scale;
            l.sa.transform.localScale = new Vector3(l.scaleX, scale, 1f);
            float ang = right ? Mathf.Atan2(d.y, d.x) : Mathf.Atan2(-d.y, -d.x);
            l.sa.transform.rotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
            l.sa.transform.position = p - l.sa.transform.rotation * Scaled(l.offset, l.scaleX, scale);
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var l = _live[i];
                if (l.sa == null || now >= l.until) { Recycle(l); _live.RemoveAt(i); continue; }
                if (!l.flying || now < l.start) continue;
                if (!l.sa.gameObject.activeSelf) l.sa.gameObject.SetActive(true);
                Place(l, (now - l.start) / l.flight);
            }
        }
    }
}
