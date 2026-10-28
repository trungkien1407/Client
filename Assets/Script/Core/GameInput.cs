using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Assets.Script.Core
{
    /// <summary>Hành động bấm phím của game (đổi phím: sửa bảng Bindings).</summary>
    public enum GameKey
    {
        Attack, Action, Revive, PotionHp, PotionMp, TalkNpc, OpenChat, CloseWindow,
        Skill1, Skill2, Skill3, Skill4, Skill5,
    }

    /// <summary>
    /// BẢNG PHÍM DUY NHẤT của game (trừ di chuyển / nhảy / chọn mục tiêu — PlayerControls.inputactions).
    /// Mọi chỗ hỏi "phím X vừa bấm?" gọi GameInput.Pressed(GameKey.X): đang gõ chữ (chat, ô nhập) thì luôn false.
    /// Phím tắt cửa sổ (I, C, K...) khai báo ở GameWindow.HotKey, hỏi bằng Pressed(Key).
    /// </summary>
    public static class GameInput
    {
        public static readonly Dictionary<GameKey, Key[]> Bindings = new Dictionary<GameKey, Key[]>
        {
            { GameKey.Attack, new[] { Key.J } },                                  // đánh (cả người khi bật Đồ sát)
            { GameKey.Action, new[] { Key.Enter, Key.NumpadEnter } },             // "làm việc với mục tiêu": quái → đánh, NPC → nói chuyện, người → tương tác; xa thì tự chạy lại (ActionInput)
            { GameKey.Revive, new[] { Key.R } },
            { GameKey.PotionHp, new[] { Key.Q } },
            { GameKey.PotionMp, new[] { Key.E } },
            { GameKey.TalkNpc, new[] { Key.F } },
            { GameKey.OpenChat, new[] { Key.T } },                                // Enter đã dành cho Action
            { GameKey.CloseWindow, new[] { Key.Escape } },
            { GameKey.Skill1, new[] { Key.Digit1 } },
            { GameKey.Skill2, new[] { Key.Digit2 } },
            { GameKey.Skill3, new[] { Key.Digit3 } },
            { GameKey.Skill4, new[] { Key.Digit4 } },
            { GameKey.Skill5, new[] { Key.Digit5 } },
        };

        private static int _lastTypingFrame = -10;

        /// <summary>
        /// Không có bàn phím, hoặc đang gõ chữ → bỏ qua mọi phím tắt. Tính cả 1 khung hình SAU khi thôi gõ: Enter gửi chat / đóng
        /// ô chat làm ô nhập mất focus ngay trong khung đó — không có khoảng đệm thì cùng phím Enter lại thành "đánh".
        /// </summary>
        public static bool Blocked
        {
            get
            {
                if (Keyboard.current == null) return true;
                if (Combat.ChatBox.IsTyping || UI.Kit.UIKit.IsTypingInUI()) { _lastTypingFrame = UnityEngine.Time.frameCount; return true; }
                return UnityEngine.Time.frameCount - _lastTypingFrame <= 1;
            }
        }

        /// <summary>Phím vẫn dùng được khi đang mở cửa sổ (Esc đóng cửa sổ, mở chat, hồi sinh). Còn lại là phím đánh / chiêu / bình.</summary>
        private static bool WorksWithWindowOpen(GameKey k) => k == GameKey.CloseWindow || k == GameKey.OpenChat || k == GameKey.Revive;

        /// <summary>Phím hành động k có bị chặn vì đang mở cửa sổ (túi, shop...) không.</summary>
        private static bool WindowBlocks(GameKey k) => !WorksWithWindowOpen(k) && UI.Kit.UIRoot.GameplayBlocked;

        public static bool Pressed(GameKey k)
        {
            if (Blocked || WindowBlocks(k) || !Bindings.TryGetValue(k, out var keys)) return false;
            foreach (var key in keys) if (Keyboard.current[key].wasPressedThisFrame) return true;
            return false;
        }

        public static bool Pressed(Key key) => key != Key.None && !Blocked && Keyboard.current[key].wasPressedThisFrame;

        /// <summary>Phím của hành động k đang được GIỮ (giữ Enter = đánh liên tục).</summary>
        public static bool Held(GameKey k)
        {
            if (Blocked || WindowBlocks(k) || !Bindings.TryGetValue(k, out var keys)) return false;
            foreach (var key in keys) if (Keyboard.current[key].isPressed) return true;
            return false;
        }

        /// <summary>Phím chiêu thứ i (0..4) vừa bấm?</summary>
        public static bool SkillSlot(int i) => Pressed(GameKey.Skill1 + i);
    }
}
