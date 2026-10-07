using Assets.Script.Combat;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CỬA SỔ CHAT: lịch sử 100 dòng + chọn kênh (Khu / Thế giới / Gia tộc / Riêng) + ô nhập + nút Gửi.
    /// Mở: phím Enter (PC) hoặc nút "Chat" góc trái (điện thoại). Enter gửi, Enter khi ô trống hoặc Esc = đóng.
    /// Phần mạng (gửi/nhận gói CHAT) nằm ở Combat/ChatBox.
    /// </summary>
    public class ChatWindow : GameWindow
    {
        private static readonly int[] TabChannels = { 1, 0, 4, 2 };
        private static readonly string[] TabLabels = { "Khu", "Thế giới", "Gia tộc", "Riêng" };

        private readonly Button[] _tabs = new Button[4];
        private RectTransform _list;
        private ScrollRect _scroll;
        private TMP_InputField _input, _target;
        private int _channel = 1;

        protected override void Build()
        {
            Title = "Trò chuyện";
            Size = new Vector2(600, 470);
            var body = CreateBody();

            for (int i = 0; i < 4; i++)
            {
                int ch = TabChannels[i];
                _tabs[i] = UIKit.Button("Tab" + i, body, TabLabels[i], () => { _channel = ch; RefreshTabs(); }, 15);
                ((RectTransform)_tabs[i].transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 112, 0), new Vector2(106, 32), new Vector2(0, 1));
            }

            _list = UIKit.ScrollList("History", body, 2);
            ((RectTransform)_list.parent).Fill(0, 0, 38, 50);
            _scroll = _list.parent.GetComponent<ScrollRect>();

            _target = UIKit.Input("Target", body, "Tên người nhận", 16, TMP_InputField.ContentType.Standard, 16);
            ((RectTransform)_target.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(130, 42), Vector2.zero);
            _input = UIKit.Input("Input", body, "Nhập tin nhắn... (/a thế giới · /g gia tộc · /w Tên riêng)", 16, TMP_InputField.ContentType.Standard, 150);
            _input.onSubmit.AddListener(OnSubmit);
            var send = UIKit.Button("Send", body, "Gửi", () => OnSubmit(_input.text), 16);
            ((RectTransform)send.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(90, 42), new Vector2(1, 0));
            send.image.color = UIKit.ButtonHot;

            ChatBox.OnNewLine += () => RefreshIfOpen();
            RefreshTabs();
        }

        /// <summary>Mở cửa sổ và đặt con trỏ vào ô nhập (bàn phím ảo tự hiện trên điện thoại).</summary>
        public void OpenAndFocus()
        {
            if (!IsOpen) Show();
            _input.Select();
            _input.ActivateInputField();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < 4; i++) _tabs[i].image.color = TabChannels[i] == _channel ? UIKit.ButtonHot : UIKit.ButtonColor;
            bool whisper = _channel == 2;
            _target.gameObject.SetActive(whisper);
            ((RectTransform)_input.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(whisper ? 136 : 0, 0),
                new Vector2(whisper ? -232 : -96, 42), Vector2.zero);
        }

        private void OnSubmit(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) { Hide(); return; } // Enter khi ô trống = đóng
            ChatBox.Send(_channel, _target.text, text);
            _input.text = "";
            _input.ActivateInputField(); // gõ tiếp được ngay
        }

        protected override void Refresh()
        {
            UIKit.Clear(_list);
            var lines = ChatBox.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                var t = UIKit.Text("L", _list, ChatBox.Format(lines[i]), 15, TextAlignmentOptions.TopLeft);
                t.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0; // cuộn xuống dòng mới nhất
        }
    }
}
