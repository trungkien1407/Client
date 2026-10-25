using UnityEngine;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// Ảnh nền PHỦ KÍN canvas ở mọi tỉ lệ màn hình (16:9, 20:9 điện thoại, 4:3 máy tính bảng): phóng theo cạnh thiếu,
    /// cắt bớt mép thừa, không méo. Gắn vào Image nền neo giữa (cha không cần là RectTransform).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class CanvasCover : MonoBehaviour
    {
        [Tooltip("Cỡ gốc của ảnh (giữ đúng tỉ lệ)")] public Vector2 artSize = new Vector2(1920, 1080);

        void LateUpdate() => Fit();

        public void Fit()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || artSize.x <= 0 || artSize.y <= 0) return;
            var size = ((RectTransform)canvas.rootCanvas.transform).rect.size;
            float s = Mathf.Max(size.x / artSize.x, size.y / artSize.y);
            var rt = (RectTransform)transform;
            var want = artSize * s;
            if ((rt.sizeDelta - want).sqrMagnitude > 0.01f) rt.sizeDelta = want;
        }
    }
}
