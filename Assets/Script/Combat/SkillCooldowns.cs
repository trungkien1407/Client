using System.Collections.Generic;
using Assets.Script.Data;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// HỒI CHIÊU phía client: TỰ ĐẾM theo coolDown của cấp chiêu đang có (GAME_DATA_SKILLS) — không chờ server trả lời.
    /// Server chỉ xác thực: gửi sớm thì bỏ đòn, không trả lời gì (chiêu hồi 0,5 giây mà lần nào cũng trả lời thì tốn gói tin).
    /// → client đếm đúng thì không bao giờ bị nuốt đòn. Lúc vào game server gửi SKILL_COOLDOWN 1 lần (chiêu còn đang hồi
    /// sau khi thoát ra vào lại) → Set.
    /// Đồng hồ dùng Time.unscaledTime (không bị ảnh hưởng khi chỉnh timeScale).
    /// </summary>
    public static class SkillCooldowns
    {
        private struct Entry { public float readyAt, total; }
        private static readonly Dictionary<int, Entry> _cd = new Dictionary<int, Entry>();

        private static float Now => Time.unscaledTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook() => GameData.OnSessionReset += Clear;   // đăng xuất → xoá

        /// <summary>Thời gian hồi (giây) ở cấp chiêu đang có; 0 nếu chưa có dữ liệu.</summary>
        public static float CooldownOf(int skillId)
        {
            var lv = SkillCaster.LevelOf(skillId);
            return lv != null ? lv.coolDown / 1000f : 0f;
        }

        /// <summary>Vừa gửi USE_SKILL → bắt đầu đếm.</summary>
        public static void Start(int skillId)
        {
            float t = CooldownOf(skillId);
            if (t > 0) _cd[skillId] = new Entry { readyAt = Now + t, total = t };
        }

        /// <summary>Server báo còn {remainingMs} (SKILL_COOLDOWN lúc vào game).</summary>
        public static void Set(int skillId, int remainingMs)
        {
            if (remainingMs <= 0) { _cd.Remove(skillId); return; }
            float left = remainingMs / 1000f;
            _cd[skillId] = new Entry { readyAt = Now + left, total = Mathf.Max(left, CooldownOf(skillId)) };
        }

        /// <summary>Còn bao nhiêu giây (0 = dùng được).</summary>
        public static float Remaining(int skillId) => _cd.TryGetValue(skillId, out var e) ? Mathf.Max(0f, e.readyAt - Now) : 0f;

        public static bool Ready(int skillId) => Remaining(skillId) <= 0f;

        /// <summary>Phần còn lại 0..1 (vẽ lớp tối quay trên ô kỹ năng).</summary>
        public static float Fraction(int skillId)
        {
            if (!_cd.TryGetValue(skillId, out var e) || e.total <= 0) return 0f;
            return Mathf.Clamp01((e.readyAt - Now) / e.total);
        }

        public static void Clear() => _cd.Clear();
    }
}
