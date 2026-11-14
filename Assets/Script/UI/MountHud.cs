using Assets.Script.Data;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// NÚT "Cưỡi" trên HUD (GĐ28 — ngay dưới nút Auto): bấm = lên / xuống thú cưỡi (như phím R; điện thoại không có phím nên cần nút này).
    /// Chỉ hiện khi đang mang thú (GameData.MyPet). Đang cưỡi → nút vàng "Xuống (R)". Dựng bằng code, GameHud gọi Create.
    /// </summary>
    public class MountHud : MonoBehaviour
    {
        public static readonly Vector2 Pos = AutoHud.Pos + new Vector2(0, -AutoHud.Size.y - 8), Size = AutoHud.Size;
        private Button _btn;
        private bool _shownRiding, _first = true;
        private float _next;

        public static MountHud Create(Transform parent)
        {
            var btn = UIKit.Button("MountBtn", parent, "Cưỡi (R)", Fx.PetMount.ToggleLocal, 14);
            ((RectTransform)btn.transform).Place(new Vector2(1, 1), new Vector2(1, 1), Pos, Size, new Vector2(1, 1));
            UIStyle.Chip(btn, 14);
            var hud = btn.gameObject.AddComponent<MountHud>();
            hud._btn = btn;
            return hud;
        }

        private void Update()
        {
            if (Time.unscaledTime < _next || _btn == null) return;
            _next = Time.unscaledTime + 0.2f;
            bool has = GameData.MyPet != null && !LocalPlayerState.IsDead;
            var img = _btn.image;
            // Ẩn bằng cách tắt hình + chữ (giữ object để Update vẫn chạy)
            if (img != null) img.enabled = has;
            _btn.interactable = has;
            foreach (var t in _btn.GetComponentsInChildren<Graphic>(true)) if (t != img) t.enabled = has;
            if (!has) return;
            bool riding = LocalPlayerState.Riding;
            if (riding == _shownRiding && !_first) return;
            _first = false;
            _shownRiding = riding;
            _btn.SetLabel(riding ? "Xuống (R)" : "Cưỡi (R)");
            UIStyle.SetChipOn(_btn, riding);
        }
    }
}
