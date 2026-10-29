using Assets.Script.Combat;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// NÚT "Auto" trên HUD (góc phải trên, ngay dưới nút Khu — dưới bản đồ nhỏ): bấm = bật / tắt AutoPlay (như phím Z),
    /// đang bật thì nút vàng + chữ "Auto: Bật". Tuỳ chọn tự động: cửa sổ AutoWindow (phím U / Cài đặt → Tự động). Dựng bằng code, GameHud gọi Create.
    /// Trước 2026-10-29 nằm cạnh phải thanh kỹ năng — thanh lên 9 ô thì đè nhau. Muốn dời nút: sửa Pos (neo góc phải trên).
    /// </summary>
    public class AutoHud : MonoBehaviour
    {
        public static readonly Vector2 Pos = new Vector2(-8, -MinimapHud.H - 14 - 30 - 20), Size = new Vector2(MinimapHud.W, 30);   // dưới nút Khu (GameHudView.zoneBtn), cách 20
        private Button _btn;
        private Color _normal;
        private bool _hasNormal;

        public static AutoHud Create(Transform parent)
        {
            var btn = UIKit.Button("AutoBtn", parent, "Auto", AutoPlay.Toggle, 14);
            ((RectTransform)btn.transform).Place(new Vector2(1, 1), new Vector2(1, 1), Pos, Size, new Vector2(1, 1));
            var hud = btn.gameObject.AddComponent<AutoHud>();
            hud._btn = btn;
            return hud;
        }

        private void Start()
        {
            AutoPlay.OnToggled += Show;
            if (AutoPlay.IsOn) Show(true);
        }

        private void OnDestroy() => AutoPlay.OnToggled -= Show;

        private void Show(bool on)
        {
            if (_btn == null) return;
            // Màu gốc lấy ở lần đổi đầu tiên: UISkin tô nút SAU Start (SkinButtonLater) — lấy sớm sẽ ra màu chưa skin
            if (!_hasNormal && _btn.image != null) { _normal = _btn.image.color; _hasNormal = true; }
            _btn.SetLabel(on ? "Auto: Bật" : "Auto");
            if (_btn.image != null) _btn.image.color = on ? UIKit.ButtonHot : _normal;
        }
    }
}
