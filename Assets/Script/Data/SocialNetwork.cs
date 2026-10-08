using System.Collections.Generic;
using Assets.Script.Combat;
using Assets.Script.Constants;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Data
{
    /// <summary>
    /// NHẬN CÁC GÓI GĐ2–GĐ4 rồi ghi vào GameData + mở cửa sổ tương ứng:
    ///   GĐ2: UPGRADE_OPEN/RESULT, STORAGE_DATA, TRADE_INVITE/UPDATE/CLOSE
    ///   GĐ3: PLAYER_PVP_INFO, DUEL_INVITE/STATE, PARTY_INVITE_RECV/INFO, FRIEND_*, MAIL_*, GUILD_*, TOP_LIST
    ///   GĐ4: EFFECT, DUNGEON_STATE, EVENT_STATE, ARENA_SCORE
    /// Payload từng gói: xem docs/PROTOCOL.md (repo server) — mục 6.15 trở đi.
    /// Lời mời (nhóm/giao dịch/bạn/tỉ thí/gia tộc) hiện hộp xác nhận ConfirmWindow, KHÔNG tự đồng ý.
    /// </summary>
    public class SocialNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
            // GĐ2
            Listen(Cmd.UPGRADE_OPEN, OnUpgradeOpen);
            Listen(Cmd.UPGRADE_RESULT, OnUpgradeResult);
            Listen(Cmd.STORAGE_DATA, OnStorage);
            Listen(Cmd.TRADE_INVITE, OnTradeInvite);
            Listen(Cmd.TRADE_UPDATE, OnTradeUpdate);
            Listen(Cmd.TRADE_CLOSE, OnTradeClose);
            // GĐ3
            Listen(Cmd.PLAYER_PVP_INFO, OnPvpInfo);
            Listen(Cmd.DUEL_INVITE, OnDuelInvite);
            Listen(Cmd.DUEL_STATE, OnDuelState);
            Listen(Cmd.PARTY_INVITE_RECV, OnPartyInvite);
            Listen(Cmd.PARTY_INFO, OnPartyInfo);
            Listen(Cmd.FRIEND_LIST, OnFriendList);
            Listen(Cmd.FRIEND_INVITE, OnFriendInvite);
            Listen(Cmd.MAIL_LIST, OnMailList);
            Listen(Cmd.MAIL_CONTENT, OnMailContent);
            Listen(Cmd.MAIL_NOTIFY, OnMailNotify);
            Listen(Cmd.GUILD_INFO, OnGuildInfo);
            Listen(Cmd.GUILD_INVITE_RECV, OnGuildInvite);
            Listen(Cmd.TOP_LIST, OnTopList);
            // GĐ4
            Listen(Cmd.EFFECT, OnEffect);
            Listen(Cmd.DUNGEON_STATE, OnDungeonState);
            Listen(Cmd.EVENT_STATE, OnEventState);
            Listen(Cmd.ARENA_SCORE, OnArenaScore);
            Listen(Cmd.ZONE_LIST, OnZoneList);
            // GĐ6 — trang quản trị: thông báo + bảo trì
            Listen(Cmd.SERVER_NOTICE, OnServerNotice);
            // GĐ8 — tên map + cổng
            Listen(Cmd.MAP_INFO, OnMapInfo);
        }

        /// <summary>
        /// MAP_INFO (GĐ8, mỗi lần vào khu): UTF tên map, short cấp quái thấp, short cao (0 = không có quái), byte n,
        /// [float x, float y, short mapId đích, UTF tên map đích, byte kiểu 0 mép trái / 1 mép phải / 2 cổng giữa] x n.
        /// Vào map mới → hiện tên map giữa màn hình; cổng nào cũng có chữ chỉ đường (PortalMarkers).
        /// </summary>
        private void OnMapInfo(byte[] data)
        {
            var r = new MessageReader(data);
            string name = r.ReadUTF();
            int lo = r.ReadShort(), hi = r.ReadShort();
            int n = r.ReadByte();
            var portals = new List<Map.PortalMarkers.Info>(n);
            for (int i = 0; i < n; i++)
            {
                float x = r.ReadFloat(), y = r.ReadFloat();
                r.ReadShort(); // mapId đích (chưa dùng)
                string target = r.ReadUTF();
                int side = r.Available() > 0 ? r.ReadByte() : 2;
                portals.Add(new Map.PortalMarkers.Info { pos = new Vector2(x, y), target = target, side = side });
            }
            r.Cleanup();
            bool newMap = GameData.MapName != name;
            GameData.MapName = name; GameData.MapLevelMin = lo; GameData.MapLevelMax = hi;
            if (newMap && !string.IsNullOrEmpty(name))
                UI.GameHud.Banner(lo > 0 ? name + "\n<size=18><color=#ddd>Quái cấp " + lo + "–" + hi + "</color></size>" : name, 2.5f);
            Map.PortalMarkers.Show(portals);
        }

        // =====================================================================
        // GĐ2 — NÂNG CẤP / RƯƠNG / GIAO DỊCH
        // =====================================================================

        /// <summary>
        /// UPGRADE_OPEN: int npcId, byte maxLv, [byte rate, int stoneId, short stoneQty, int yen] x maxLv,
        ///               int protectItemId, byte n, [short statPercent] x n
        /// </summary>
        private void OnUpgradeOpen(byte[] data)
        {
            var r = new MessageReader(data);
            var t = new UpgradeTable { npcId = r.ReadInt(), maxLv = r.ReadByte() };
            t.rate = new int[t.maxLv + 1]; t.stoneId = new int[t.maxLv + 1]; t.stoneQty = new int[t.maxLv + 1]; t.yen = new int[t.maxLv + 1];
            for (int lv = 1; lv <= t.maxLv; lv++)
            {
                t.rate[lv] = r.ReadByte(); t.stoneId[lv] = r.ReadInt(); t.stoneQty[lv] = r.ReadShort(); t.yen[lv] = r.ReadInt();
            }
            t.protectItemId = r.ReadInt();
            int n = r.ReadByte();
            t.statPercent = new int[n];
            for (int i = 0; i < n; i++) t.statPercent[i] = r.ReadShort();
            r.Cleanup();
            GameData.Upgrade = t;
            GameData.Notify(DataKind.Upgrade);
            GameWindow.Get<UpgradeWindow>().ShowFor(t.npcId);
        }

        /// <summary>UPGRADE_RESULT: byte result(0 thành công/1 trượt giữ cấp/2 trượt tụt cấp/3 lỗi), byte level, UTF msg</summary>
        private void OnUpgradeResult(byte[] data)
        {
            var r = new MessageReader(data);
            int result = r.ReadByte(), level = r.ReadByte();
            string msg = r.ReadUTF();
            r.Cleanup();
            GameWindow.Get<UpgradeWindow>().ShowResult(result, level, msg);
        }

        /// <summary>STORAGE_DATA: int npcId, short capacity, short n, [ô đồ] x n</summary>
        private void OnStorage(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.StorageNpc = r.ReadInt();
            GameData.StorageCapacity = r.ReadShort();
            var list = BagSlot.ReadList(r);
            r.Cleanup();
            GameData.Storage.Clear();
            GameData.Storage.AddRange(list);
            GameData.Notify(DataKind.Storage);
            var w = GameWindow.Get<StorageWindow>();
            if (!w.IsOpen) w.OpenWithBag();
        }

        private void OnTradeInvite(byte[] data)
        {
            var r = new MessageReader(data);
            int from = r.ReadInt(); string name = r.ReadUTF();
            r.Cleanup();
            ConfirmWindow.Ask($"<b>{name}</b> muốn giao dịch với bạn.", "Đồng ý", () => GameActions.TradeAccept(from), "Từ chối", null);
        }

        /// <summary>TRADE_UPDATE: int otherId, UTF otherName, [mình: byte locked, byte confirmed, int yen, ô đồ], [bên kia: như trên]</summary>
        private void OnTradeUpdate(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.TradeWith = r.ReadInt();
            GameData.TradeWithName = r.ReadUTF();
            ReadSide(r, GameData.TradeMine);
            ReadSide(r, GameData.TradeTheirs);
            r.Cleanup();
            GameData.Notify(DataKind.Trade);
            var w = GameWindow.Get<TradeWindow>();
            if (!w.IsOpen) w.OpenWithBag();
        }

        private static void ReadSide(MessageReader r, TradeSide s)
        {
            s.locked = r.ReadByte() != 0; s.confirmed = r.ReadByte() != 0; s.yen = r.ReadInt();
            s.items.Clear(); s.items.AddRange(BagSlot.ReadList(r));
        }

        /// <summary>TRADE_CLOSE: byte result(0 huỷ/1 xong), UTF lý do</summary>
        private void OnTradeClose(byte[] data)
        {
            var r = new MessageReader(data);
            int result = r.ReadByte(); string reason = r.ReadUTF();
            r.Cleanup();
            GameData.TradeWith = 0;
            GameData.Notify(DataKind.Trade);
            GameWindow.Get<TradeWindow>().Hide();
            ChatBox.AddSystem(result == 1 ? "Giao dịch thành công!" : "Giao dịch đã huỷ" + (string.IsNullOrEmpty(reason) ? "" : ": " + reason));
        }

        // =====================================================================
        // GĐ3 — PvP / NHÓM / BẠN BÈ / THƯ / GIA TỘC / XẾP HẠNG
        // =====================================================================

        /// <summary>PLAYER_PVP_INFO: int playerId, byte pkMode, short pkPoint, UTF guildName</summary>
        private void OnPvpInfo(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            var info = new PvpInfo { pkMode = r.ReadByte(), pkPoint = r.ReadShort(), guild = r.ReadUTF() };
            r.Cleanup();
            GameData.Pvp[id] = info;
            if (id == LocalPlayerState.Id)
            {
                GameData.MyPkMode = info.pkMode;
                GameData.Me.pkPoint = info.pkPoint;
            }
            GameData.Notify(DataKind.Pvp);
        }

        private void OnDuelInvite(byte[] data)
        {
            var r = new MessageReader(data);
            int from = r.ReadInt(); string name = r.ReadUTF();
            r.Cleanup();
            ConfirmWindow.Ask($"<b>{name}</b> mời bạn TỈ THÍ (không mất gì khi thua).", "Nhận lời", () => GameActions.DuelAccept(from), "Từ chối", null);
        }

        /// <summary>DUEL_STATE: byte state(0 kết thúc/1 đếm ngược/2 đang đấu), int opponentId, UTF msg</summary>
        private void OnDuelState(byte[] data)
        {
            var r = new MessageReader(data);
            int state = r.ReadByte(); r.ReadInt(); string msg = r.ReadUTF();
            r.Cleanup();
            // Tin nhắn hệ thống đi kèm đã vào khung chat; ở đây hiện banner to giữa màn hình
            UI.GameHud.Banner(msg, state == 0 ? 3f : 2f);
        }

        private void OnPartyInvite(byte[] data)
        {
            var r = new MessageReader(data);
            int from = r.ReadInt(); string name = r.ReadUTF();
            r.Cleanup();
            ConfirmWindow.Ask($"<b>{name}</b> mời bạn vào nhóm.", "Vào nhóm", () => GameActions.PartyAccept(from), "Từ chối", null);
        }

        /// <summary>PARTY_INFO: short n, [int id, UTF name, int hp, int maxHp, short level] x n, int leaderId (n = 0 → không còn nhóm)</summary>
        private void OnPartyInfo(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Party.Clear();
            for (int i = 0; i < n; i++)
            {
                var m = new PartyMember { id = r.ReadInt(), name = r.ReadUTF(), hp = r.ReadInt(), maxHp = r.ReadInt(), level = r.ReadShort() };
                if (m.id > 0) GameData.Party.Add(m);
            }
            GameData.PartyLeader = r.Available() >= 4 ? r.ReadInt() : 0;
            r.Cleanup();
            GameData.Notify(DataKind.Party);
        }

        /// <summary>FRIEND_LIST: short n, [int id, UTF name, short level, byte online]</summary>
        private void OnFriendList(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Friends.Clear();
            for (int i = 0; i < n; i++)
                GameData.Friends.Add(new FriendInfo { id = r.ReadInt(), name = r.ReadUTF(), level = r.ReadShort(), online = r.ReadByte() != 0 });
            r.Cleanup();
            GameData.Notify(DataKind.Friends);
        }

        private void OnFriendInvite(byte[] data)
        {
            var r = new MessageReader(data);
            int from = r.ReadInt(); string name = r.ReadUTF();
            r.Cleanup();
            ConfirmWindow.Ask($"<b>{name}</b> muốn kết bạn với bạn.", "Kết bạn", () => GameActions.FriendAccept(from), "Từ chối", null);
        }

        /// <summary>MAIL_LIST: short n, [long id, UTF from, UTF title, byte read, byte claimed, byte hasAttach, long createdMs]</summary>
        private void OnMailList(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Mails.Clear();
            int unread = 0;
            for (int i = 0; i < n; i++)
            {
                var m = new MailHeader
                {
                    id = r.ReadLong(), from = r.ReadUTF(), title = r.ReadUTF(), read = r.ReadByte() != 0,
                    claimed = r.ReadByte() != 0, hasAttach = r.ReadByte() != 0, createdMs = r.ReadLong()
                };
                if (!m.read) unread++;
                GameData.Mails.Add(m);
            }
            r.Cleanup();
            GameData.MailUnread = unread;
            GameData.Notify(DataKind.Mails);
        }

        /// <summary>MAIL_CONTENT: long id, UTF from, UTF title, UTF content, int yen, [ô đồ], byte claimed, int xu, int luong (xu/lượng: quà từ trang quản trị)</summary>
        private void OnMailContent(byte[] data)
        {
            var r = new MessageReader(data);
            var m = new MailBody { id = r.ReadLong(), from = r.ReadUTF(), title = r.ReadUTF(), content = r.ReadUTF(), yen = r.ReadInt() };
            m.items.AddRange(BagSlot.ReadList(r));
            m.claimed = r.ReadByte() != 0;
            if (r.Available() >= 8) { m.xu = r.ReadInt(); m.luong = r.ReadInt(); } // server cũ không gửi
            r.Cleanup();
            GameData.OpenMail = m;
            GameData.Notify(DataKind.Mails);
        }

        /// <summary>MAIL_NOTIFY: short unread</summary>
        private void OnMailNotify(byte[] data)
        {
            var r = new MessageReader(data);
            int unread = r.ReadShort();
            r.Cleanup();
            if (unread > GameData.MailUnread) ChatBox.AddSystem($"Bạn có {unread} thư chưa đọc (bấm Thư để xem).");
            GameData.MailUnread = unread;
            GameData.Notify(DataKind.Mails);
            if (GameWindow.Get<MailWindow>().IsOpen) GameActions.MailList();
        }

        /// <summary>
        /// GUILD_INFO: int id (0 = chưa có gia tộc) rồi UTF name, short level, long fund, UTF notice, int leaderId,
        ///             byte myRank, short capacity, short n, [int id, UTF name, short level, byte rank, byte online] x n
        /// </summary>
        private void OnGuildInfo(byte[] data)
        {
            var r = new MessageReader(data);
            var g = GameData.Guild;
            g.id = r.ReadInt();
            g.members.Clear();
            if (g.id > 0)
            {
                g.name = r.ReadUTF(); g.level = r.ReadShort(); g.fund = r.ReadLong(); g.notice = r.ReadUTF();
                g.leaderId = r.ReadInt(); g.myRank = r.ReadByte(); g.capacity = r.ReadShort();
                int n = r.ReadShort();
                for (int i = 0; i < n; i++)
                    g.members.Add(new GuildMember { id = r.ReadInt(), name = r.ReadUTF(), level = r.ReadShort(), rank = r.ReadByte(), online = r.ReadByte() != 0 });
            }
            else { g.name = ""; g.notice = ""; }
            r.Cleanup();
            GameData.Notify(DataKind.Guild);
        }

        private void OnGuildInvite(byte[] data)
        {
            var r = new MessageReader(data);
            int gid = r.ReadInt(); string gname = r.ReadUTF(); string from = r.ReadUTF();
            r.Cleanup();
            ConfirmWindow.Ask($"<b>{from}</b> mời bạn vào gia tộc <b>{gname}</b>.", "Gia nhập", () => GameActions.GuildAccept(gid), "Từ chối", null);
        }

        /// <summary>TOP_LIST: byte type, short n, [short rank, UTF name, long value, UTF extra]</summary>
        private void OnTopList(byte[] data)
        {
            var r = new MessageReader(data);
            int type = r.ReadByte();
            int n = r.ReadShort();
            var rows = new List<TopRow>(n);
            for (int i = 0; i < n; i++) rows.Add(new TopRow { rank = r.ReadShort(), name = r.ReadUTF(), value = r.ReadLong(), extra = r.ReadUTF() });
            r.Cleanup();
            GameData.Tops[type] = rows;
            GameData.Notify(DataKind.Top);
        }

        // =====================================================================
        // GĐ4 — HIỆU ỨNG / PHÓ BẢN / SỰ KIỆN
        // =====================================================================

        private static readonly string[] EffectNames = { "", "Choáng", "Chậm", "Bỏng", "Tăng sức mạnh" };
        private static readonly Color[] EffectColors = { Color.white, new Color(1f, 0.9f, 0.2f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.45f, 0.1f), new Color(0.5f, 1f, 0.5f) };

        /// <summary>EFFECT: byte targetType(0 quái/1 người), int targetId, byte effect(1 choáng/2 chậm/3 bỏng/4 tăng sức mạnh — GĐ8), int durationMs</summary>
        private void OnEffect(byte[] data)
        {
            var r = new MessageReader(data);
            int type = r.ReadByte(), id = r.ReadInt(), effect = r.ReadByte(), ms = r.ReadInt();
            r.Cleanup();
            if (effect < 1 || effect >= EffectNames.Length) return;

            Transform t = null;
            if (type == 1)
            {
                if (id == LocalPlayerState.Id)
                {
                    t = NetworkPlayerManager.Instance != null && NetworkPlayerManager.Instance.localPlayer != null
                        ? NetworkPlayerManager.Instance.localPlayer.transform : null;
                    if (effect == 1) LocalPlayerState.StunnedUntil = Time.time + ms / 1000f;
                    if (effect == 2) LocalPlayerState.SlowedUntil = Time.time + ms / 1000f;
                    if (effect == 4) LocalPlayerState.BuffedUntil = Time.time + ms / 1000f;
                }
                else
                {
                    var rp = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.GetRemotePlayer(id) : null;
                    if (rp != null) t = rp.transform;
                }
            }
            else
            {
                var mob = NetworkMobManager.Instance != null ? NetworkMobManager.Instance.GetMob(id) : null;
                if (mob != null) t = mob.transform;
            }
            // [CẦN ĐIỀN khi có art] thay chữ nổi bằng hiệu ứng hình (sao quay trên đầu = choáng, băng = chậm, lửa = bỏng)
            if (t != null) DamagePopup.Show(t.position + Vector3.up * 1.4f, EffectNames[effect], EffectColors[effect], 4f);
        }

        /// <summary>DUNGEON_STATE: byte state(2 đang chơi/3 thắng/4 thua/0 rời), int secondsLeft, short mobsLeft, UTF name</summary>
        private void OnDungeonState(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.DungeonState = r.ReadByte();
            int secs = r.ReadInt();
            GameData.DungeonMobsLeft = r.ReadShort();
            GameData.DungeonName = r.ReadUTF();
            r.Cleanup();
            GameData.DungeonEndTime = Time.time + secs;
            GameData.Notify(DataKind.Event);
            if (GameData.DungeonState == 3) UI.GameHud.Banner($"CHINH PHỤC {GameData.DungeonName.ToUpper()}!", 4f);
            if (GameData.DungeonState == 4) UI.GameHud.Banner("Phó bản thất bại — hết giờ", 4f);
        }

        /// <summary>EVENT_STATE: byte eventType(1 boss thế giới/2 lôi đài), byte state(0 kết thúc/1 báo trước/2 đang diễn ra), int secs, UTF text</summary>
        private void OnEventState(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.EventType = r.ReadByte();
            GameData.EventState = r.ReadByte();
            int secs = r.ReadInt();
            GameData.EventText = r.ReadUTF();
            r.Cleanup();
            GameData.EventEndTime = Time.time + secs;
            if (GameData.EventType == 2 && GameData.EventState != 2) GameData.ArenaScore.Clear();
            GameData.Notify(DataKind.Event);
            UI.GameHud.Banner(GameData.EventText, 4f);
        }

        /// <summary>
        /// SERVER_NOTICE: byte type, int secs, UTF text — GM gửi từ trang quản trị (server: MaintenanceService).
        ///   1 thông báo → chữ lớn giữa màn hình; 2 đếm ngược bảo trì → thêm đồng hồ ở khung sự kiện;
        ///   3 huỷ bảo trì → tắt đồng hồ; 4 bị đưa ra vì bảo trì → nhớ lý do, server ngắt ngay sau đó.
        /// (Dòng chat "Hệ thống" server gửi riêng bằng gói CHAT.)
        /// </summary>
        private void OnServerNotice(byte[] data)
        {
            var r = new MessageReader(data);
            int type = r.ReadByte();
            int secs = r.ReadInt();
            string text = r.ReadUTF();
            r.Cleanup();
            switch (type)
            {
                case 2:
                    GameData.MaintEndTime = Time.time + secs;
                    GameData.Notify(DataKind.Event);
                    break;
                case 3:
                    GameData.MaintEndTime = 0;
                    GameData.Notify(DataKind.Event);
                    break;
                case 4:
                    GameData.KickReason = text;
                    return; // không hiện banner — popup lúc ngắt kết nối sẽ hiện lý do
            }
            UI.GameHud.Banner(text, type == 3 ? 3f : 6f);
        }

        /// <summary>ZONE_LIST: byte currentZone (255 = khu riêng), byte n, [byte zoneId, byte players, byte max] x n</summary>
        private void OnZoneList(byte[] data)
        {
            var r = new MessageReader(data);
            int current = r.ReadByte();
            int n = r.ReadByte();
            var zones = new List<int[]>(n);
            for (int i = 0; i < n; i++) zones.Add(new[] { (int)r.ReadByte(), r.ReadByte(), r.ReadByte() });
            r.Cleanup();
            LocalPlayerState.ZoneId = current;
            GameWindow.Get<ZoneWindow>().SetData(current, zones);
        }

        /// <summary>ARENA_SCORE: short n, [UTF name, short kills] — top 10 Lôi đài</summary>
        private void OnArenaScore(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.ArenaScore.Clear();
            for (int i = 0; i < n; i++) GameData.ArenaScore.Add(new KeyValuePair<string, int>(r.ReadUTF(), r.ReadShort()));
            r.Cleanup();
            GameData.Notify(DataKind.Event);
        }
    }
}
