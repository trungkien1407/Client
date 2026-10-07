using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// LỚP NỀN CỬA SỔ: khung + thanh tiêu đề (kéo để di chuyển) + nút X + phím tắt bật/tắt.
    /// Lớp con chỉ cần: đặt Title/Size/HotKey trong Build(), vẽ nội dung vào Body, và Refresh() khi dữ liệu đổi.
    ///
    ///   public class BagWindow : GameWindow
    ///   {
    ///       protected override void Build() { Title = "Túi đồ"; HotKey = Key.I; ... vẽ vào Body ... }
    ///       protected override void Refresh() { ... vẽ lại khi mở hoặc khi dữ liệu đổi ... }
    ///   }
    ///   GameWindow.Open&lt;BagWindow&gt;();   // mở từ bất kỳ đâu
    /// </summary>
    public abstract class GameWindow : MonoBehaviour, IPointerDownHandler
    {
        public string Title { get; protected set; } = "Cửa sổ";
        public Key HotKey { get; protected set; } = Key.None;
        protected Vector2 Size = new Vector2(520, 420);

        protected RectTransform Body { get; private set; }
        private TextMeshProUGUI _titleText;
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Lấy (tạo nếu chưa có) cửa sổ loại T.</summary>
        public static T Get<T>() where T : GameWindow
        {
            var root = UIRoot.Instance;
            var existing = root.WindowLayer.GetComponentInChildren<T>(true);
            if (existing != null) return existing;

            var rt = UIKit.Rect(typeof(T).Name, root.WindowLayer);
            var w = rt.gameObject.AddComponent<T>();
            w.Construct();
            rt.gameObject.SetActive(false);
            root.Register(w);
            return w;
        }

        public static T Open<T>() where T : GameWindow
        {
            var w = Get<T>();
            w.Show();
            return w;
        }

        /// <summary>Tạo sẵn cửa sổ (để phím tắt hoạt động ngay cả khi chưa mở lần nào).</summary>
        public static void Preload<T>() where T : GameWindow => Get<T>();

        private void Construct()
        {
            Build(); // lớp con đặt Title, Size, HotKey + vẽ nội dung

            var rt = (RectTransform)transform;
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Size);
            var bg = gameObject.AddComponent<Image>();
            bg.color = UIKit.PanelColor;

            var header = UIKit.Panel("Header", transform, UIKit.HeaderColor);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 36), new Vector2(0.5f, 1));
            header.gameObject.AddComponent<WindowDragger>().target = rt;
            _titleText = UIKit.Text("Title", header.transform, Title, 20, TextAlignmentOptions.Center);
            _titleText.rectTransform.Fill(40, 40);
            var close = UIKit.Button("Close", header.transform, "X", Hide, 18);
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(32, 28));

            // Body tạo ở Build() trước khi khung tồn tại → đưa lên trên nền + chừa chỗ tiêu đề
            Body.SetAsLastSibling();
            Body.Fill(10, 10, 44, 10);
        }

        /// <summary>Lớp con gọi đầu Build() để có vùng nội dung.</summary>
        protected RectTransform CreateBody()
        {
            Body = UIKit.Rect("Body", transform);
            return Body;
        }

        protected abstract void Build();
        protected virtual void Refresh() { }

        public void Show()
        {
            gameObject.SetActive(true);
            UIRoot.Instance.BringToFront(this);
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// Xếp 2 cửa sổ cạnh nhau (vd Rương | Hành trang) vừa khít bề ngang màn hình thật (PC 16:9 hay điện thoại 20:9).
        /// Không đủ chỗ thì cho chồng nhẹ lên nhau — cửa sổ phải nằm trên.
        /// </summary>
        public static void SideBySide(GameWindow left, GameWindow right)
        {
            float screenW = ((RectTransform)UIRoot.Instance.WindowLayer).rect.width;
            float lw = left.Size.x, rw = right.Size.x, margin = 8f;
            float half = screenW / 2f;
            float lx = -half + margin + lw / 2f, rx = half - margin - rw / 2f;
            float gap = (rx - rw / 2f) - (lx + lw / 2f);
            if (gap > 20f) { lx += gap / 2f - 10f; rx -= gap / 2f - 10f; } // màn rộng: dồn vào giữa
            ((RectTransform)left.transform).anchoredPosition = new Vector2(lx, 0);
            ((RectTransform)right.transform).anchoredPosition = new Vector2(rx, 0);
            UIRoot.Instance.BringToFront(right);
        }

        public void SetTitle(string title)
        {
            Title = title;
            if (_titleText != null) _titleText.text = title;
        }
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        /// <summary>Vẽ lại nếu đang mở (gọi khi dữ liệu thay đổi).</summary>
        public void RefreshIfOpen() { if (IsOpen) Refresh(); }

        public void OnPointerDown(PointerEventData e) => UIRoot.Instance.BringToFront(this);
    }

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
