namespace Assets.Script.Constants
{
    // =====================================================================
    // MÃ SỐ TRONG GÓI TIN (khớp server) — dùng tên thay cho số ma. Ép kiểu khi ghi / đọc gói: (byte)AttackTarget.Mob.
    // =====================================================================

    /// <summary>USE_SKILL / BROADCAST_ATTACK / EFFECT: loại mục tiêu.</summary>
    public enum AttackTarget : byte { Mob = 0, Player = 1, Self = 2 }

    /// <summary>PLAYER_MOVE: trạng thái di chuyển.</summary>
    public enum MoveState : byte { Idle = 0, Run = 1, JumpUp = 2, Fall = 3 }

    /// <summary>skill_template.type.</summary>
    public enum SkillType { Active = 1, Passive = 2, Support = 3 }

    /// <summary>EFFECT: hiệu ứng trên người / quái.</summary>
    public enum StatusEffect : byte { None = 0, Stun = 1, Slow = 2, Burn = 3, Buff = 4 }

    /// <summary>quest_template.type.</summary>
    public enum QuestType { Kill = 0, Talk = 1, Collect = 2 }

    /// <summary>Loại tiền (npc_shop.currency, giá chợ...).</summary>
    public enum Currency { Yen = 0, Xu = 1, Luong = 2 }

    public static class ZoneIds
    {
        /// <summary>Khu riêng (phó bản / lôi đài) — server gửi 255 thay cho số khu.</summary>
        public const int Private = 255;
    }

    public static class ItemIds
    {
        /// <summary>Bình dùng bằng phím Q / E (item_template.id).</summary>
        public const int HpPotion = 1, MpPotion = 2;
    }
}
