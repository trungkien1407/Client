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
    /// GÓI XÃ HỘI: PvP / tỉ thí, nhóm, bạn bè, thư, gia tộc, xếp hạng → ghi GameData.
    /// Payload: docs/PROTOCOL.md (repo server). Lời mời (nhóm / bạn / tỉ thí / gia tộc) hiện ConfirmWindow, KHÔNG tự đồng ý.
    /// </summary>
    public class SocialNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
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
        }

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
            var inv = Packets.ReadInvite(data);
            ConfirmWindow.Ask($"<b>{inv.name}</b> mời bạn TỈ THÍ (không mất gì khi thua).", "Nhận lời", () => GameActions.DuelAccept(inv.fromId), "Từ chối", null);
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
            var inv = Packets.ReadInvite(data);
            ConfirmWindow.Ask($"<b>{inv.name}</b> mời bạn vào nhóm.", "Vào nhóm", () => GameActions.PartyAccept(inv.fromId), "Từ chối", null);
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
            var inv = Packets.ReadInvite(data);
            ConfirmWindow.Ask($"<b>{inv.name}</b> muốn kết bạn với bạn.", "Kết bạn", () => GameActions.FriendAccept(inv.fromId), "Từ chối", null);
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
    }
}
