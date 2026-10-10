using System;
using Assets.Script.UI;

namespace Assets.Script.Player
{
    /// <summary>
    /// TRẠNG THÁI TỨC THỜI của nhân vật chính chủ: id, tên, khu, máu / mana đang có, chết, choáng.
    /// Bảng nhân vật (cấp, EXP, tiền, máu / mana tối đa, chỉ số) nằm ở GameData.Me — các thuộc tính Level / Exp / Yen /
    /// MaxHp / MaxMp dưới đây chỉ ĐỌC từ đó (trước đây giữ 2 bản, mỗi gói phải ghi cả hai, dễ lệch nhau).
    /// Gói cập nhật máu (PLAYER_HEAL, MOB_ATTACK, REVIVE...) gọi SetHp / SetHpMp → HUD vẽ lại.
    /// </summary>
    public static class LocalPlayerState
    {
        public static int Id = -1;
        public static string Name;
        public static int Hp, Mp;
        /// <summary>Khu đang đứng (255 = khu riêng: phó bản / lôi đài). Cập nhật khi đăng nhập / đổi map / đổi khu.</summary>
        public static int ZoneId;
        public static bool IsDead;

        private static Data.CharacterInfo Me => Data.GameData.Me;
        public static int MaxHp => Me.maxHp;
        public static int MaxMp => Me.maxMp;
        public static int Level => Me.level;
        public static long Exp => Me.exp;
        public static int Yen => Me.yen;
        public static int BonusDamage => Me.bonusDamage;

        /// <summary>Đang bị CHOÁNG tới thời điểm này (Time.time) — server gửi EFFECT; không di chuyển/đánh được.</summary>
        public static float StunnedUntil;
        public static bool IsStunned => UnityEngine.Time.time < StunnedUntil;
        /// <summary>Đang bị LÀM CHẬM tới thời điểm này, chậm SlowPercent % — server cũng giới hạn tốc độ theo đúng số này.</summary>
        public static float SlowedUntil;
        public static int SlowPercent;
        /// <summary>Hệ số tốc độ chạy (1 = bình thường) — khớp server StatusEffects.speedFactor.</summary>
        public static float SpeedFactor => UnityEngine.Time.time < SlowedUntil ? UnityEngine.Mathf.Max(0.2f, 1f - SlowPercent / 100f) : 1f;
        public static float BuffedUntil;   // GĐ8: đang được tăng sức mạnh (Cổ Vũ / Thiết Thể / Ảnh Bộ)
        public static bool IsBuffed => UnityEngine.Time.time < BuffedUntil;

        /// <summary>Bắn ra mỗi khi có chỉ số thay đổi (UI khác muốn nghe thì đăng ký).</summary>
        public static event Action OnChanged;

        /// <summary>Vào game (gói LOGIN / CREATE_CHARACTER). Gọi SAU GameData.ClearSession.</summary>
        public static void Init(int id, string name, int level, long exp, int yen, int xu, int luong, int hp, int maxHp, int mp, int maxMp)
        {
            Id = id; Name = name;
            Me.level = level; Me.exp = exp; Me.yen = yen; Me.xu = xu; Me.luong = luong;
            Hp = hp; Mp = mp; Me.maxHp = maxHp; Me.maxMp = maxMp;
            IsDead = hp <= 0;
            RefreshHud();
        }

        public static void SetHpMp(int hp, int maxHp, int mp, int maxMp)
        {
            Hp = hp; Mp = mp; Me.maxHp = maxHp; Me.maxMp = maxMp;
            RefreshHud();
        }

        public static void SetHp(int hp)
        {
            Hp = hp;
            RefreshHud();
        }

        public static void Reset()
        {
            Id = -1; IsDead = false; StunnedUntil = 0; SlowedUntil = 0; BuffedUntil = 0;
        }

        public static void RefreshHud()
        {
            UISetup.Instance?.SetupHealth(Hp, MaxHp, Mp, MaxMp);
            OnChanged?.Invoke();
        }
    }
}
