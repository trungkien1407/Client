using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// LỚP NỀN CỬA SỔ: khung (UI/Kit/Window — tiêu đề kéo được, nút X, vùng Body) + phím tắt bật/tắt.
    ///
    /// Có prefab Resources/UI/Windows/&lt;Lớp&gt; → tạo từ prefab (bố cục chỉnh bằng kéo-thả trong Editor), rồi Bind().
    /// Chưa có → dựng bằng code: Build() rồi Bind(). Lớp con:
    ///   Configure()  Title / HotKey / Size (Size chỉ dùng khi dựng bằng code)
    ///   Build()      BỐ CỤC: tạo phần tử vào Body, gán vào các ô [SerializeField] — tool chạy hàm này để xuất prefab
    ///   Bind()       HÀNH VI: nối nút → hàm, nghe GameData.OnChanged (chạy cả khi tạo từ prefab)
    ///   Refresh()    vẽ lại nội dung khi mở / khi dữ liệu đổi
    /// Cửa sổ cũ làm hết trong Build() (chưa tách Bind) thì chỉ chạy bằng code, tool không xuất prefab.
    ///   GameWindow.Open&lt;InventoryWindow&gt;();   // mở từ bất kỳ đâu
    /// </summary>
    public abstract class GameWindow : MonoBehaviour, IPointerDownHandler
    {
        public string Title { get; protected set; } = "Cửa sổ";
        public Key HotKey { get; protected set; } = Key.None;
        protected Vector2 Size = new Vector2(520, 420);

        protected WindowFrame Frame { get; private set; }
        protected RectTransform Body => Frame.body;
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Lấy (tạo nếu chưa có) cửa sổ loại T.</summary>
        public static T Get<T>() where T : GameWindow
        {
            var root = UIRoot.Instance;
            var existing = root.WindowLayer.GetComponentInChildren<T>(true);
            if (existing != null) return existing;

            T w;
            var prefab = UIPrefabs.Window(typeof(T));
            if (prefab != null)
            {
                var go = UIPrefabs.Spawn(prefab, root.WindowLayer);
                go.name = typeof(T).Name;
                w = go.GetComponent<T>();
                w.Frame = w.GetComponent<WindowFrame>();
                w.Configure();
            }
            else w = (T)CreateByCode(typeof(T), root.WindowLayer);

            w.Frame.close.onClick.AddListener(w.Hide);
            w.Bind();
            w.Frame.title.text = w.Title;
            w.gameObject.SetActive(false);
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

        /// <summary>Dựng cửa sổ bằng code: khung + Configure + Build (chưa Bind). Tool xuất prefab cũng gọi hàm này.</summary>
        public static GameWindow CreateByCode(Type type, Transform parent)
        {
            var frame = UIKit.WindowFrame(type.Name, parent);
            var w = (GameWindow)frame.gameObject.AddComponent(type);
            w.Frame = frame;
            w.Configure();
            w.Build();
            ((RectTransform)w.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, w.Size);
            frame.title.text = w.Title;
            return w;
        }

        /// <summary>Cửa sổ đã tách Bind() (tạo được từ prefab) — tool chỉ xuất những cửa sổ này.</summary>
        public static bool SupportsPrefab(Type type)
            => type.GetMethod(nameof(Bind), BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType == type;

        private void Awake() { if (Frame == null) Frame = GetComponent<WindowFrame>(); }

        /// <summary>Cửa sổ cũ gọi đầu Build() để lấy vùng nội dung.</summary>
        protected RectTransform CreateBody() => Body;

        protected virtual void Configure() { }
        protected abstract void Build();
        protected virtual void Bind() { }
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
            float lw = ((RectTransform)left.transform).rect.width, rw = ((RectTransform)right.transform).rect.width, margin = 8f;
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
            if (Frame != null) Frame.title.text = title;
        }
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        /// <summary>Vẽ lại nếu đang mở (gọi khi dữ liệu thay đổi).</summary>
        public void RefreshIfOpen() { if (IsOpen) Refresh(); }

        public void OnPointerDown(PointerEventData e) => UIRoot.Instance.BringToFront(this);
    }
}
