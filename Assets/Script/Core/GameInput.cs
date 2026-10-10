using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Assets.Script.Core
{
    /// <summary>Hành động bấm phím của game (đổi phím: sửa bảng Bindings).</summary>
    public enum GameKey
    {
        Attack, Revive, PotionHp, PotionMp, TalkNpc, OpenChat, CloseWindow,
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
            { GameKey.Attack, new[] { Key.J } },
            { GameKey.Revive, new[] { Key.R } },
            { GameKey.PotionHp, new[] { Key.Q } },
            { GameKey.PotionMp, new[] { Key.E } },
            { GameKey.TalkNpc, new[] { Key.F } },
            { GameKey.OpenChat, new[] { Key.Enter, Key.NumpadEnter } },
            { GameKey.CloseWindow, new[] { Key.Escape } },
            { GameKey.Skill1, new[] { Key.Digit1 } },
            { GameKey.Skill2, new[] { Key.Digit2 } },
            { GameKey.Skill3, new[] { Key.Digit3 } },
            { GameKey.Skill4, new[] { Key.Digit4 } },
            { GameKey.Skill5, new[] { Key.Digit5 } },
        };

        /// <summary>Không có bàn phím, hoặc đang gõ chữ → bỏ qua mọi phím tắt.</summary>
        public static bool Blocked => Keyboard.current == null || Combat.ChatBox.IsTyping || UI.Kit.UIKit.IsTypingInUI();

        public static bool Pressed(GameKey k)
        {
            if (Blocked || !Bindings.TryGetValue(k, out var keys)) return false;
            foreach (var key in keys) if (Keyboard.current[key].wasPressedThisFrame) return true;
            return false;
        }

        public static bool Pressed(Key key) => key != Key.None && !Blocked && Keyboard.current[key].wasPressedThisFrame;

        /// <summary>Phím chiêu thứ i (0..4) vừa bấm?</summary>
        public static bool SkillSlot(int i) => Pressed(GameKey.Skill1 + i);
    }
}
