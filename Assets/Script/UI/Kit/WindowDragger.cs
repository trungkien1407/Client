using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Script.UI.Kit
{
    /// <summary>Kéo thanh tiêu đề để di chuyển cửa sổ.</summary>
    public class WindowDragger : MonoBehaviour, IDragHandler
    {
        public RectTransform target;
        public void OnDrag(PointerEventData e)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            target.anchoredPosition += e.delta / (canvas != null ? canvas.scaleFactor : 1f);
        }
    }
}
