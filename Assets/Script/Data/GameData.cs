using System;
using System.Collections.Generic;

namespace Assets.Script.Data
{
    // =====================================================================
    // MẪU DỮ LIỆU TĨNH (server gửi lúc vào game qua GAME_DATA_*). Client KHÔNG tự giữ bảng tên/giá.
    // =====================================================================
    public class ItemTpl
    {
        public int id, type, slot, iconId, levelRequire, classRequire, price, maxStack;
        public int bonusHp, bonusMp, bonusDamage, hpRestore, mpRestore;
        public bool tradeable;
        public string name, description;
        public bool IsEquip => slot > 0;
        public bool IsUsable => type >= 0 && type <= 3;
    }

    public class SkillLevel { public int point, manaUse, coolDown, damage; public float range, aoe; public string info; }

    public class SkillTpl
    {
        public int id, classId, maxLevel, type, iconId;
        public string name, description;
        public List<SkillLevel> levels = new List<SkillLevel>();
        public SkillLevel Level(int point) => levels.Find(l => l.point == point);
    }

    public class QuestTpl
    {
        public int id, type, targetId, targetCount, npcId, levelRequire, prevQuestId, rewardYen, rewardItemId, rewardItemQty;
        public long rewardExp;
        public string name, description;
    }

    public class MobTpl { public int id, level, rank; public bool aggressive; public string name; }

    // =====================================================================
    // THÔNG TIN NHÂN VẬT (CHARACTER_INFO)
    // =====================================================================
    public class CharacterInfo
    {
        public int level, potential, skillPoints, sucManh, thanPhap, chakra, theLuc;
        public long exp, expToNext;
        public int maxHp, maxMp, bonusDamage, yen, xu, luong, pkPoint;
        public float dodge, crit, moveSpeed;
    }

    /// <summary>
    /// KHO DỮ LIỆU CLIENT — 1 chỗ duy nhất giữ những gì server gửi về; UI chỉ đọc ở đây
    /// và nghe sự kiện OnChanged để vẽ lại. Nhận gói ở GameDataNetwork.
    /// </summary>
    public static class GameData
    {
        public static readonly Dictionary<int, ItemTpl> Items = new Dictionary<int, ItemTpl>();
        public static readonly Dictionary<int, SkillTpl> Skills = new Dictionary<int, SkillTpl>();
        public static readonly Dictionary<int, QuestTpl> Quests = new Dictionary<int, QuestTpl>();
        public static readonly Dictionary<int, MobTpl> Mobs = new Dictionary<int, MobTpl>();
        public static readonly Dictionary<int, string> NpcNames = new Dictionary<int, string>();

        // ---- trạng thái của mình ----
        public static readonly CharacterInfo Me = new CharacterInfo();
        public static readonly List<KeyValuePair<int, int>> Inventory = new List<KeyValuePair<int, int>>(); // (templateId, qty) theo đúng thứ tự ô
        public static readonly Dictionary<int, int> Equipment = new Dictionary<int, int>();  // slot → templateId
        public static readonly Dictionary<int, int> MySkills = new Dictionary<int, int>();   // skillId → cấp
        public static readonly Dictionary<int, int[]> MyQuests = new Dictionary<int, int[]>(); // questId → [progress, done]
        public static int ClassType;

        /// <summary>Bắn ra khi có thay đổi. Tham số = loại dữ liệu vừa đổi.</summary>
        public static event Action<DataKind> OnChanged;
        public static void Notify(DataKind kind) => OnChanged?.Invoke(kind);

        public static string ItemName(int id) => Items.TryGetValue(id, out var t) ? t.name : $"Vật phẩm {id}";
        public static string MobName(int id) => Mobs.TryGetValue(id, out var t) ? t.name : $"Quái {id}";
        public static string NpcName(int id) => NpcNames.TryGetValue(id, out var n) ? n : $"NPC {id}";

        public static int CountItem(int templateId)
        {
            int n = 0;
            foreach (var kv in Inventory) if (kv.Key == templateId) n += kv.Value;
            return n;
        }

        /// <summary>Tên ô trang bị theo số slot (khớp quy ước item_template.slot bên server).</summary>
        public static readonly string[] SlotNames = { "", "Vũ khí", "Áo", "Ngọc bội", "Quần", "Găng", "Giày", "Nhẫn", "Dây chuyền", "Phù" };
        public const int EquipSlotCount = 9;

        public static void ClearSession()
        {
            Inventory.Clear(); Equipment.Clear(); MySkills.Clear(); MyQuests.Clear();
        }
    }

    public enum DataKind { Templates, Character, Inventory, Equipment, Skills, Quests }
}
