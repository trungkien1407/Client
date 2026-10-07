using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// Canvas chung cho mọi cửa sổ game dựng bằng code. Tự tạo khi cần (UIRoot.Instance).
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

        public RectTransform WindowLayer { get; private set; }  // cửa sổ (kéo được)
        public RectTransform HudLayer { get; private set; }     // HUD cố định (thanh EXP, nút menu)

        private readonly List<GameWindow> _windows = new List<GameWindow>();

        private static void Create()
        {
            var go = new GameObject("[UIRoot]");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // trên HUD cũ của scene
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); // thiết kế theo 1280x720, tự co giãn PC/điện thoại
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            _instance = go.AddComponent<UIRoot>();
            _instance.HudLayer = UIKit.Rect("HUD", go.transform).Fill();
            _instance.WindowLayer = UIKit.Rect("Windows", go.transform).Fill();

            // Scene đã có EventSystem; nếu chưa (test riêng) thì tạo
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(es);
            }
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
