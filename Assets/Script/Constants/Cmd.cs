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

        // ==========================================
        // 2. NHÂN VẬT & DI CHUYỂN (100 - 199)
        // ==========================================
        public const short PLAYER_MOVE = 100;       // C→S float x, float y, byte dir, byte state
        public const short ENTER_MAP = 101;
        public const short CHANGE_MAP = 102;        // S→C short mapId, byte zoneId, float x, float y
        public const short FORCE_MOVE = 103;        // S→C float x, float y (server kéo về vì sai/hack)
        public const short PLAYER_ADD = 104;
        public const short PLAYER_REMOVE = 105;
        public const short PLAYER_LIST = 106;
        public const short PLAYER_MOVE_BATCH = 107;
        public const short CHANGE_ZONE = 108;

        // ==========================================
        // 3. CHIẾN ĐẤU & KỸ NĂNG (200 - 299)
        // ==========================================
        public const short USE_SKILL = 200;         // C→S int skillTemplateId, byte targetType(0 quái/1 người), int targetId
        public const short PLAYER_DIE = 201;        // S→C int playerId, long expLost
        public const short REVIVE = 202;            // C→S rỗng | S→C int id, float x, float y, int hp, int maxHp, int mp, int maxMp
        public const short BROADCAST_ATTACK = 203;  // S→C int attackerId, byte targetType, int targetId, int skillId, int damage, int hpRemain, byte dead
        public const short PLAYER_EXP_UPDATE = 204; // S→C long exp, short level, int maxHp, int maxMp, int hp, int mp

        // ==========================================
        // 4. VẬT PHẨM & TÚI ĐỒ (300 - 399)
        // ==========================================
        public const short USE_ITEM = 300;          // C→S int templateId
        public const short PICK_ITEM = 301;         // C→S int groundItemId
        public const short INVENTORY = 302;         // S→C short count, [int templateId, int qty] x count
        public const short ITEM_DROP = 303;         // S→C int groundId, int templateId, int qty, float x, float y
        public const short ITEM_REMOVE = 304;       // S→C int groundId
        public const short EQUIP_ITEM = 305;
        public const short UNEQUIP_ITEM = 306;
        public const short EQUIPMENT = 307;         // S→C short count, [int slot, int templateId] x count
        public const short OPEN_SHOP = 308;
        public const short SHOP_DATA = 309;
        public const short BUY_ITEM = 310;
        public const short SELL_ITEM = 311;
        public const short PLAYER_STATS = 312;      // S→C int maxHp, int maxMp, int hp, int mp, int bonusDamage, int yen

        public const short CLIENT_READY = 400;

        // ==========================================
        // QUÁI VẬT (500 - 599)
        // ==========================================
        public const short MOB_ADD = 500;           // S→C MOB (quái hồi sinh)
        public const short MOB_LIST = 501;
        public const short MOB_MOVE = 502;
        public const short MOB_DIE = 503;           // S→C int mobId
        public const short MOB_MOVE_BATCH = 504;
        public const short MOB_ATTACK = 505;        // S→C int mobId, int playerId, int damage, int playerHpRemain

        // ==========================================
        // 5. CẬP NHẬT TÀI NGUYÊN (600)
        // ==========================================
        public const short CHECK_VERSION = 600;

        // ==========================================
        // XÃ HỘI (700 - 799)
        // ==========================================
        public const short CHAT = 700;              // C→S byte channel, [UTF target nếu channel 2], UTF msg | S→C byte channel, int fromId, UTF fromName, UTF msg
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

        // ==========================================
        // NPC (800)
        // ==========================================
        public const short NPC_LIST = 800;

        public const short PLAYER_TAKE_DAMGE = 900; // (chưa dùng)
        public const short PLAYER_HEAL = 901;       // S→C int id, int hpHeal, int mpHeal, int hp, int maxHp, int mp, int maxMp
    }
}
