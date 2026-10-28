using Assets.Script.Combat;
using Assets.Script.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// NÚT "Auto" trên HUD (cạnh phải thanh kỹ năng): bấm = bật / tắt AutoPlay (như phím Z), đang bật thì nút vàng + chữ "Auto: Bật".
    /// Tuỳ chọn tự động: cửa sổ AutoWindow (phím U / Cài đặt → Tự động). Dựng bằng code, GameHud gọi Create.
    /// Muốn dời nút: sửa Pos (chuẩn 1280×720, neo giữa-đáy màn hình).
    /// </summary>
    public class AutoHud : MonoBehaviour
    {
        public static readonly Vector2 Pos = new Vector2(300, 18), Size = new Vector2(92, 44);
        private Button _btn;
        private Color _normal;
        private bool _hasNormal;

        public static AutoHud Create(Transform parent)
        {
            var btn = UIKit.Button("AutoBtn", parent, "Auto", AutoPlay.Toggle, 17);
            ((RectTransform)btn.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Pos, Size, new Vector2(0.5f, 0));
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
