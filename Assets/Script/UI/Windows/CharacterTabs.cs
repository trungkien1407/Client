using System;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// NHÓM CỬA SỔ NHÂN VẬT CÓ TAB: Nhân vật (+ tiềm năng) · Hành trang · Kỹ năng · Thú cưng.
    /// Trên HUD chỉ còn 1 nút "Nhân vật"; mở ra có hàng tab phía trên cửa sổ, bấm tab hoặc phím ← / → để chuyển.
    /// Phím tắt cũ (C / I / K / P) vẫn mở thẳng đúng tab.
    ///
    /// Cách làm (không viết lại 4 cửa sổ — mỗi tab vẫn là 1 GameWindow riêng như cũ):
    ///   - GameWindow.Shown báo "vừa mở cửa sổ X". X thuộc nhóm → đóng tab cũ đang mở, đặt X vào ĐÚNG CHỖ tab cũ (trông như 1 cửa sổ
    ///     đổi nội dung), gắn hàng tab (lớp Host) lên X.
    ///   - Hành trang mở cặp với Shop / Rương / Chợ / Giao dịch (SideBySide) → ẩn hàng tab (lúc đó nó là "túi bên cạnh", không phải tab).
    /// Thêm tab mới: thêm 1 dòng vào Tabs.
    /// </summary>
    public static class CharacterTabs
    {
        private static readonly (string label, Type type)[] Tabs =
        {
            ("Nhân vật (C)", typeof(CharacterWindow)),
            ("Hành trang (I)", typeof(InventoryWindow)),
            ("Kỹ năng (K)", typeof(SkillWindow)),
            ("Thú cưng (P)", typeof(PetWindow)),
        };

        private const float TabW = 116f, TabH = 34f, Gap = 5f;
        private static int _stepFrame = -1;   // 1 lần bấm ← / → chỉ đổi 1 tab (tab mới cũng đọc phím trong cùng khung hình)
        private static int _last;          // tab mở gần nhất — nút "Nhân vật" trên HUD mở lại tab này
        private static bool _inited;

        /// <summary>GameHud gọi 1 lần khi tạo HUD.</summary>
        public static void Init()
        {
            if (_inited) return;
            _inited = true;
            GameWindow.Shown += OnShown;
        }

        public static int IndexOf(Type t)
        {
            for (int i = 0; i < Tabs.Length; i++) if (Tabs[i].type == t) return i;
            return -1;
        }

        /// <summary>Nút "Nhân vật" trên HUD: đang mở tab nào trong nhóm → đóng; không thì mở tab gần nhất.</summary>
        public static void Toggle()
        {
            foreach (var t in Tabs)
            {
                var w = Existing(t.type);
                if (w != null && w.IsOpen && !w.HasPartner) { w.Hide(); return; }
            }
            Open(_last);
        }

        public static void Open(int i) => GameWindow.Get(Tabs[(i + Tabs.Length) % Tabs.Length].type).Show();

        /// <summary>Cửa sổ đã tạo (không tạo mới).</summary>
        private static GameWindow Existing(Type t) => (GameWindow)UIRoot.Instance.WindowLayer.GetComponentInChildren(t, true);

        /// <summary>Vừa mở 1 cửa sổ: thuộc nhóm → đóng tab cũ, lấy chỗ của tab cũ, gắn hàng tab.</summary>
        private static void OnShown(GameWindow w)
        {
            int idx = IndexOf(w.GetType());
            if (idx < 0) return;
            _last = idx;
            for (int i = 0; i < Tabs.Length; i++)
            {
                if (i == idx) continue;
                var other = Existing(Tabs[i].type);
                if (other == null || !other.IsOpen || other.HasPartner) continue;
                // cùng tâm với tab cũ → đổi tab không bị nhảy chỗ (cửa sổ cao / rộng khác nhau vẫn canh giữa theo tab cũ)
                ((RectTransform)w.transform).anchoredPosition = ((RectTransform)other.transform).anchoredPosition;
                other.Hide();
            }
            var host = w.GetComponent<Host>() ?? w.gameObject.AddComponent<Host>();
            host.Build(w, idx);
        }

        /// <summary>Hàng tab gắn lên 1 cửa sổ trong nhóm + phím ← / → đổi tab.</summary>
        private sealed class Host : MonoBehaviour
        {
            private GameWindow _w;
            private RectTransform _strip;
            private int _index;

            public void Build(GameWindow w, int index)
            {
                _w = w;
                _index = index;
                if (_strip != null) return;
                // Hàng tab nằm NGAY TRÊN mép trên cửa sổ (con của cửa sổ → kéo cửa sổ thì tab đi theo)
                _strip = UIKit.Rect("Tabs", w.transform);
                _strip.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, 2), new Vector2(Tabs.Length * (TabW + Gap) + 170, TabH), new Vector2(0, 0));
                for (int i = 0; i < Tabs.Length; i++)
                {
                    int k = i;
                    var b = UIKit.Button("Tab" + i, _strip, Tabs[i].label, () => Open(k), 14);
                    ((RectTransform)b.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * (TabW + Gap), 0), new Vector2(TabW, TabH), Vector2.zero);
                    UIStyle.Chip(b, 14);
                    var on = i == index;
                    b.image.color = on ? UIStyle.Gold : UIStyle.ChipBg;
                    var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (on && label != null) { label.color = new Color(0.25f, 0.13f, 0.08f); label.fontSharedMaterial = label.font.material; label.fontStyle = FontStyles.Bold; }
                }
                var hint = UIKit.Text("Hint", _strip, "Trái / Phải: đổi tab", 13, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.85f));
                hint.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(Tabs.Length * (TabW + Gap) + 4, 0), new Vector2(166, TabH), Vector2.zero);
                hint.raycastTarget = false;
                UIStyle.Readable(hint);
            }

            private void LateUpdate()
            {
                if (_strip == null) return;
                bool show = !_w.HasPartner;   // túi mở cặp với Shop / Rương → không phải tab
                if (_strip.gameObject.activeSelf != show) _strip.gameObject.SetActive(show);
            }

            private void Update()
            {
                if (_w == null || _w.HasPartner || !_w.IsTopMost || Core.GameInput.Blocked || Keyboard.current == null) return;
                if (_stepFrame == Time.frameCount) return;
                int dir = Keyboard.current.leftArrowKey.wasPressedThisFrame ? -1 : Keyboard.current.rightArrowKey.wasPressedThisFrame ? 1 : 0;
                if (dir == 0) return;
                _stepFrame = Time.frameCount;
                Open(_index + dir);
            }
        }
    }
}
