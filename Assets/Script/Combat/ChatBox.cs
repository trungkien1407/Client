using System.Collections.Generic;
using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.Combat
{
    /// <summary>
    /// Khung chat tối giản (vẽ bằng IMGUI, không cần prefab/Canvas) — để có chat chạy được ngay.
    /// Sau này làm UI đẹp bằng uGUI thì chỉ cần giữ phần gửi/nhận gói CHAT.
    ///
    ///   Enter        : mở ô nhập / gửi tin
    ///   Esc          : đóng ô nhập
    ///   "/w Tên nội dung" : chat riêng (kênh 2) | "/a nội dung" : kênh thế giới (0) | mặc định: trong khu (1)
    /// </summary>
    public class ChatBox : NetworkListener
    {
        /// <summary>Đang gõ chat -> PlayerMovement/CombatNetwork phải bỏ qua phím di chuyển/đánh.</summary>
        public static bool IsTyping { get; private set; }

        private const int MaxLines = 8;
        private readonly List<string> _lines = new List<string>();
        private string _input = "";
        private bool _focusNextFrame;
        private int _openedFrame; // frame vừa mở ô nhập: bỏ qua phím Enter của chính frame đó (không gửi rỗng)
        private GUIStyle _lineStyle;

        protected override void RegisterHandlers() => Listen(Cmd.CHAT, OnChat);

        protected override void Update()
        {
            base.Update();

            if (LocalPlayerState.Id < 0) { IsTyping = false; return; }

            var kb = Keyboard.current;
            if (kb == null) return;
            if (!IsTyping && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                IsTyping = true;
                _focusNextFrame = true;
                _openedFrame = Time.frameCount;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            IsTyping = false;
        }

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
            string tag = channel == 0 ? "[TG]" : channel == 2 ? "[Riêng]" : "[Khu]";
            Push($"{tag} {from}: {msg}");
        }

        private static ChatBox _instance;
        private void Awake() => _instance = this;

        private void Push(string line)
        {
            foreach (var l in line.Split('\n'))
            {
                _lines.Add(l);
                if (_lines.Count > MaxLines) _lines.RemoveAt(0);
            }
        }

        /// <summary>Thêm 1 dòng thông báo hệ thống vào khung chat (gọi được từ mọi nơi).</summary>
        public static void AddSystem(string text)
        {
            if (_instance != null) _instance.Push("[Hệ thống] " + text);
            Debug.Log("[Hệ thống] " + text);
        }

        private void Send(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return;

            var w = new MessageWriter();
            if (text.StartsWith("/w "))
            {
                // "/w Tên nội dung"
                string rest = text.Substring(3).Trim();
                int space = rest.IndexOf(' ');
                if (space <= 0) { w.Cleanup(); return; }
                w.WriteByte((byte)2);
                w.WriteUTF(rest.Substring(0, space));
                w.WriteUTF(rest.Substring(space + 1));
            }
            else if (text.StartsWith("/a "))
            {
                w.WriteByte((byte)0);
                w.WriteUTF(text.Substring(3));
            }
            else
            {
                w.WriteByte((byte)1);
                w.WriteUTF(text);
            }
            NetworkManager.Instance?.Send(Cmd.CHAT, w.ToArray());
            w.Cleanup();
        }

        private void OnGUI()
        {
            if (LocalPlayerState.Id < 0) return;
            if (_lineStyle == null)
            {
                _lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };
                _lineStyle.normal.textColor = Color.white;
            }

            float w = 420f, lineH = 20f;
            float top = Screen.height - 40f - lineH * _lines.Count;
            for (int i = 0; i < _lines.Count; i++)
            {
                var rect = new Rect(10, top + i * lineH, w, lineH);
                // bóng đen phía sau cho dễ đọc trên nền sáng
                var shadow = new GUIStyle(_lineStyle); shadow.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, w, lineH), _lines[i], shadow);
                GUI.Label(rect, _lines[i], _lineStyle);
            }

            if (!IsTyping) return;

            // Xử lý phím trong IMGUI (vì TextField "nuốt" phím khi đang focus)
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                && Time.frameCount != _openedFrame)
            {
                Send(_input);
                _input = "";
                IsTyping = false;
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _input = "";
                IsTyping = false;
                e.Use();
                return;
            }

            GUI.SetNextControlName("ChatInput");
            _input = GUI.TextField(new Rect(10, Screen.height - 34f, w, 24f), _input, 200);
            if (_focusNextFrame)
            {
                GUI.FocusControl("ChatInput");
                _focusNextFrame = false;
            }
        }
    }
}
