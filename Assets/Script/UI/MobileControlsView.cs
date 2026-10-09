using Assets.Script.Combat;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// HÌNH NÚT CẢM ỨNG (prefab Resources/UI/Hud/MobileControlsView): joystick trái, nút Nhảy / Đánh / Nói bên phải.
    /// Joystick + nút Nhảy giả lập tay cầm (OnScreenStick / OnScreenButton — đường dẫn control lưu trong prefab);
    /// Đánh / Nói nối trong Bind. Thay sprite nền / nút joystick ngay trong prefab.
    /// </summary>
    public class MobileControlsView : HudPanel
    {
        [SerializeField] private Button _attack, _talk;

        protected override void Build()
        {
            // ---- Joystick trái ----
            var bg = UIKit.Panel("StickBg", transform, new Color(1, 1, 1, 0.12f));
            bg.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(150, 150), new Vector2(200, 200));
            var knobGo = UIKit.Rect("StickKnob", bg.transform).gameObject;
            knobGo.SetActive(false); // tắt trước khi thêm OnScreenStick để đặt controlPath trước OnEnable
            var knob = knobGo.AddComponent<Image>();
            knob.color = new Color(1, 1, 1, 0.45f);
            ((RectTransform)knobGo.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            var stick = knobGo.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 70;
            knobGo.SetActive(true);

            // ---- Nút phải ----
            var jump = UIKit.Button("Nhảy", transform, "Nhảy", null, 20);
            ((RectTransform)jump.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 150), new Vector2(110, 110));
            jump.image.color = new Color(1, 1, 1, 0.18f);
            jump.gameObject.SetActive(false);
            jump.gameObject.AddComponent<OnScreenButton>().controlPath = "<Gamepad>/buttonSouth";
            jump.gameObject.SetActive(true);

            _attack = ActionButton("Đánh", new Vector2(-190, 90));
            _talk = ActionButton("Nói", new Vector2(-190, 210));
        }

        private Button ActionButton(string label, Vector2 pos)
        {
            var btn = UIKit.Button(label, transform, label, null, 20);
            ((RectTransform)btn.transform).Place(new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(100, 100));
            btn.image.color = new Color(1, 0.6f, 0.2f, 0.25f);
            return btn;
        }

        protected override void Bind()
        {
            _attack.onClick.AddListener(CombatInput.CastSelected);
            _talk.onClick.AddListener(Data.NpcNetwork.TalkToNearest);
        }
    }
}
