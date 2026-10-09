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
            _chatBtn = UIKit.Button("ChatBtn", transform, "Chat", null, 15);
            ((RectTransform)_chatBtn.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, -40), new Vector2(80, 34), Vector2.zero);
        }

        protected override void Bind() => _chatBtn.onClick.AddListener(() => GameWindow.Get<ChatWindow>().OpenAndFocus());
    }
}
