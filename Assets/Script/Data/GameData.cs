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

    public class SkillLevel
    {
        public int point, manaUse, coolDown, damage; public float range, aoe; public string info;
        // Hiệu ứng (GĐ4): "stun" / "slow" / "burn" hoặc rỗng
        public string effect; public float effectChance; public int effectMs, effectValue;
    }

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

    /// <summary>1 Ô ĐỒ (túi / rương / giao dịch / thư): mẫu, số lượng, cấp cường hoá +N, khoá (không giao dịch được).</summary>
    public class BagSlot
    {
        public int tpl, qty, level;
        public bool locked;
        public string Name => GameData.ItemName(tpl) + (level > 0 ? $" +{level}" : "");

        /// <summary>Đọc 1 danh sách ô đồ: short n, [int tpl, int qty, byte level, byte locked] x n.</summary>
        public static List<BagSlot> ReadList(Network.MessageReader r)
        {
            int n = r.ReadShort();
            var list = new List<BagSlot>(n);
            for (int i = 0; i < n; i++)
                list.Add(new BagSlot { tpl = r.ReadInt(), qty = r.ReadInt(), level = r.ReadByte(), locked = r.ReadByte() != 0 });
            return list;
        }
    }

    public class PartyMember { public int id, hp, maxHp, level; public string name; }
    public class FriendInfo { public int id, level; public string name; public bool online; }
    public class MailHeader { public long id, createdMs; public string from, title; public bool read, claimed, hasAttach; }
    public class MailBody { public long id; public string from, title, content; public int yen; public List<BagSlot> items = new List<BagSlot>(); public bool claimed; }
    public class GuildMember { public int id, level, rank; public string name; public bool online; }

    public class GuildInfo
    {
        public int id, level, leaderId, myRank, capacity;
        public long fund;
        public string name, notice;
        public readonly List<GuildMember> members = new List<GuildMember>();
        public bool Has => id > 0;
        public static string RankName(int r) => r == 2 ? "Tộc trưởng" : r == 1 ? "Trưởng lão" : "Thành viên";
    }

    public class TopRow { public int rank; public string name, extra; public long value; }

    /// <summary>Bảng nâng cấp (UPGRADE_OPEN): mỗi cấp đích 1..maxLv có tỉ lệ, đá cần, yên.</summary>
    public class UpgradeTable
    {
        public int npcId, maxLv, protectItemId;
        public int[] rate, stoneId, stoneQty, yen;   // chỉ số = cấp ĐÍCH (1..maxLv)
        public int[] statPercent;                    // chỉ số = cấp hiện tại (0..maxLv) → % chỉ số cộng thêm
    }

    /// <summary>Trạng thái 1 bên trong giao dịch.</summary>
    public class TradeSide { public bool locked, confirmed; public int yen; public List<BagSlot> items = new List<BagSlot>(); }

    /// <summary>Thông tin PvP của 1 người (PLAYER_PVP_INFO): chế độ, điểm PK, gia tộc → màu tên.</summary>
    public class PvpInfo { public int pkMode, pkPoint; public string guild = ""; }

    /// <summary>
    /// KHO DỮ LIỆU CLIENT — 1 chỗ duy nhất giữ những gì server gửi về; UI chỉ đọc ở đây
    /// và nghe sự kiện OnChanged để vẽ lại. Nhận gói ở GameDataNetwork + SocialNetwork.
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
        /// <summary>Túi đồ ĐÚNG THỨ TỰ Ô (chỉ số = bagIndex gửi lên server khi mặc/bán/nâng cấp...).</summary>
        public static readonly List<BagSlot> Inventory = new List<BagSlot>();
        public static int BagCapacity = 40;
        public static readonly Dictionary<int, int> Equipment = new Dictionary<int, int>();   // slot → templateId
        public static readonly Dictionary<int, int> EquipLevel = new Dictionary<int, int>();  // slot → cấp +N
        public static readonly Dictionary<int, int> MySkills = new Dictionary<int, int>();    // skillId → cấp
        public static readonly Dictionary<int, int[]> MyQuests = new Dictionary<int, int[]>(); // questId → [progress, done]
        public static int ClassType;

        // ---- GĐ2 ----
        public static readonly List<BagSlot> Storage = new List<BagSlot>();
        public static int StorageCapacity = 30, StorageNpc = -1;
        public static UpgradeTable Upgrade;
        public static int TradeWith; public static string TradeWithName = "";
        public static readonly TradeSide TradeMine = new TradeSide(), TradeTheirs = new TradeSide();

        // ---- GĐ3 ----
        public static int MyPkMode;
        public static readonly Dictionary<int, PvpInfo> Pvp = new Dictionary<int, PvpInfo>();
        public static readonly List<PartyMember> Party = new List<PartyMember>();
        public static int PartyLeader;
        public static readonly List<FriendInfo> Friends = new List<FriendInfo>();
        public static readonly List<MailHeader> Mails = new List<MailHeader>();
        public static MailBody OpenMail;
        public static int MailUnread;
        public static readonly GuildInfo Guild = new GuildInfo();
        public static readonly Dictionary<int, List<TopRow>> Tops = new Dictionary<int, List<TopRow>>();

        // ---- GĐ4 ----
        public static int DungeonState, DungeonMobsLeft; public static float DungeonEndTime; public static string DungeonName = "";
        public static int EventType, EventState; public static float EventEndTime; public static string EventText = "";
        public static readonly List<KeyValuePair<string, int>> ArenaScore = new List<KeyValuePair<string, int>>();

        /// <summary>Bắn ra khi có thay đổi. Tham số = loại dữ liệu vừa đổi.</summary>
        public static event Action<DataKind> OnChanged;
        public static void Notify(DataKind kind) => OnChanged?.Invoke(kind);

        public static string ItemName(int id) => Items.TryGetValue(id, out var t) ? t.name : $"Vật phẩm {id}";
        public static string MobName(int id) => Mobs.TryGetValue(id, out var t) ? t.name : $"Quái {id}";
        public static string NpcName(int id) => NpcNames.TryGetValue(id, out var n) ? n : $"NPC {id}";

        public static int CountItem(int templateId)
        {
            int n = 0;
            foreach (var s in Inventory) if (s.tpl == templateId) n += s.qty;
            return n;
        }

        /// <summary>Ô túi đầu tiên chứa món này (-1 nếu không có).</summary>
        public static int FirstIndexOf(int templateId)
        {
            for (int i = 0; i < Inventory.Count; i++) if (Inventory[i].tpl == templateId) return i;
            return -1;
        }

        public static BagSlot BagAt(int index) => index >= 0 && index < Inventory.Count ? Inventory[index] : null;

        /// <summary>Tên đơn vị tiền theo mã currency (0 yên/1 xu/2 lượng).</summary>
        public static string CurrencyName(int c) => c == 1 ? "xu" : c == 2 ? "lượng" : "yên";
        public static int Money(int c) => c == 1 ? Me.xu : c == 2 ? Me.luong : Me.yen;

        /// <summary>Màu chữ cấp cường hoá (+N) kiểu NSO: trắng → xanh lá → xanh dương → tím → cam → đỏ.</summary>
        public static string LevelColor(int lv) =>
            lv >= 15 ? "#ff4040" : lv >= 12 ? "#ff9a2e" : lv >= 9 ? "#c27bff" : lv >= 6 ? "#4fa8ff" : lv >= 3 ? "#5cf05c" : "#ffffff";

        /// <summary>Tên món kèm +N tô màu (dùng trong rich text).</summary>
        public static string ColoredName(int tpl, int level) =>
            level > 0 ? $"<color={LevelColor(level)}>{ItemName(tpl)} +{level}</color>" : ItemName(tpl);

        /// <summary>Tên ô trang bị theo số slot (khớp quy ước item_template.slot bên server).</summary>
        public static readonly string[] SlotNames = { "", "Vũ khí", "Áo", "Ngọc bội", "Quần", "Găng", "Giày", "Nhẫn", "Dây chuyền", "Phù" };
        public const int EquipSlotCount = 9;

        public static void ClearSession()
        {
            Inventory.Clear(); Equipment.Clear(); EquipLevel.Clear(); MySkills.Clear(); MyQuests.Clear();
            Storage.Clear(); StorageNpc = -1; Upgrade = null; TradeWith = 0;
            MyPkMode = 0; Pvp.Clear(); Party.Clear(); PartyLeader = 0; Friends.Clear(); Mails.Clear(); OpenMail = null; MailUnread = 0;
            Guild.id = 0; Guild.members.Clear(); Tops.Clear();
            DungeonState = 0; EventState = 0; ArenaScore.Clear();
        }
    }

    public enum DataKind
    {
        Templates, Character, Inventory, Equipment, Skills, Quests,
        Money, Storage, Upgrade, Trade, Party, Friends, Mails, Guild, Top, Pvp, Event
    }
}
