using System.Collections.Generic;
using System.Text;
using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.Combat
{
    /// <summary>
    /// CHAT — nhận/gửi gói CHAT + hiện 7 dòng mới nhất góc trái màn hình (uGUI, chạy cả PC lẫn điện thoại).
    ///   - PC: Enter mở cửa sổ chat (ChatWindow) và đặt con trỏ vào ô nhập; Enter gửi; Enter khi ô trống / Esc = đóng.
    ///   - Điện thoại: nút "Chat" góc trái → cửa sổ chat, bàn phím ảo tự hiện khi chạm ô nhập.
    ///   - Gõ nhanh: "/a nội dung" thế giới · "/g nội dung" gia tộc · "/w Tên nội dung" riêng · "/..." khác = lệnh GM.
    /// Kênh: 0 thế giới · 1 khu · 2 riêng · 3 hệ thống (server gửi) · 4 gia tộc.
    /// </summary>
    public class ChatBox : NetworkListener
    {
        /// <summary>Đang gõ vào 1 ô nhập UI (chat, thư, gia tộc...) → di chuyển / đánh / phím tắt phải bỏ qua phím.</summary>
        public static bool IsTyping => UIKit.IsTypingInUI();

        public struct Line { public int channel; public string text; }

        private const int MaxHistory = 100, OverlayLines = 7;
        private static readonly List<Line> History = new List<Line>();
        /// <summary>Có dòng chat mới (ChatWindow nghe để vẽ lại).</summary>
        public static event System.Action OnNewLine;
        public static IReadOnlyList<Line> Lines => History;

        private static ChatBox _instance;
        private GameObject _overlayRoot;
        private TextMeshProUGUI _overlay;

        private void Awake() => _instance = this;

        protected override void RegisterHandlers() => Listen(Cmd.CHAT, OnChat);

        protected override void Update()
        {
            base.Update();
            bool inGame = LocalPlayerState.Id >= 0;
            if (inGame && _overlayRoot == null) BuildOverlay();
            if (_overlayRoot != null && _overlayRoot.activeSelf != inGame) _overlayRoot.SetActive(inGame);
            if (!inGame) return;

            var kb = Keyboard.current;
            if (kb != null && !IsTyping && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                GameWindow.Get<ChatWindow>().OpenAndFocus();
        }

        private void BuildOverlay()
        {
            var hud = UIRoot.Instance.HudLayer;
            _overlayRoot = UIKit.Rect("ChatOverlay", hud).gameObject;
            var rt = (RectTransform)_overlayRoot.transform;
            // Góc trái, PHÍA TRÊN joystick / nút mũi tên (dưới cùng bên trái)
            rt.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 240), new Vector2(470, 170), Vector2.zero);
            _overlay = UIKit.Text("Lines", rt, "", 15, TextAlignmentOptions.BottomLeft);
            _overlay.rectTransform.Fill(4, 4, 2, 2);
            _overlay.outlineWidth = 0.2f;
            _overlay.outlineColor = new Color32(0, 0, 0, 200);
            var btn = UIKit.Button("ChatBtn", hud, "Chat", () => GameWindow.Get<ChatWindow>().OpenAndFocus(), 15);
            ((RectTransform)btn.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 200), new Vector2(80, 34), Vector2.zero);
            btn.transform.SetParent(rt, true); // ẩn/hiện cùng overlay
            RefreshOverlay();
        }

        private void RefreshOverlay()
        {
            if (_overlay == null) return;
            var sb = new StringBuilder();
            for (int i = Mathf.Max(0, History.Count - OverlayLines); i < History.Count; i++)
                sb.Append(Format(History[i])).Append('\n');
            _overlay.text = sb.ToString().TrimEnd('\n');
        }

        /// <summary>Tô màu theo kênh.</summary>
        public static string Format(Line l)
        {
            string color = l.channel switch { 0 => "#ffd84a", 2 => "#ff8ad8", 3 => "#ffb060", 4 => "#6cff6c", _ => "#ffffff" };
            return $"<color={color}>{l.text}</color>";
        }

        public static string ChannelTag(int channel) =>
            channel switch { 0 => "[TG]", 2 => "[Riêng]", 3 => "[Hệ thống]", 4 => "[Gia tộc]", _ => "[Khu]" };

        /// <summary>CHAT (S→C): byte channel, int fromId, UTF fromName, UTF message</summary>
        private void OnChat(byte[] data)
        {
            var r = new MessageReader(data);
            byte channel = r.ReadByte();
            r.ReadInt();
            string from = r.ReadUTF();
            string msg = r.ReadUTF();
            r.Cleanup();

            if (channel == 3) { AddSystem(msg); return; } // tin hệ thống (trả lời lệnh GM, thông báo lỗi...)
            Push(channel, $"{ChannelTag(channel)} {from}: {msg}");
        }

        private static void Push(int channel, string text)
        {
            foreach (var l in text.Split('\n'))
            {
                // Chặn thẻ rich text người chơi tự gõ (<color>, <size>...) để không phá giao diện
                History.Add(new Line { channel = channel, text = l.Replace("<", "<​") });
                if (History.Count > MaxHistory) History.RemoveAt(0);
            }
            if (_instance != null) _instance.RefreshOverlay();
            OnNewLine?.Invoke();
        }

        /// <summary>Thêm 1 dòng thông báo hệ thống vào khung chat (gọi được từ mọi nơi).</summary>
        public static void AddSystem(string text)
        {
            Push(3, "[Hệ thống] " + text);
            Debug.Log("[Hệ thống] " + text);
        }

        /// <summary>
        /// Gửi 1 tin. channel/target lấy từ cửa sổ chat; nếu tin bắt đầu bằng /a /g /w thì theo lệnh đó.
        /// Lệnh GM ("/item 1 5"...) gửi qua kênh khu — server nhận ra và không phát cho ai.
        /// </summary>
        public static void Send(int channel, string target, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.StartsWith("/a ")) { channel = 0; text = text.Substring(3); }
            else if (text.StartsWith("/g ")) { channel = 4; text = text.Substring(3); }
            else if (text.StartsWith("/w "))
            {
                string rest = text.Substring(3).Trim();
                int space = rest.IndexOf(' ');
                if (space <= 0) return;
                channel = 2; target = rest.Substring(0, space); text = rest.Substring(space + 1);
            }
            else if (text.StartsWith("/")) channel = 1;

            if (channel == 2 && string.IsNullOrWhiteSpace(target)) { AddSystem("Nhập tên người nhận để chat riêng."); return; }
            var w = new MessageWriter();
            w.WriteByte((byte)channel);
            if (channel == 2) w.WriteUTF(target.Trim());
            w.WriteUTF(text);
            NetworkManager.Instance?.Send(Cmd.CHAT, w.ToArray());
            w.Cleanup();
            // Chat riêng: server gửi lại bản sao "Bạn → Tên: ..." (hoặc báo người đó không online)
        }
    }
}
