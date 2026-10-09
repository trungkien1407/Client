using Assets.Script.Constants;
using Assets.Script.Network;

namespace Assets.Script.Data
{
    /// <summary>
    /// NƠI DUY NHẤT GỬI GÓI LÊN SERVER (C→S). Mọi lớp khác chỉ gọi các hàm này, không tự viết MessageWriter
    /// hay gọi NetworkManager.Send → muốn biết client gửi gì, định dạng ra sao: đọc file này (khớp docs/PROTOCOL.md).
    /// Server kiểm tra hợp lệ rồi trả kết quả (INVENTORY, CHARACTER_INFO...).
    /// </summary>
    public static class GameActions
    {
        private static void Send(short cmd, System.Action<MessageWriter> write)
        {
            var w = new MessageWriter();
            write?.Invoke(w);
            NetworkManager.Instance?.Send(cmd, w.ToArray());
            w.Cleanup();
        }

        // ---- Kết nối / tài khoản ----
        public static void CheckVersion(string version) => Send(Cmd.CHECK_VERSION, w => w.WriteUTF(version));
        public static void Login(string user, string pass) => Send(Cmd.LOGIN, w => { w.WriteUTF(user); w.WriteUTF(pass); });
        public static void Register(string user, string pass, string email) => Send(Cmd.REGISTER, w => { w.WriteUTF(user); w.WriteUTF(pass); w.WriteUTF(email); });
        /// <param name="classId">1 Đấu sĩ · 2 Hỗ trợ · 3 Sát thủ</param>
        public static void CreateCharacter(string name, int classId) => Send(Cmd.CREATE_CHARACTER, w => { w.WriteUTF(name); w.WriteByte((byte)classId); });
        /// <summary>Đã tải xong map / đổi khu xong → server cho vào khu và gửi người, quái, NPC.</summary>
        public static void ClientReady() => Send(Cmd.CLIENT_READY, null);
        /// <summary>Giữ kết nối (server ngắt nếu 60 giây không nhận gói nào).</summary>
        public static void Heartbeat() => Send(Cmd.HEARTBEAT, null);
        public static void ReportError(string version, string platform, string message, string stack) =>
            Send(Cmd.CLIENT_ERROR, w => { w.WriteUTF(version); w.WriteUTF(platform); w.WriteUTF(message); w.WriteUTF(stack); });

        // ---- Di chuyển / khu / chiến đấu ----
        /// <param name="dir">1 quay trái · 0 quay phải</param>
        /// <param name="state">0 đứng · 1 chạy · 2 nhảy lên · 3 rơi</param>
        public static void Move(float x, float y, byte dir, byte state) => Send(Cmd.PLAYER_MOVE, w => { w.WriteFloat(x); w.WriteFloat(y); w.WriteByte(dir); w.WriteByte(state); });
        public static void ZoneListRequest() => Send(Cmd.ZONE_LIST_REQ, null);
        public static void ChangeZone(int zoneId) => Send(Cmd.CHANGE_ZONE, w => w.WriteByte((byte)zoneId));
        /// <param name="targetType">0 quái · 1 người · 2 bản thân / nhóm (chiêu hỗ trợ)</param>
        public static void UseSkill(int skillId, byte targetType, int targetId) => Send(Cmd.USE_SKILL, w => { w.WriteInt(skillId); w.WriteByte(targetType); w.WriteInt(targetId); });
        public static void Revive() => Send(Cmd.REVIVE, null);
        public static void PickItem(int groundItemId) => Send(Cmd.PICK_ITEM, w => w.WriteInt(groundItemId));

        // ---- Vật phẩm ----
        public static void UseItem(int templateId) => Send(Cmd.USE_ITEM, w => w.WriteInt(templateId));
        /// <param name="bagIndex">vị trí ô trong túi (GameData.Inventory) — 2 món cùng loại khác cấp +N là 2 ô khác nhau</param>
        public static void Equip(int bagIndex) => Send(Cmd.EQUIP_ITEM, w => w.WriteInt(bagIndex));
        public static void Unequip(int slot) => Send(Cmd.UNEQUIP_ITEM, w => w.WriteInt(slot));
        public static void Buy(int npcId, int templateId, int qty) => Send(Cmd.BUY_ITEM, w => { w.WriteInt(npcId); w.WriteInt(templateId); w.WriteInt(qty); });
        public static void Sell(int bagIndex, int qty) => Send(Cmd.SELL_ITEM, w => { w.WriteInt(bagIndex); w.WriteInt(qty); });
        public static void SortBag() => Send(Cmd.BAG_SORT, null);

        // ---- Nâng cấp / rương (GĐ2) ----
        public static void Upgrade(int npcId, int bagIndex, bool useProtect) =>
            Send(Cmd.UPGRADE_ITEM, w => { w.WriteInt(npcId); w.WriteInt(bagIndex); w.WriteByte((byte)(useProtect ? 1 : 0)); });
        public static void StoragePut(int bagIndex, int qty) => Send(Cmd.STORAGE_PUT, w => { w.WriteInt(bagIndex); w.WriteInt(qty); });
        public static void StorageTake(int storageIndex, int qty) => Send(Cmd.STORAGE_TAKE, w => { w.WriteInt(storageIndex); w.WriteInt(qty); });

        // ---- Giao dịch (GĐ2) ----
        public static void TradeRequest(int playerId) => Send(Cmd.TRADE_REQUEST, w => w.WriteInt(playerId));
        public static void TradeAccept(int fromId) => Send(Cmd.TRADE_ACCEPT, w => w.WriteInt(fromId));
        public static void TradeAddItem(int bagIndex, int qty) => Send(Cmd.TRADE_ADD_ITEM, w => { w.WriteInt(bagIndex); w.WriteInt(qty); });
        public static void TradeSetYen(int yen) => Send(Cmd.TRADE_SET_YEN, w => w.WriteInt(yen));
        public static void TradeLock() => Send(Cmd.TRADE_LOCK, null);
        public static void TradeConfirm() => Send(Cmd.TRADE_CONFIRM, null);
        public static void TradeCancel() => Send(Cmd.TRADE_CANCEL, null);

        // ---- PvP (GĐ3) ----
        public static void SetPkMode(int mode) => Send(Cmd.PK_MODE, w => w.WriteByte((byte)mode));
        public static void DuelRequest(int playerId) => Send(Cmd.DUEL_REQUEST, w => w.WriteInt(playerId));
        public static void DuelAccept(int fromId) => Send(Cmd.DUEL_ACCEPT, w => w.WriteInt(fromId));

        // ---- Nhóm ----
        public static void PartyInvite(int playerId) => Send(Cmd.PARTY_INVITE, w => w.WriteInt(playerId));
        public static void PartyAccept(int inviterId) => Send(Cmd.PARTY_ACCEPT, w => w.WriteInt(inviterId));
        public static void PartyLeave() => Send(Cmd.PARTY_LEAVE, null);
        public static void PartyKick(int playerId) => Send(Cmd.PARTY_KICK, w => w.WriteInt(playerId));

        // ---- Bạn bè ----
        public static void FriendList() => Send(Cmd.FRIEND_LIST_REQ, null);
        public static void FriendRequest(string name) => Send(Cmd.FRIEND_REQUEST, w => w.WriteUTF(name));
        public static void FriendAccept(int fromId) => Send(Cmd.FRIEND_ACCEPT, w => w.WriteInt(fromId));
        public static void FriendRemove(int id) => Send(Cmd.FRIEND_REMOVE, w => w.WriteInt(id));

        // ---- Thư ----
        public static void MailList() => Send(Cmd.MAIL_LIST_REQ, null);
        public static void MailRead(long id) => Send(Cmd.MAIL_READ, w => w.WriteLong(id));
        public static void MailClaim(long id) => Send(Cmd.MAIL_CLAIM, w => w.WriteLong(id));
        public static void MailDelete(long id) => Send(Cmd.MAIL_DELETE, w => w.WriteLong(id));
        public static void MailSend(string to, string title, string content, int yen, int bagIndex, int qty) =>
            Send(Cmd.MAIL_SEND, w => { w.WriteUTF(to); w.WriteUTF(title); w.WriteUTF(content); w.WriteInt(yen); w.WriteInt(bagIndex); w.WriteInt(qty); });

        // ---- Gia tộc ----
        public static void GuildCreate(string name) => Send(Cmd.GUILD_CREATE, w => w.WriteUTF(name));
        public static void GuildInfo() => Send(Cmd.GUILD_INFO_REQ, null);
        public static void GuildInvite(int playerId) => Send(Cmd.GUILD_INVITE, w => w.WriteInt(playerId));
        public static void GuildAccept(int guildId) => Send(Cmd.GUILD_ACCEPT, w => w.WriteInt(guildId));
        public static void GuildLeave() => Send(Cmd.GUILD_LEAVE, null);
        public static void GuildKick(int playerId) => Send(Cmd.GUILD_KICK, w => w.WriteInt(playerId));
        public static void GuildPromote(int playerId, int rank) => Send(Cmd.GUILD_PROMOTE, w => { w.WriteInt(playerId); w.WriteByte((byte)rank); });
        public static void GuildDonate(int yen) => Send(Cmd.GUILD_DONATE, w => w.WriteInt(yen));
        public static void GuildNotice(string notice) => Send(Cmd.GUILD_NOTICE, w => w.WriteUTF(notice));

        // ---- Xếp hạng ----
        /// <param name="type">0 cấp · 1 tài phú · 2 gia tộc · 3 cừu sát</param>
        public static void Top(int type) => Send(Cmd.TOP_REQUEST, w => w.WriteByte((byte)type));

        // ---- Chat ----
        /// <param name="channel">0 thế giới · 1 khu (cũng là kênh lệnh GM "/...") · 4 gia tộc</param>
        public static void Chat(int channel, string msg) => Send(Cmd.CHAT, w => { w.WriteByte((byte)channel); w.WriteUTF(msg); });
        /// <summary>Chat riêng (kênh 2) tới 1 người theo tên.</summary>
        public static void Whisper(string toName, string msg) => Send(Cmd.CHAT, w => { w.WriteByte(2); w.WriteUTF(toName); w.WriteUTF(msg); });

        // ---- Nhân vật / kỹ năng ----
        /// <param name="stat">0 Sức mạnh · 1 Thân pháp · 2 Chakra · 3 Thể lực</param>
        public static void AddPotential(byte stat, short amount) => Send(Cmd.ADD_POTENTIAL, w => { w.WriteByte(stat); w.WriteShort(amount); });
        public static void UpgradeSkill(int skillId) => Send(Cmd.SKILL_UPGRADE, w => w.WriteInt(skillId));
        public static void SetShortcut(int slot, int skillId) => Send(Cmd.SET_SKILL_SHORTCUT, w => { w.WriteByte((byte)slot); w.WriteInt(skillId); });

        // ---- GĐ9: hoạt động hằng ngày, sổ tay nhiệm vụ ----
        public static void ActivityRequest() => Send(Cmd.ACTIVITY_REQ, null);
        public static void ActivityClaim(int milestoneIndex) => Send(Cmd.ACTIVITY_CLAIM, w => w.WriteByte((byte)milestoneIndex));
        public static void QuestGuide() => Send(Cmd.QUEST_GUIDE_REQ, null);
        public static void OnlineClaim(int index) => Send(Cmd.ONLINE_CLAIM, w => w.WriteByte((byte)index));
        public static void GiftcodeUse(string code) => Send(Cmd.GIFTCODE_USE, w => w.WriteUTF(code ?? ""));

        // ---- GĐ9: khảm ngọc ----
        public static void GemSocket(int equipBagIndex, int gemBagIndex) => Send(Cmd.GEM_SOCKET, w => { w.WriteInt(equipBagIndex); w.WriteInt(gemBagIndex); });
        public static void GemRemove(int equipBagIndex, int pos) => Send(Cmd.GEM_REMOVE, w => { w.WriteInt(equipBagIndex); w.WriteByte((byte)pos); });
        public static void GemCombine(int gemTemplateId) => Send(Cmd.GEM_COMBINE, w => w.WriteInt(gemTemplateId));

        // ---- GĐ9: chợ ----
        /// <param name="category">0 tất cả · 1 vũ khí · 2 trang phục · 3 bình · 4 nguyên liệu</param>
        /// <param name="sort">0 mới nhất · 1 rẻ nhất (theo giá 1 cái)</param>
        public static void MarketSearch(int category, int sort, int page, string keyword) =>
            Send(Cmd.MARKET_SEARCH, w => { w.WriteByte((byte)category); w.WriteByte((byte)sort); w.WriteShort((short)page); w.WriteUTF(keyword ?? ""); });
        public static void MarketSell(int bagIndex, int qty, int price) => Send(Cmd.MARKET_SELL, w => { w.WriteInt(bagIndex); w.WriteInt(qty); w.WriteInt(price); });
        public static void MarketBuy(long id) => Send(Cmd.MARKET_BUY, w => w.WriteLong(id));
        public static void MarketCancel(long id) => Send(Cmd.MARKET_CANCEL, w => w.WriteLong(id));
        public static void MarketMine() => Send(Cmd.MARKET_MINE_REQ, null);

        // ---- NPC ----
        public static void NpcTalk(int npcId) => Send(Cmd.NPC_TALK, w => w.WriteInt(npcId));
        public static void NpcSelect(int npcId, int index) => Send(Cmd.NPC_SELECT, w => { w.WriteInt(npcId); w.WriteByte((byte)index); });
    }
}
