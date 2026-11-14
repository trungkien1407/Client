using Assets.Script.Core;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// NÚT DI CHUYỂN / NHẢY / ĐÁNH TRÊN MÀN HÌNH (khung "Joystick" + "AttackBtn" có sẵn trong scene HUDCanvas).
    ///   Up (2) = sang trái · Up (1) = sang phải (GIỮ để đi) · Up = nhảy · AttackBtn = đánh (CombatInput nối onClick).
    /// Giữ nút trái / phải → PlayerMovement cộng hướng này vào ManualInput như bấm phím → mọi luật cũ vẫn đúng
    /// (Tự đi / Enter tự chạy thấy "người chơi tự đi" thì thôi; mở cửa sổ / choáng thì không đi).
    /// Ngược lại: bấm phím A/D/←/→, W/↑, J thì nút tương ứng trên màn hình cũng lún xuống + tối màu, như được bấm.
    /// Scene bật HUD sau đăng nhập → tìm nút mỗi giây tới khi thấy (giống CombatInput.HookAttackButton).
    /// </summary>
    public class HudPad : MonoBehaviour
    {
        /// <summary>Hướng ngang từ nút màn hình: -1 trái · 0 · 1 phải. PlayerMovement đọc.</summary>
        public static float Horizontal { get; private set; }

        private Pad _left, _right, _jump, _attack;
        private float _nextFind;

        private void Update()
        {
            if (_left == null && Time.unscaledTime >= _nextFind) Find();
            if (_left == null) { Horizontal = 0f; return; }

            var me = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            Horizontal = (_right.Held ? 1f : 0f) - (_left.Held ? 1f : 0f);
            if (_jump.Down && me != null && !UIRoot.GameplayBlocked) me.Jump();

            // Nút sáng theo phím: tay bấm trên màn hình HOẶC phím đang giữ (ManualInput đã gồm cả phím lẫn nút màn hình)
            float x = me != null ? me.ManualInput.x : 0f;
            _left.Show(_left.Held || x < -0.1f);
            _right.Show(_right.Held || x > 0.1f);
            _jump.Show(_jump.Held || (me != null && me.JumpHeld));
            if (_attack != null) _attack.Show(_attack.Held || GameInput.Held(GameKey.Attack) || GameInput.Held(GameKey.Action));
            UpdateAttackIcon();
            _jump.Down = false;
        }

        private void Find()
        {
            _nextFind = Time.unscaledTime + 1f;
            var l = GameObject.Find("HUDCanvas/Joystick/Up (2)");
            var r = GameObject.Find("HUDCanvas/Joystick/Up (1)");
            var j = GameObject.Find("HUDCanvas/Joystick/Up");
            if (l == null || r == null || j == null) return;
            _left = Pad.On(l); _right = Pad.On(r); _jump = Pad.On(j);
            var a = GameObject.Find("AttackBtn");
            if (a != null) _attack = Pad.On(a);
        }

        private void OnDisable() => Horizontal = 0f;

        // ---- Nút Đánh đổi hình thành CHAT khi đang chọn NPC / người không phải đối thủ (2026-11-15) ----
        private UnityEngine.UI.Image _attackImg, _talkIcon;
        private bool _talkShown;
        private float _nextIcon;

        /// <summary>
        /// Chọn NPC / người chơi (không tỉ thí, không Đồ sát) → nút Đánh hiện biểu tượng chat (ẩn nắm đấm), bấm = nói chuyện / mở bảng tương tác
        /// (CombatInput.CastSelected chuyển sang ActionInput). Bỏ chọn / chọn quái → nắm đấm như cũ. Kiểm 6 lần / giây.
        /// </summary>
        private void UpdateAttackIcon()
        {
            if (_attack == null || Time.unscaledTime < _nextIcon) return;
            _nextIcon = Time.unscaledTime + 0.15f;
            if (_attackImg == null) _attackImg = _attack.GetComponent<UnityEngine.UI.Image>();
            bool talk = Combat.ActionInput.TargetIsTalk();
            if (talk == _talkShown || _attackImg == null) return;
            _talkShown = talk;
            if (_talkIcon == null)
            {
                // biểu tượng chat (hình kho "menu_talk" nếu có; không có → bong bóng vẽ bằng chữ) đè giữa nút, nắm đấm mờ đi
                var go = new GameObject("TalkIcon", typeof(RectTransform));
                go.transform.SetParent(_attack.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.18f, 0.18f); rt.anchorMax = new Vector2(0.82f, 0.82f); rt.offsetMin = rt.offsetMax = Vector2.zero;
                var sp = UISkin.Get("menu_talk");
                if (sp != null)
                {
                    _talkIcon = go.AddComponent<UnityEngine.UI.Image>();
                    _talkIcon.sprite = sp; _talkIcon.preserveAspect = true; _talkIcon.raycastTarget = false;
                }
                else
                {
                    var t = go.AddComponent<TMPro.TextMeshProUGUI>();
                    t.text = "Nói"; t.fontSize = 30; t.fontStyle = TMPro.FontStyles.Bold; t.alignment = TMPro.TextAlignmentOptions.Center; t.raycastTarget = false;
                    UIStyle.Readable(t);
                }
            }
            _attackImg.color = talk ? new Color(1f, 1f, 1f, 0.25f) : Color.white;   // nắm đấm mờ đi khi là nút chat
            var icon = _attack.transform.Find("TalkIcon");
            if (icon != null) icon.gameObject.SetActive(talk);
        }

        /// <summary>Nút Đánh đang hiện hình chat (AutoTestRunner đọc).</summary>
        public bool AttackShowsTalk => _talkShown;

        /// <summary>1 nút: biết đang bị giữ (chuột / ngón tay) + hiện trạng thái "đang bấm" (lún + tối).</summary>
        public class Pad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool Held { get; private set; }
            /// <summary>Vừa bấm xuống trong khung này (HudPad dùng cho nút nhảy rồi tự xoá).</summary>
            public bool Down { get; set; }

            private Graphic _g;
            private Color _color;
            private Vector3 _scale;
            private bool _shown;

            public static Pad On(GameObject go)
            {
                var p = go.GetComponent<Pad>();
                if (p != null) return p;
                p = go.AddComponent<Pad>();
                p._g = go.GetComponent<Graphic>();
                if (p._g != null) { p._g.raycastTarget = true; p._color = p._g.color; }
                p._scale = go.transform.localScale;
                return p;
            }

            public void OnPointerDown(PointerEventData e) { Held = true; Down = true; }
            public void OnPointerUp(PointerEventData e) => Held = false;
            public void OnPointerExit(PointerEventData e) => Held = false;   // kéo ngón tay ra ngoài nút = thả
            private void OnDisable() { Held = false; Show(false); }

            /// <summary>Đang bấm: thu nhỏ 88% + tối màu; thả: về như cũ. Chỉ đổi khi trạng thái đổi.</summary>
            public void Show(bool pressed)
            {
                if (pressed == _shown) return;
                _shown = pressed;
                transform.localScale = pressed ? _scale * 0.88f : _scale;
                if (_g != null) _g.color = pressed ? new Color(_color.r * 0.7f, _color.g * 0.7f, _color.b * 0.7f, _color.a) : _color;
            }

            /// <summary>Đang hiện trạng thái bấm (AutoTestRunner đọc).</summary>
            public bool Shown => _shown;
        }

        /// <summary>Cho AutoTestRunner: nút trái / phải / nhảy / đánh đang hiện "bấm" không.</summary>
        public static HudPad Instance => FindAnyObjectByType<HudPad>();
        public Pad Left => _left;
        public Pad Right => _right;
        public Pad JumpPad => _jump;
        public Pad Attack => _attack;
    }
}
