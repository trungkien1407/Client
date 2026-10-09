using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// Canvas chung cho HUD + cửa sổ game. Có prefab Resources/UI/UIRoot thì tạo từ đó (chỉnh độ phân giải chuẩn,
    /// thứ tự vẽ trong Editor), không thì dựng bằng code. Tự tạo khi cần (UIRoot.Instance).
    /// - Esc: đóng cửa sổ trên cùng.
    /// - Phím tắt mở cửa sổ: mỗi GameWindow khai báo HotKey riêng.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        private static UIRoot _instance;
        public static UIRoot Instance
        {
            get
            {
                if (_instance == null) Create();
                return _instance;
            }
        }

        [SerializeField] private RectTransform hudLayer;      // HUD cố định (thanh EXP, nút menu)
        [SerializeField] private RectTransform windowLayer;   // cửa sổ (kéo được)
        public RectTransform HudLayer => hudLayer;
        public RectTransform WindowLayer => windowLayer;

        private readonly List<GameWindow> _windows = new List<GameWindow>();

        private static void Create()
        {
            var prefab = Resources.Load<GameObject>(UIPrefabs.RootPath);
            var go = prefab != null ? Instantiate(prefab) : BuildByCode();
            go.name = "[UIRoot]";
            DontDestroyOnLoad(go);
            _instance = go.GetComponent<UIRoot>();

            // Scene đã có EventSystem; nếu chưa (test riêng) thì tạo
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(es);
            }
        }

        /// <summary>Dựng canvas bằng code (tool cũng dùng để sinh prefab UIRoot lần đầu).</summary>
        public static GameObject BuildByCode()
        {
            var go = new GameObject("[UIRoot]");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // trên HUD cũ của scene
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); // thiết kế theo 1280x720, tự co giãn PC/điện thoại
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var root = go.AddComponent<UIRoot>();
            root.hudLayer = UIKit.Rect("HUD", go.transform).Fill();
            root.windowLayer = UIKit.Rect("Windows", go.transform).Fill();
            return go;
        }

        public void Register(GameWindow w) => _windows.Add(w);

        /// <summary>
        /// Đóng mọi cửa sổ đang mở — gọi khi đăng xuất / mất kết nối / bị đưa ra vì bảo trì,
        /// để cửa sổ game (NPC, túi...) không nằm đè lên màn đăng nhập và che popup thông báo.
        /// Chưa từng tạo UIRoot thì thôi (không tạo mới).
        /// </summary>
        public static void CloseAll()
        {
            if (_instance == null) return;
            foreach (var w in _instance._windows)
                if (w != null && w.IsOpen) w.Hide();
        }

        public void BringToFront(GameWindow w) => w.transform.SetAsLastSibling();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || Assets.Script.Combat.ChatBox.IsTyping) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                // Đóng cửa sổ trên cùng đang mở
                for (int i = WindowLayer.childCount - 1; i >= 0; i--)
                {
                    var w = WindowLayer.GetChild(i).GetComponent<GameWindow>();
                    if (w != null && w.IsOpen) { w.Hide(); break; }
                }
            }

            foreach (var w in _windows)
                if (w.HotKey != Key.None && kb[w.HotKey].wasPressedThisFrame) w.Toggle();
        }
    }
}
