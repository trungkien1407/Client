using Assets.Script.Player;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.UI
{
    /// <summary>
    /// ĐIỀU KHIỂN CẢM ỨNG cho Android/iOS: hiện MobileControlsView (prefab UI/Hud) khi máy có màn hình cảm ứng
    /// (điện thoại, hoặc Device Simulator trong Editor) và đang trong game.
    /// Joystick / nút Nhảy giả lập tay cầm nên PlayerMovement KHÔNG cần sửa: vẫn đọc action Move / Jump như bàn phím.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        private GameObject _root;

        public static bool ShouldShow => Application.isMobilePlatform || Touchscreen.current != null;

        private void Update()
        {
            bool show = ShouldShow && LocalPlayerState.Id >= 0;
            if (show && _root == null) _root = HudPanel.Create<MobileControlsView>(UIRoot.Instance.HudLayer).gameObject;
            if (_root != null && _root.activeSelf != show) _root.SetActive(show);
        }
    }
}
