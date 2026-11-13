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
