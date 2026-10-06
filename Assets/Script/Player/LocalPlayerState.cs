using System;
using Assets.Script.UI;

namespace Assets.Script.Player
{
    /// <summary>
    /// Chỉ số của NHÂN VẬT CHÍNH CHỦ (HP/MP/Level/EXP/Yen) — 1 nơi duy nhất giữ số liệu server gửi về.
    /// Mọi gói cập nhật (LOGIN, PLAYER_HEAL, PLAYER_EXP_UPDATE, PLAYER_STATS, MOB_ATTACK, REVIVE...)
    /// đều ghi vào đây rồi gọi RefreshHud() để thanh máu/mana trên HUD luôn khớp server.
    /// </summary>
    public static class LocalPlayerState
    {
        public static int Id = -1;
        public static string Name;
        public static int Hp, MaxHp, Mp, MaxMp;
        public static int Level;
        public static long Exp;
        public static int Yen;
        public static int BonusDamage;
        public static bool IsDead;

        /// <summary>Bắn ra mỗi khi có chỉ số thay đổi (UI khác muốn nghe thì đăng ký).</summary>
        public static event Action OnChanged;

        public static void Init(int id, string name, int level, long exp, int yen, int hp, int maxHp, int mp, int maxMp)
        {
            Id = id; Name = name; Level = level; Exp = exp; Yen = yen;
            Hp = hp; MaxHp = maxHp; Mp = mp; MaxMp = maxMp;
            IsDead = hp <= 0;
            RefreshHud();
        }

        public static void SetHpMp(int hp, int maxHp, int mp, int maxMp)
        {
            Hp = hp; MaxHp = maxHp; Mp = mp; MaxMp = maxMp;
            RefreshHud();
        }

        public static void SetHp(int hp)
        {
            Hp = hp;
            RefreshHud();
        }

        public static void Reset()
        {
            Id = -1; IsDead = false;
        }

        public static void RefreshHud()
        {
            UISetup.Instance?.SetupHealth(Hp, MaxHp, Mp, MaxMp);
            OnChanged?.Invoke();
        }
    }
}
