using System.Collections.Generic;
using Assets.Script.Constants;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>Thân mang được hiệu ứng (nhân vật, quái) — BodyAura chỉ hỏi qua đây, không cần biết lớp cụ thể.</summary>
    public interface IAuraBody
    {
        bool AuraHidden { get; }        // chết / chưa có hình → không vẽ gì
        Renderer MainRenderer { get; }  // lấy lớp vẽ (sorting layer / order) để vòng dưới chân nằm SAU thân, sao / tia lửa nằm TRƯỚC
        Bounds BodyBounds { get; }      // khung thân (toạ độ world): chân = min.y, đỉnh đầu = max.y
        void SetStatusTint(Color c);    // nhuộm thân (nhân với màu gốc); trắng = bỏ nhuộm
    }

    /// <summary>
    /// HIỆU ỨNG TRÊN THÂN nhân vật / quái — vẽ hoàn toàn bằng code (FxShapes), không cần art:
    ///   Choáng        → 3 ngôi sao vàng xoay quanh đỉnh đầu
    ///   Chậm          → thân nhuộm xanh lạnh + vòng xanh dưới chân
    ///   Bỏng          → thân nhấp nháy cam + tàn lửa bay lên
    ///   Tăng sức mạnh → vòng xanh lá dưới chân + đốm sáng bay lên
    ///   Hào quang     → (người chơi) vòng + đốm sáng màu theo cấp vũ khí: 1 tím (+9) · 2 cam (+12) · 3 đỏ (+15)
    /// Màu trạng thái trùng icon trên HUD (StatusIconsHud.Colors). Phân Thân đã có PlayerVisualController.Ghost (mờ) nên không vẽ thêm.
    /// Dùng: BodyAura.On(transform).Add(hiệuỨng, giây) (EventNetwork.OnEffect) · .GlowTier = n (SocialNetwork.OnPvpInfo).
    /// Hình nằm ở 1 object riêng "Aura" bám theo thân (không làm con) → không bị co giãn theo scale của quái.
    /// </summary>
    public class BodyAura : MonoBehaviour
    {
        /// <summary>Màu hào quang theo bậc 1..3 — trùng màu chữ +9 / +12 / +15 (GameData.LevelColor).</summary>
        public static readonly Color[] GlowColors =
        {
            Color.clear, new Color(0.76f, 0.48f, 1f), new Color(1f, 0.6f, 0.18f), new Color(1f, 0.25f, 0.25f),
        };

        private const int MaxSparks = 18;
        private const float RingBehind = -1, InFront = 5;   // lệch order so với thân

        private readonly float[] _until = new float[6];      // hết hạn từng hiệu ứng (Time.time), chỉ số = StatusEffect
        private IAuraBody _body;
        private Color _tint = Color.white;

        private Transform _root;
        private SpriteRenderer _ring;
        private readonly SpriteRenderer[] _stars = new SpriteRenderer[3];
        private readonly List<Spark> _sparks = new List<Spark>();
        private float _sparkDebt;                            // số tia "nợ" (tốc độ × thời gian) — đủ 1 thì sinh 1 tia

        private class Spark { public SpriteRenderer r; public Vector3 pos, vel; public float age, life, size; public Color color; }

        /// <summary>Hào quang 0..3 (server tính theo cấp vũ khí, gửi trong PLAYER_PVP_INFO).</summary>
        public int GlowTier { get; set; }

        /// <summary>Lấy (hoặc gắn mới) BodyAura trên đối tượng.</summary>
        public static BodyAura On(Component c) => c.TryGetComponent(out BodyAura a) ? a : c.gameObject.AddComponent<BodyAura>();

        /// <summary>Bỏ mọi trạng thái đang có (chết, lấy lại từ pool). {glowToo}: bỏ luôn hào quang (thân dùng cho người khác).</summary>
        public static void ResetOn(Component c, bool glowToo = false)
        {
            if (c == null || !c.TryGetComponent(out BodyAura a)) return;
            System.Array.Clear(a._until, 0, a._until.Length);
            if (glowToo) a.GlowTier = 0;
            a.ApplyTint(Color.white);
        }

        /// <summary>Đổi map / khu: bỏ choáng / chậm / bỏng (khớp server Zone.leave), giữ tăng sức mạnh + hào quang.</summary>
        public static void ClearHarmfulOn(Component c)
        {
            if (c == null || !c.TryGetComponent(out BodyAura a)) return;
            for (int k = (int)StatusEffect.Stun; k <= (int)StatusEffect.Burn; k++) a._until[k] = 0f;
        }

        /// <summary>Dính hiệu ứng {e} thêm {seconds} giây (lần dính sau dài hơn thì nối dài).</summary>
        public void Add(StatusEffect e, float seconds)
        {
            int i = (int)e;
            if (i <= 0 || i >= _until.Length) return;
            _until[i] = Mathf.Max(_until[i], Time.time + seconds);
        }

        /// <summary>Đang hiện hiệu ứng {e} không (autotest kiểm).</summary>
        public bool Has(StatusEffect e) => (int)e > 0 && (int)e < _until.Length && Time.time < _until[(int)e];

        /// <summary>Vòng dưới chân đang hiện không (autotest kiểm hào quang).</summary>
        public bool RingVisible => _ring != null && _ring.enabled;

        private void Update()
        {
            if (_body == null && !TryGetComponent(out _body)) return;
            float now = Time.time;
            bool hidden = _body.AuraHidden;
            bool stun = !hidden && Has(StatusEffect.Stun), slow = !hidden && Has(StatusEffect.Slow);
            bool burn = !hidden && Has(StatusEffect.Burn), buff = !hidden && Has(StatusEffect.Buff);
            int glow = hidden ? 0 : Mathf.Clamp(GlowTier, 0, GlowColors.Length - 1);

            // 1. Nhuộm thân: chậm → xanh lạnh; bỏng → nhấp nháy cam
            Color tint = Color.white;
            if (slow) tint *= new Color(0.6f, 0.8f, 1f);
            if (burn) tint *= Color.Lerp(Color.white, new Color(1f, 0.55f, 0.35f), 0.5f + 0.5f * Mathf.Sin(now * 18f));
            ApplyTint(tint);

            bool any = stun || slow || burn || buff || glow > 0;
            if (!any && _root == null) return;               // chưa từng có gì → khỏi tạo object
            EnsureRoot();
            _root.gameObject.SetActive(any || _sparks.Exists(s => s.r.enabled));
            if (!_root.gameObject.activeSelf) return;

            Bounds b = _body.BodyBounds;
            float w = Mathf.Max(0.5f, b.size.x), feetY = b.min.y, topY = b.max.y, cx = b.center.x;
            var main = _body.MainRenderer;
            int layer = main != null ? main.sortingLayerID : 0, order = main != null ? main.sortingOrder : 0;

            // 2. Vòng dưới chân (nằm sau thân): tăng sức mạnh > chậm > hào quang
            Color ringColor = buff ? StatusColor(StatusEffect.Buff) : slow ? StatusColor(StatusEffect.Slow) : glow > 0 ? GlowColors[glow] : Color.clear;
            _ring.enabled = ringColor.a > 0f;
            if (_ring.enabled)
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(now * 4f);
                ringColor.a = pulse;
                _ring.color = ringColor;
                float rw = w * 2.2f;
                _ring.transform.position = new Vector3(cx, feetY + 0.06f, 0f);
                _ring.transform.localScale = new Vector3(rw, rw * 0.28f, 1f);
                SetSort(_ring, layer, order + (int)RingBehind);
            }

            // 3. Choáng: 3 sao xoay quanh đỉnh đầu (sao ở nửa vòng sau thì tối hơn → cảm giác vòng tròn nằm ngang)
            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                s.enabled = stun;
                if (!stun) continue;
                float a = now * 5f + i * Mathf.PI * 2f / _stars.Length;
                float front = Mathf.Sin(a);
                s.transform.position = new Vector3(cx + Mathf.Cos(a) * Mathf.Max(0.3f, w * 0.5f), topY + 0.18f + front * 0.06f, 0f);
                s.transform.localScale = Vector3.one * (0.24f + 0.04f * front);
                var c = StatusColor(StatusEffect.Stun); c.a = front >= 0 ? 1f : 0.55f;
                s.color = c;
                SetSort(s, layer, order + (int)InFront);
            }

            // 4. Tia bay lên: bỏng (tàn lửa dày), tăng sức mạnh (đốm xanh), hào quang (đốm màu bậc, thưa)
            float rate = (burn ? 16f : 0f) + (buff ? 7f : 0f) + glow * 2.5f;
            _sparkDebt += rate * Time.deltaTime;
            while (_sparkDebt >= 1f)
            {
                _sparkDebt -= 1f;
                // Chọn loại tia theo tỉ trọng tốc độ sinh của từng loại
                float pick = Random.value * rate;
                if (burn && (pick -= 16f) < 0f)
                    Emit(new Vector3(cx + Random.Range(-0.4f, 0.4f) * w, Random.Range(feetY, Mathf.Lerp(feetY, topY, 0.7f)), 0f),
                         Color.Lerp(StatusColor(StatusEffect.Burn), new Color(1f, 0.85f, 0.2f), Random.value), 1.3f, 0.6f, 0.3f);
                else if (buff && (pick -= 7f) < 0f)
                    Emit(new Vector3(cx + Random.Range(-0.6f, 0.6f) * w, feetY + Random.Range(0f, 0.3f), 0f), StatusColor(StatusEffect.Buff), 1.6f, 0.8f, 0.22f);
                else if (glow > 0)
                    Emit(new Vector3(cx + Random.Range(-0.8f, 0.8f) * w, feetY + Random.Range(0f, 0.2f), 0f), GlowColors[glow], 1.1f, 0.9f, 0.2f);
            }
            foreach (var sp in _sparks)
            {
                if (!sp.r.enabled) continue;
                sp.age += Time.deltaTime;
                if (sp.age >= sp.life) { sp.r.enabled = false; continue; }
                float t = sp.age / sp.life;
                sp.pos += sp.vel * Time.deltaTime;
                sp.r.transform.position = sp.pos;
                sp.r.transform.localScale = Vector3.one * sp.size * (1f - 0.5f * t);
                var c = sp.color; c.a = 1f - t * t;
                sp.r.color = c;
                SetSort(sp.r, layer, order + (int)InFront);
            }
        }

        private static Color StatusColor(StatusEffect e) => UI.StatusIconsHud.Colors[(int)e];

        private void ApplyTint(Color c)
        {
            if (c == _tint) return;
            _tint = c;
            if (_body != null || TryGetComponent(out _body)) _body.SetStatusTint(c);
        }

        private void Emit(Vector3 pos, Color color, float speed, float life, float size)
        {
            Spark sp = _sparks.Find(s => !s.r.enabled);
            if (sp == null)
            {
                if (_sparks.Count >= MaxSparks) return;
                sp = new Spark { r = NewRenderer("Spark", FxShapes.Dot) };
                _sparks.Add(sp);
            }
            sp.pos = pos;
            sp.vel = new Vector3(Random.Range(-0.15f, 0.15f), speed * Random.Range(0.7f, 1.1f), 0f);
            sp.age = 0f;
            sp.life = life * Random.Range(0.8f, 1.2f);
            sp.size = size;
            sp.color = color;
            sp.r.transform.position = pos;
            sp.r.enabled = true;
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("Aura_" + name).transform;
            _ring = NewRenderer("Ring", FxShapes.Dot);
            for (int i = 0; i < _stars.Length; i++) _stars[i] = NewRenderer("Star" + i, FxShapes.Star);
            _sparks.Clear();
        }

        private SpriteRenderer NewRenderer(string n, Sprite sprite)
        {
            var go = new GameObject(n);
            go.transform.SetParent(_root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.enabled = false;
            return r;
        }

        private static void SetSort(SpriteRenderer r, int layer, int order)
        {
            if (r.sortingLayerID != layer) r.sortingLayerID = layer;
            if (r.sortingOrder != order) r.sortingOrder = order;
        }

        // Thân bị cất vào pool / ẩn → ẩn hình theo; bị huỷ → huỷ hình
        private void OnDisable() { if (_root != null) _root.gameObject.SetActive(false); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
