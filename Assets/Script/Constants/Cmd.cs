namespace Assets.Script.Constants
{
    /// <summary>
    /// Bảng opcode — PHẢI khớp 100% với server: D:\Server\Server\src\main\java\com\server\constant\Cmd.java
    /// Payload từng lệnh: xem docs/PROTOCOL.md (repo server).
    /// C→S = client gửi lên, S→C = server gửi xuống.
    /// </summary>
    public static class Cmd
    {
        // ==========================================
        // 1. HỆ THỐNG & TÀI KHOẢN (0 - 99)
        // ==========================================
        public const short LOGIN = 1;
        public const short REGISTER = 2;
        public const short LOGOUT = 3;
        public const short HEARTBEAT = 4;           // C→S giữ kết nối (server kick nếu im > 60s)
        public const short CREATE_CHARACTER = 5;
        public const short CLIENT_ERROR = 6;        // C→S UTF version, UTF platform, UTF message, UTF stack — Core/ErrorReporter gửi khi game gặp lỗi
        public const short SERVER_NOTICE = 7;       // S→C byte type(1 thông báo/2 đếm ngược bảo trì/3 huỷ bảo trì/4 bị đưa ra vì bảo trì), int secs, UTF text

        // ==========================================
        // 2. NHÂN VẬT & DI CHUYỂN (100 - 199)
        // ==========================================
        public const short PLAYER_MOVE = 100;       // C→S float x, float y, byte dir, byte state
        public const short CHANGE_MAP = 102;        // S→C short mapId, byte zoneId, float x, float y
        public const short FORCE_MOVE = 103;        // S→C float x, float y (server kéo về vì sai/hack)
        public const short PLAYER_ADD = 104;
        public const short PLAYER_REMOVE = 105;
        public const short PLAYER_LIST = 106;
        public const short PLAYER_MOVE_BATCH = 107;
        public const short CHANGE_ZONE = 108;       // C→S byte zoneId | S→C byte zoneId (đổi khu thành công)
        public const short ZONE_LIST_REQ = 109;     // C→S (rỗng) xin danh sách khu
        public const short ZONE_LIST = 110;         // S→C byte currentZone(255 = khu riêng), byte n, [byte zoneId, byte players, byte max] x n

        // ==========================================
        // 3. CHIẾN ĐẤU & KỸ NĂNG (200 - 299)
        // ==========================================
        public const short USE_SKILL = 200;         // C→S int skillTemplateId, byte targetType(0 quái/1 người), int targetId
        public const short PLAYER_DIE = 201;        // S→C int playerId, long expLost
        public const short REVIVE = 202;            // C→S rỗng | S→C int id, float x, float y, int hp, int maxHp, int mp, int maxMp
        public const short BROADCAST_ATTACK = 203;  // S→C int attackerId, byte targetType, int targetId, int skillId, int damage, int hpRemain, byte dead
        public const short PLAYER_EXP_UPDATE = 204; // S→C long exp, short level, int maxHp, int maxMp, int hp, int mp
        public const short ADD_POTENTIAL = 205;     // C→S byte stat (0 Sức mạnh/1 Thân pháp/2 Chakra/3 Thể lực), short amount
        public const short CHARACTER_INFO = 206;    // S→C bảng chỉ số nhân vật (xem GameDataNetwork.OnCharacterInfo)
        public const short SKILL_UPGRADE = 207;     // C→S int skillTemplateId
        public const short SKILL_LIST = 208;        // S→C short count, [int templateId, short point]
        public const short SET_SKILL_SHORTCUT = 209; // C→S byte slot 0..4, int skillTemplateId (-1 = xoá)
        // ---- GĐ3: PvP ----
        public const short PK_MODE = 210;           // C→S byte mode (0 hoà bình / 1 đồ sát)
        public const short PLAYER_PVP_INFO = 211;   // S→C int playerId, byte pkMode, short pkPoint, UTF guildName
        public const short DUEL_REQUEST = 212;      // C→S int targetPlayerId
        public const short DUEL_INVITE = 213;       // S→C int fromId, UTF fromName
        public const short DUEL_ACCEPT = 214;       // C→S int fromId
        public const short DUEL_STATE = 215;        // S→C byte state(0 kết thúc/1 đếm ngược/2 đang đấu), int opponentId, UTF msg
        // ---- GĐ4: hiệu ứng ----
        public const short EFFECT = 216;            // S→C byte targetType(0 quái/1 người), int targetId, byte effect(1 choáng/2 chậm/3 bỏng), int ms

        // ==========================================
        // 4. VẬT PHẨM & TÚI ĐỒ (300 - 399)
        // ==========================================
        public const short USE_ITEM = 300;          // C→S int templateId (server dùng ô đầu tiên có món đó)
        public const short PICK_ITEM = 301;         // C→S int groundItemId
        public const short INVENTORY = 302;         // S→C short capacity, short n, [int templateId, int qty, byte level, byte locked] x n
        public const short ITEM_DROP = 303;         // S→C int groundId, int templateId, int qty, float x, float y
        public const short ITEM_REMOVE = 304;       // S→C int groundId
        public const short EQUIP_ITEM = 305;        // C→S int bagIndex
        public const short UNEQUIP_ITEM = 306;      // C→S int slot
        public const short EQUIPMENT = 307;         // S→C short n, [int slot, int templateId, byte level] x n
        public const short OPEN_SHOP = 308;
        public const short SHOP_DATA = 309;         // S→C int npcId, short n, [int itemId, int price, byte currency(0 yên/1 xu/2 lượng)]
        public const short BUY_ITEM = 310;          // C→S int npcId, int itemId, int qty
        public const short SELL_ITEM = 311;         // C→S int bagIndex, int qty
        public const short PLAYER_STATS = 312;      // S→C int maxHp, int maxMp, int hp, int mp, int bonusDamage, int yen
        public const short MONEY_UPDATE = 313;      // S→C int yen, int xu, int luong
        public const short BAG_SORT = 314;          // C→S (rỗng) sắp xếp + gộp túi
        // ---- GĐ2: nâng cấp / rương / giao dịch ----
        public const short UPGRADE_OPEN = 320;      // S→C bảng tỉ lệ (xem SocialNetwork.OnUpgradeOpen)
        public const short UPGRADE_ITEM = 321;      // C→S int npcId, int bagIndex, byte useProtect
        public const short UPGRADE_RESULT = 322;    // S→C byte result(0 ok/1 trượt giữ/2 trượt tụt/3 lỗi), byte level, UTF msg
        public const short STORAGE_DATA = 325;      // S→C int npcId, short capacity, short n, [ô đồ] x n
        public const short STORAGE_PUT = 326;       // C→S int bagIndex, int qty
        public const short STORAGE_TAKE = 327;      // C→S int storageIndex, int qty
        public const short TRADE_REQUEST = 330;     // C→S int targetPlayerId
        public const short TRADE_INVITE = 331;      // S→C int fromId, UTF fromName
        public const short TRADE_ACCEPT = 332;      // C→S int fromId
        public const short TRADE_ADD_ITEM = 333;    // C→S int bagIndex, int qty
        public const short TRADE_SET_YEN = 334;     // C→S int yen
        public const short TRADE_LOCK = 335;        // C→S (rỗng)
        public const short TRADE_CONFIRM = 336;     // C→S (rỗng)
        public const short TRADE_CANCEL = 337;      // C→S (rỗng)
        public const short TRADE_UPDATE = 338;      // S→C int otherId, UTF otherName, [phần mình], [phần bên kia]
        public const short TRADE_CLOSE = 339;       // S→C byte result(0 huỷ/1 xong), UTF lý do

        public const short CLIENT_READY = 400;

        // ==========================================
        // QUÁI VẬT (500 - 599)
        // ==========================================
        public const short MOB_ADD = 500;           // S→C MOB (quái hồi sinh)
        public const short MOB_LIST = 501;
        public const short MOB_DIE = 503;           // S→C int mobId
        public const short MOB_MOVE_BATCH = 504;
        public const short MOB_ATTACK = 505;        // S→C int mobId, int playerId, int damage, int playerHpRemain
        public const short MOB_REMOVE = 506;        // S→C int mobId — quái biến mất hẳn (boss thế giới, quái phó bản)

        // ==========================================
        // 5. CẬP NHẬT TÀI NGUYÊN (600)
        // ==========================================
        public const short CHECK_VERSION = 600;
        public const short GAME_DATA_ITEMS = 601;   // S→C mẫu vật phẩm (gửi 1 lần sau CLIENT_READY)
        public const short GAME_DATA_SKILLS = 602;  // S→C mẫu kỹ năng + thông số từng cấp
        public const short GAME_DATA_QUESTS = 603;  // S→C mẫu nhiệm vụ
        public const short GAME_DATA_MOBS = 604;    // S→C mẫu quái (tên, cấp, hạng)
        public const short GAME_DATA_NPCS = 605;    // S→C tên NPC

        // ==========================================
        // XÃ HỘI (700 - 799)
        // ==========================================
        public const short CHAT = 700;              // C→S byte channel(0 TG/1 khu/2 riêng/4 gia tộc), [UTF target nếu channel 2], UTF msg | S→C byte channel(3 = hệ thống), int fromId, UTF fromName, UTF msg
        public const short PARTY_INVITE = 710;
        public const short PARTY_INVITE_RECV = 711;
        public const short PARTY_ACCEPT = 712;
        public const short PARTY_LEAVE = 713;
        public const short PARTY_INFO = 714;
        public const short PARTY_KICK = 715;
        public const short QUEST_ACCEPT = 720;
        public const short QUEST_COMPLETE = 721;
        public const short QUEST_UPDATE = 722;      // S→C int questId, int progress, byte done
        public const short QUEST_LIST = 723;        // S→C short count, [int questId, int progress, byte done] x count
        // ---- GĐ3: bạn bè / thư / gia tộc / xếp hạng ----
        public const short FRIEND_LIST_REQ = 730;   // C→S
        public const short FRIEND_LIST = 731;       // S→C short n, [int id, UTF name, short level, byte online]
        public const short FRIEND_REQUEST = 732;    // C→S UTF name
        public const short FRIEND_INVITE = 733;     // S→C int fromId, UTF fromName
        public const short FRIEND_ACCEPT = 734;     // C→S int fromId
        public const short FRIEND_REMOVE = 735;     // C→S int friendId
        public const short MAIL_LIST_REQ = 740;     // C→S
        public const short MAIL_LIST = 741;         // S→C short n, [long id, UTF from, UTF title, byte read, byte claimed, byte hasAttach, long createdMs]
        public const short MAIL_READ = 742;         // C→S long id
        public const short MAIL_CONTENT = 743;      // S→C long id, UTF from, UTF title, UTF content, int yen, [ô đồ], byte claimed
        public const short MAIL_CLAIM = 744;        // C→S long id
        public const short MAIL_SEND = 745;         // C→S UTF to, UTF title, UTF content, int yen, int bagIndex(-1), int qty
        public const short MAIL_DELETE = 746;       // C→S long id
        public const short MAIL_NOTIFY = 747;       // S→C short unread
        public const short GUILD_CREATE = 750;      // C→S UTF name
        public const short GUILD_INFO_REQ = 751;    // C→S
        public const short GUILD_INFO = 752;        // S→C int id(0 = chưa có) + chi tiết (xem SocialNetwork.OnGuildInfo)
        public const short GUILD_INVITE = 753;      // C→S int targetPlayerId
        public const short GUILD_INVITE_RECV = 754; // S→C int guildId, UTF guildName, UTF fromName
        public const short GUILD_ACCEPT = 755;      // C→S int guildId
        public const short GUILD_LEAVE = 756;       // C→S
        public const short GUILD_KICK = 757;        // C→S int playerId
        public const short GUILD_PROMOTE = 758;     // C→S int playerId, byte rank(0 thành viên/1 trưởng lão)
        public const short GUILD_DONATE = 759;      // C→S int yen
        public const short GUILD_NOTICE = 760;      // C→S UTF notice
        public const short TOP_REQUEST = 770;       // C→S byte type(0 cấp/1 tài phú/2 gia tộc/3 cừu sát)
        public const short TOP_LIST = 771;          // S→C byte type, short n, [short rank, UTF name, long value, UTF extra]

        // ==========================================
        // NPC (800)
        // ==========================================
        public const short NPC_LIST = 800;
        public const short NPC_TALK = 801;          // C→S int npcId
        public const short NPC_MENU = 802;          // S→C int npcId, UTF tên, UTF lời thoại, byte n, [UTF lựa chọn]
        public const short NPC_SELECT = 803;        // C→S int npcId, byte index
        public const short MAP_INFO = 804;          // S→C (GĐ8) UTF tên map, short cấp quái thấp, short cao, byte n, [float x, float y, short mapId đích, UTF tên đích, byte kiểu 0 trái/1 phải/2 giữa]

        // ---- GĐ4: phó bản / sự kiện ----
        public const short DUNGEON_STATE = 850;     // S→C byte state(2 đang chơi/3 thắng/4 thua/0 rời), int secs, short mobsLeft, UTF name
        public const short EVENT_STATE = 851;       // S→C byte type(1 boss/2 lôi đài), byte state(0 hết/1 báo trước/2 đang diễn ra), int secs, UTF text
        public const short ARENA_SCORE = 852;
        // GĐ9 — CHỢ ("ô đồ" = BagSlot.Read)
        public const short MARKET_SEARCH = 340;     // C→S byte loại(0 tất cả/1 vũ khí/2 trang phục/3 bình/4 nguyên liệu), byte sắp xếp(0 mới/1 rẻ), short trang, UTF từ khoá
        public const short MARKET_LIST = 341;       // S→C short tổng, short trang, short n, [long id, ô đồ, int giá, UTF người bán, int giây còn lại]
        public const short MARKET_SELL = 342;       // C→S int bagIndex, int qty, int giá
        public const short MARKET_BUY = 343;        // C→S long id
        public const short MARKET_CANCEL = 344;     // C→S long id
        public const short MARKET_MINE_REQ = 345;   // C→S rỗng
        public const short MARKET_MINE = 346;       // S→C short n, [long id, ô đồ, int giá, int giây còn lại, byte trạng thái]
        public const short MARKET_RESULT = 347;     // S→C byte ok, UTF lời báo
        public const short MARKET_OPEN = 348;       // S→C int npcId, byte tối đa món, short phí ‰, byte thuế %, short giờ treo
        // GĐ9 — KHẢM NGỌC (ở Thợ Rèn)
        public const short GEM_OPEN = 350;          // S→C int npcId, short phí khảm/cấp, short phí tháo/cấp, short phí ghép/cấp
        public const short GEM_SOCKET = 351;        // C→S int bagIndex trang bị, int bagIndex ngọc
        public const short GEM_REMOVE = 352;        // C→S int bagIndex trang bị, byte vị trí lỗ
        public const short GEM_COMBINE = 353;       // C→S int templateId ngọc (3 → 1 cấp kế)
        public const short GEM_RESULT = 354;        // S→C byte ok, UTF lời báo
        // GĐ9 — hoạt động hằng ngày, sổ tay nhiệm vụ, mẹo theo cấp
        public const short QUEST_GUIDE_REQ = 724;   // C→S rỗng
        public const short QUEST_GUIDE = 725;       // S→C short n, [int questId, byte status(0 đang làm/1 có thể nhận/2 chờ trả), byte daily, UTF nơi đến]
        public const short ACTIVITY_REQ = 860;      // C→S rỗng
        public const short ACTIVITY_INFO = 861;     // S→C short điểm, byte bit mốc đã nhận, byte n,[UTF tên, short tiến độ, short đích, byte điểm có, byte điểm tối đa], byte m,[short cần, UTF thưởng], byte k,[UTF dòng lịch]
        public const short ACTIVITY_CLAIM = 862;    // C→S byte chỉ số mốc
        public const short GUIDE_TIP = 863;         // S→C UTF tiêu đề, UTF nội dung, byte mục cẩm nang       // S→C short n, [UTF name, short kills]

        public const short PLAYER_HEAL = 901;       // S→C int id, int hpHeal, int mpHeal, int hp, int maxHp, int mp, int maxMp
    }
}
