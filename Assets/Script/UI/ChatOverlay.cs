using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// KHUNG CHAT NHỎ góc trái (prefab Resources/UI/Hud/ChatOverlay): vài dòng chat mới nhất + nút "Chat" mở cửa sổ chat.
    /// ChatBox đổ chữ vào {lines}.
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
        }

        private void LateUpdate()
        {
            // Không có dòng chat nào → ẩn nền (khỏi 1 khung trống giữa màn hình)
            var bg = transform.Find("Bg");
            if (bg != null) { bool on = lines != null && !string.IsNullOrEmpty(lines.text); if (bg.gameObject.activeSelf != on) bg.gameObject.SetActive(on); }
        }
    }
}
