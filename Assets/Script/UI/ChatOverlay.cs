using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// KHUNG CHAT NHỎ góc trái (prefab Resources/UI/Hud/ChatOverlay): vài dòng chat mới nhất + nút "Chat" mở cửa sổ chat.
    /// ChatBox đổ chữ vào {lines}. Mặc định THU GỌN (2 dòng); nút mũi tên góc trên phải mở rộng (vẫn nhỏ hơn bản cũ).
    /// </summary>
    public class ChatOverlay : HudPanel
    {
        public TextMeshProUGUI lines;
        [SerializeField] private Button _chatBtn;

        protected override void Build()
        {
            // Góc trái, PHÍA TRÊN joystick / nút mũi tên (dưới cùng bên trái)
            ((RectTransform)transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 240), new Vector2(470, 170), Vector2.zero);
            lines = UIKit.Text("Lines", transform, "", 15, TextAlignmentOptions.BottomLeft);
            lines.rectTransform.Fill(4, 4, 2, 2);
            lines.outlineWidth = 0.2f;
            lines.outlineColor = new Color32(0, 0, 0, 200);
            Assets.Script.UI.Kit.UIStyle.Readable(lines);   // viền + bóng đổ → đọc được cả trên nền trắng
            _chatBtn = UIKit.Button("ChatBtn", transform, "Chat", null, 15);
            ((RectTransform)_chatBtn.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, -40), new Vector2(80, 34), Vector2.zero);
        }

        // Thu gọn (mặc định) / mở rộng (2026-11-15): trước 470 × 170 che nhiều màn hình
        private static readonly Vector2 SmallSize = new Vector2(300, 44), BigSize = new Vector2(370, 128);
        private const string PrefKey = "chat_expanded";
        private bool _expanded;
        private Button _toggle;
        private RectTransform _toggleIcon;

        protected override void Bind()
        {
            _chatBtn.onClick.AddListener(() => GameWindow.Get<ChatWindow>().OpenAndFocus());
            // Cùng kiểu với khung nhiệm vụ / thông báo (2026-11-15): nền tối trong suốt bo góc phía sau các dòng chat, nút "Chat (X)" kiểu chip
            var bg = transform.Find("Bg") as RectTransform;
            if (bg == null)
            {
                bg = UIKit.Rect("Bg", transform).Fill();
                bg.SetAsFirstSibling();
                var img = bg.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                UIStyle.SetRound(img, UIStyle.PanelBg, 1.6f);
            }
            _chatBtn.SetLabel("Chat (X)");
            ((RectTransform)_chatBtn.transform).sizeDelta = new Vector2(96, 32);
            UIStyle.Chip(_chatBtn, 14);

            // Chữ nằm trong 1 khung CẮT (RectMask2D): dòng cũ tràn lên trên bị giấu khi khung thu nhỏ.
            // (Không cắt cả khung gốc — nút Chat nằm dưới mép khung sẽ bị cắt mất.)
            var clip = UIKit.Rect("Clip", transform).Fill(4, 4, 2, 2);
            clip.SetSiblingIndex(1);
            clip.gameObject.AddComponent<RectMask2D>();
            lines.transform.SetParent(clip, false);
            lines.rectTransform.Fill();
            lines.overflowMode = TextOverflowModes.Overflow;

            // Nút mũi tên góc trên phải: ▲ mở rộng / ▼ thu gọn
            _toggle = UIKit.Button("Expand", transform, "", Toggle, 12);
            var trt = (RectTransform)_toggle.transform;
            trt.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(34, 26), new Vector2(0, 1));
            UIStyle.Chip(_toggle, 12);
            _toggleIcon = UIIcons.Add(_toggle.transform, UIIcons.ChevronUp, 16, UIStyle.Gold).rectTransform;

            try { _expanded = PlayerPrefs.GetInt(PrefKey, 0) == 1; } catch { _expanded = false; }
            Apply();
        }

        /// <summary>Đổi thu gọn ↔ mở rộng, nhớ lựa chọn cho lần sau.</summary>
        public void Toggle()
        {
            _expanded = !_expanded;
            try { PlayerPrefs.SetInt(PrefKey, _expanded ? 1 : 0); } catch { }
            Apply();
        }

        public bool Expanded => _expanded;

        private void Apply()
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = _expanded ? BigSize : SmallSize;
            lines.fontSize = _expanded ? 14 : 13;
            if (_toggleIcon != null) _toggleIcon.localRotation = Quaternion.Euler(0, 0, _expanded ? 180f : 0f);
        }

        private void LateUpdate()
        {
            // Không có dòng chat nào → ẩn nền (khỏi 1 khung trống giữa màn hình); nút mũi tên vẫn hiện
            var bg = transform.Find("Bg");
            if (bg != null) { bool on = lines != null && !string.IsNullOrEmpty(lines.text); if (bg.gameObject.activeSelf != on) bg.gameObject.SetActive(on); }
        }
    }
}
