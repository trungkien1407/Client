using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// TUỲ CHỌN TỰ ĐỘNG (lưu PlayerPrefs trên máy, mọi nhân vật dùng chung) — AutoWindow chỉnh, AutoPlay đọc.
    /// </summary>
    public static class AutoSettings
    {
        private const string P = "auto_";

        /// <summary>Bật: đi khắp map tìm quái. Tắt: chỉ đánh quái trong AutoPlay.LocalRadius quanh chỗ bật auto (đứng 1 chỗ luyện).</summary>
        public static bool FindMobs { get => Get("find", true); set => Set("find", value); }
        /// <summary>Chiêu hỗ trợ / buff {skillId} được tự dùng khi hồi xong (người chơi chọn từng chiêu trong AutoWindow; mặc định tắt).
        /// Chiêu đánh thì auto LUÔN dùng chiêu đang chọn trên thanh phím.</summary>
        public static bool Buff(int skillId) => Get("buff_" + skillId, false);
        public static void SetBuff(int skillId, bool on) => Set("buff_" + skillId, on);
        public static bool PotionHp { get => Get("hp", true); set => Set("hp", value); }
        public static bool PotionMp { get => Get("mp", true); set => Set("mp", value); }
        /// <summary>Uống bình khi máu / chakra dưới bao nhiêu % (10..90).</summary>
        public static int HpPercent { get => PlayerPrefs.GetInt(P + "hp_pct", 50); set => PlayerPrefs.SetInt(P + "hp_pct", Mathf.Clamp(value, 10, 90)); }
        public static int MpPercent { get => PlayerPrefs.GetInt(P + "mp_pct", 30); set => PlayerPrefs.SetInt(P + "mp_pct", Mathf.Clamp(value, 10, 90)); }
        public static bool PickItems { get => Get("pick", true); set => Set("pick", value); }

        private static bool Get(string k, bool def) => PlayerPrefs.GetInt(P + k, def ? 1 : 0) != 0;
        private static void Set(string k, bool v) => PlayerPrefs.SetInt(P + k, v ? 1 : 0);
    }
}
