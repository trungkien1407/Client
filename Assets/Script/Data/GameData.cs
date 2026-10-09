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
        public int bonusHp, bonusMp, bonusDamage, bonusDef, hpRestore, mpRestore;
        public bool tradeable;
        public string name, description;
        public bool IsEquip => slot > 0;
        public bool IsGem => type == 6;                                   // GĐ9 ngọc
        /// <summary>Số lỗ khảm (khớp server GemRules.socketsFor): dưới cấp 15 = 1, từ 15 = 2.</summary>
        public int Sockets => levelRequire >= 15 ? 2 : 1;
        /// <summary>Cấp ngọc 1..3 (khớp GemRules: id = 90 + loại·3 + cấp − 1).</summary>
        public int GemTier => IsGem ? (id - 90) % 3 + 1 : 0;
        public bool IsUsable => type >= 0 && type <= 3;
    }

    public class SkillLevel
    {
        public int point, manaUse, coolDown, damage; public float range, aoe; public string info;
        // Hiệu ứng (GĐ4): "stun" / "slow" / "burn" hoặc rỗng
        public string effect; public float effectChance; public int effectMs, effectValue;
        public int levelRequire; // GĐ7: cấp nhân vật tối thiểu để học / nâng lên cấp này (0 = không giới hạn)
        // GĐ8 — chiêu hỗ trợ (type 3) + đòn kết liễu
        public int healBase, buffDef, buffMs; public float healPct, buffDmgPct, buffCrit, executeHpPct, executeBonus;
    }

    public class SkillTpl
    {
        public int id, classId, maxLevel, type, iconId;
        public int fxCast = -1, fxHit = -1;   // GĐ12: hiệu ứng ở người ra chiêu / ở mục tiêu (GameData.Effects)
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

    public class MobTpl
    {
        public int id, level, rank; public bool aggressive; public string name;
        public int art = -1;          // GĐ12: bộ hình MobAnim_{art}; -1 = MobDatabase / ô màu
        public int hitW, hitH;        // khung va chạm (điểm gốc, 24 = 1 ô); 0 = mặc định 24 × 32 (khớp server MonsterTemplate)
        public float HalfWidth => (hitW > 0 ? hitW : 24) / (2f * Models.ArtUnits.PointsPerUnit);
        public float Height => (hitH > 0 ? hitH : 32) / Models.ArtUnits.PointsPerUnit;
    }

    /// <summary>GĐ12 — mẫu hiệu ứng (GAME_DATA_EFFECTS): mỗi khung 1 ảnh kho số, tâm đặt tại (dx, dy) điểm gốc.</summary>
    public class EffectTpl { public int id, frameMs; public Models.FramePart[] frames; }

    // =====================================================================
    // THÔNG TIN NHÂN VẬT (CHARACTER_INFO)
    // =====================================================================
    public class CharacterInfo
    {
        public int level, potential, skillPoints, sucManh, thanPhap, chakra, theLuc;
        public long exp, expToNext;
        public int maxHp, maxMp, bonusDamage, yen, xu, luong, pkPoint;
        public float dodge, crit, moveSpeed;
        public int defense; public float reduction; // GĐ7: phòng thủ + % giảm sát thương (quái / người cùng cấp)

        public void Reset()
        {
            level = potential = skillPoints = sucManh = thanPhap = chakra = theLuc = 0;
            exp = expToNext = 0;
            maxHp = maxMp = bonusDamage = yen = xu = luong = pkPoint = defense = 0;
            dodge = crit = moveSpeed = reduction = 0f;
        }
    }

    /// <summary>1 Ô ĐỒ (túi / rương / giao dịch / thư): mẫu, số lượng, cấp cường hoá +N, khoá (không giao dịch được).</summary>
    public class BagSlot
    {
        public int tpl, qty, level;
        public bool locked;
        public int bonus;                                   // GĐ9 phẩm chất 0..10 (% chỉ số gốc)
        public List<int> gems = new List<int>();            // GĐ9 ngọc đã khảm (templateId)
        public string Name => GameData.ItemName(tpl) + (level > 0 ? $" +{level}" : "");

        /// <summary>Đọc 1 danh sách ô đồ: short n, [ô đồ] x n.</summary>
        public static List<BagSlot> ReadList(Network.MessageReader r)
        {
            int n = r.ReadShort();
            var list = new List<BagSlot>(n);
            for (int i = 0; i < n; i++) list.Add(Read(r));
            return list;
        }

        /// <summary>Đọc 1 "ô đồ" (khớp server ItemService.writeSlot): int tpl, int qty, byte level, byte locked. Đổi định dạng → chỉ sửa ở đây.</summary>
        public static BagSlot Read(Network.MessageReader r)
        {
            var s = new BagSlot { tpl = r.ReadInt(), qty = r.ReadInt(), level = r.ReadByte(), locked = r.ReadByte() != 0 };
            ReadExtras(r, s);
            return s;
        }

        /// <summary>Ô trang bị đang mặc (EQUIPMENT): int tpl, byte level + đuôi GĐ9 — không có số lượng / khoá.</summary>
        public static BagSlot ReadEquip(Network.MessageReader r)
        {
            var s = new BagSlot { tpl = r.ReadInt(), qty = 1, level = r.ReadByte() };
            ReadExtras(r, s);
            return s;
        }

        /// <summary>Đuôi GĐ9 (ô túi + ô trang bị): byte bonus, byte n, [int gemId] × n.</summary>
        public static void ReadExtras(Network.MessageReader r, BagSlot s)
        {
            s.bonus = r.ReadByte();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) s.gems.Add(r.ReadInt());
        }
    }

    public class PartyMember { public int id, hp, maxHp, level; public string name; }
    public class FriendInfo { public int id, level; public string name; public bool online; }
    public class MailHeader { public long id, createdMs; public string from, title; public bool read, claimed, hasAttach; }
    public class MailBody { public long id; public string from, title, content; public int yen, xu, luong; public List<BagSlot> items = new List<BagSlot>(); public bool claimed; }
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

    /// <summary>GĐ9 — 1 món ở chợ (MARKET_LIST / MARKET_MINE). status: 0 đang bán · 1 đã bán.</summary>
    public class MarketRow { public long id; public BagSlot slot; public int price, secondsLeft, status; public string seller = ""; }

    /// <summary>GĐ9 — 1 việc trong bảng Hoạt động hằng ngày.</summary>
    public class ActivityRow { public string name; public int progress, target, pts, maxPts; }
    /// <summary>GĐ9 — 1 mốc thưởng Hoạt động.</summary>
    public class ActivityMilestone { public int need; public string reward; }
    /// <summary>GĐ9 — 1 dòng sổ tay nhiệm vụ: status 0 đang làm / 1 có thể nhận / 2 xong chờ trả; where = nơi đến (server dựng).</summary>
    public class QuestGuideRow { public int questId, status; public bool daily; public string where; }

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
        public static readonly Dictionary<int, EffectTpl> Effects = new Dictionary<int, EffectTpl>();

        // ---- trạng thái của mình ----
        public static readonly CharacterInfo Me = new CharacterInfo();
        /// <summary>Túi đồ ĐÚNG THỨ TỰ Ô (chỉ số = bagIndex gửi lên server khi mặc/bán/nâng cấp...).</summary>
        public static readonly List<BagSlot> Inventory = new List<BagSlot>();
        public static int BagCapacity = 40;
        public static readonly Dictionary<int, int> Equipment = new Dictionary<int, int>();   // slot → templateId
        public static readonly Dictionary<int, int> EquipLevel = new Dictionary<int, int>();  // slot → cấp +N
        public static readonly Dictionary<int, BagSlot> EquipSlots = new Dictionary<int, BagSlot>(); // GĐ9: slot → đủ thuộc tính (phẩm chất, ngọc)
        // GĐ9 khảm ngọc (GEM_OPEN)
        public static int GemNpc = -1, GemSocketCost = 500, GemRemoveCost = 2000, GemCombineCost = 1000;
        public static string GemMessage = "";
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

        // ---- GĐ9: hoạt động hằng ngày + sổ tay nhiệm vụ ----
        public static int ActivityPoints, ActivityClaimed;   // ActivityClaimed: bit i = đã nhận mốc i
        public static readonly List<ActivityRow> Activities = new List<ActivityRow>();
        public static readonly List<ActivityMilestone> Milestones = new List<ActivityMilestone>();
        public static readonly List<string> EventSchedule = new List<string>();
        // Quà online (đuôi ACTIVITY_INFO) + giftcode
        public static int OnlineSeconds, OnlineClaimed;
        public static float OnlineSyncTime;                              // Time.time lúc nhận OnlineSeconds → đồng hồ chạy tiếp ở client
        public static readonly List<ActivityMilestone> OnlineGifts = new List<ActivityMilestone>();   // need = số phút
        public static string GiftcodeMessage = "";
        public static bool OnlineClaimedAt(int i) => (OnlineClaimed & (1 << i)) != 0;
        public static readonly List<QuestGuideRow> QuestGuide = new List<QuestGuideRow>();
        // Chợ (MARKET_*)
        public static int MarketNpc = -1, MarketMax = 8, MarketFeePermil = 10, MarketTaxPercent = 5, MarketHours = 48;
        public static int MarketTotal, MarketPage;
        public static readonly List<MarketRow> MarketRows = new List<MarketRow>();
        public static readonly List<MarketRow> MarketMine = new List<MarketRow>();
        public static string MarketMessage = "";
        /// <summary>Phí treo bán (khớp server MarketService.listingFee): 1% giá, tối thiểu 100 yên.</summary>
        public static long MarketFee(long price) => System.Math.Max(100, price * MarketFeePermil / 1000);
        public static bool MilestoneClaimed(int i) => (ActivityClaimed & (1 << i)) != 0;
        /// <summary>Số phút online hôm nay (số server gửi + thời gian từ lúc nhận — chỉ để hiển thị, nhận quà server tự tính lại).</summary>
        public static int OnlineMinutesNow => (OnlineSeconds + (int)(UnityEngine.Time.time - OnlineSyncTime)) / 60;
        /// <summary>Có mốc đủ điểm mà chưa nhận → nút Hoạt động hiện dấu "!".</summary>
        public static bool ActivityClaimable
        {
            get
            {
                for (int i = 0; i < Milestones.Count; i++) if (ActivityPoints >= Milestones[i].need && !MilestoneClaimed(i)) return true;
                int mins = OnlineMinutesNow;
                for (int i = 0; i < OnlineGifts.Count; i++) if (mins >= OnlineGifts[i].need && !OnlineClaimedAt(i)) return true;
                return false;
            }
        }

        // ---- Bảo trì (SERVER_NOTICE) ----
        public static float MaintEndTime;            // > Time.time = đang đếm ngược bảo trì (hiện ở khung sự kiện)
        public static string KickReason;             // server báo "bị đưa ra vì bảo trì" ngay trước khi ngắt → hiện thay cho "mất kết nối"

        // ---- Map hiện tại (MAP_INFO, GĐ8) ----
        public static string MapName = "";
        public static int MapLevelMin, MapLevelMax;  // 0 = map không có quái (làng, trường)

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

        /// <summary>Hết phiên (đăng nhập mới / mất kết nối): các kho tạm ngoài GameData (đồ dưới đất, chữ cổng, chat...) tự dọn theo.</summary>
        public static event Action OnSessionReset;

        public static void ClearSession()
        {
            Me.Reset();
            Inventory.Clear(); Equipment.Clear(); EquipLevel.Clear(); EquipSlots.Clear(); GemNpc = -1; MySkills.Clear(); MyQuests.Clear();
            Storage.Clear(); StorageNpc = -1; Upgrade = null; TradeWith = 0;
            MyPkMode = 0; Pvp.Clear(); Party.Clear(); PartyLeader = 0; Friends.Clear(); Mails.Clear(); OpenMail = null; MailUnread = 0;
            Guild.id = 0; Guild.members.Clear(); Tops.Clear();
            DungeonState = 0; EventState = 0; ArenaScore.Clear(); MaintEndTime = 0;
            ActivityPoints = 0; ActivityClaimed = 0; Activities.Clear(); Milestones.Clear(); EventSchedule.Clear(); QuestGuide.Clear();
            OnlineSeconds = 0; OnlineClaimed = 0; OnlineGifts.Clear(); GiftcodeMessage = "";
            MarketNpc = -1; MarketRows.Clear(); MarketMine.Clear(); MarketMessage = "";
            OnSessionReset?.Invoke();
        }
    }

    public enum DataKind
    {
        Templates, Character, Inventory, Equipment, Skills, Quests,
        Money, Storage, Upgrade, Trade, Party, Friends, Mails, Guild, Top, Pvp, Event,
        Activity, QuestGuide, Market, Gem
    }
}
