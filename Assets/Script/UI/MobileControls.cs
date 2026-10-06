using Assets.Script.Combat;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// ĐIỀU KHIỂN CẢM ỨNG cho Android/iOS (dựng bằng code).
    ///  - Joystick ảo trái  → giả lập "&lt;Gamepad&gt;/leftStick"   (action Move đã gắn binding này)
    ///  - Nút Nhảy           → giả lập "&lt;Gamepad&gt;/buttonSouth" (action Jump)
    ///  - Nút Đánh / Nói     → gọi thẳng CombatNetwork / GameHud
    /// Nhờ "giả lập tay cầm" nên PlayerMovement KHÔNG cần sửa: nó vẫn đọc action Move/Jump như bàn phím.
    ///
    /// Chỉ hiện khi máy có màn hình cảm ứng (điện thoại, hoặc Device Simulator trong Editor).
    /// [CẦN ĐIỀN khi có art] gán sprite nền/nút joystick: StickBgSprite, StickKnobSprite.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        public static Sprite StickBgSprite, StickKnobSprite;

        private GameObject _root;

        public static bool ShouldShow => Application.isMobilePlatform || Touchscreen.current != null;

        private void Update()
        {
            bool show = ShouldShow && LocalPlayerState.Id >= 0;
            if (show && _root == null) Build();
            if (_root != null && _root.activeSelf != show) _root.SetActive(show);
        }

        private void Build()
        {
            _root = UIKit.Rect("MobileControls", UIRoot.Instance.HudLayer).Fill().gameObject;

            // ---- Joystick trái ----
            var bg = UIKit.Panel("StickBg", _root.transform, new Color(1, 1, 1, 0.12f));
            if (StickBgSprite != null) bg.sprite = StickBgSprite;
            bg.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(150, 150), new Vector2(200, 200));

            var knobGo = UIKit.Rect("StickKnob", bg.transform).gameObject;
            knobGo.SetActive(false); // tắt trước khi thêm OnScreenStick để set controlPath trước OnEnable
            var knob = knobGo.AddComponent<Image>();
            knob.color = new Color(1, 1, 1, 0.45f);
            if (StickKnobSprite != null) knob.sprite = StickKnobSprite;
            ((RectTransform)knobGo.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            var stick = knobGo.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 70;
            knobGo.SetActive(true);

            // ---- Nút phải ----
            MakeOnScreenButton("Nhảy", "<Gamepad>/buttonSouth", new Vector2(-70, 150));
            MakeActionButton("Đánh", new Vector2(-190, 90), () =>
            {
                if (SkillBarManager.Instance != null) CombatNetwork.Instance?.TryUseSkill(SkillBarManager.Instance.GetSelectedSkillId());
            });
            MakeActionButton("Nói", new Vector2(-190, 210), GameHud.TalkToNearestNpc);
        }

        private void MakeOnScreenButton(string label, string controlPath, Vector2 pos)
        {
            var btn = UIKit.Button(label, _root.transform, label, null, 20);
            ((RectTransform)btn.transform).Place(new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(110, 110));
            btn.image.color = new Color(1, 1, 1, 0.18f);
            btn.gameObject.SetActive(false);
            var osb = btn.gameObject.AddComponent<OnScreenButton>();
            osb.controlPath = controlPath;
            btn.gameObject.SetActive(true);
        }

        private void MakeActionButton(string label, Vector2 pos, System.Action onClick)
        {
            var btn = UIKit.Button(label, _root.transform, label, onClick, 20);
            ((RectTransform)btn.transform).Place(new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(100, 100));
            btn.image.color = new Color(1, 0.6f, 0.2f, 0.25f);
        }
    }
}
