using Assets.Script.Data;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// XÃ HỘI (phím O) — 3 thẻ:
    ///   Nhóm    : thành viên + máu (PARTY_INFO), rời nhóm, đội trưởng đá người. Mời: chọn người chơi → [Tương tác].
    ///   Bạn bè  : nhập tên → kết bạn; danh sách (online/offline), gửi thư, xoá.
    ///   Gia tộc : chưa có → lập gia tộc; có → thông tin, quỹ, thông báo, thành viên, góp yên, phong chức, đá, rời.
    /// Mọi quyền (ai được đá, phong chức...) do server kiểm; nút chỉ ẩn/hiện cho dễ nhìn.
    /// </summary>
    public class SocialWindow : GameWindow
    {
        private enum Tab { Party, Friends, Guild }
        private Tab _tab = Tab.Party;
        private readonly Button[] _tabs = new Button[3];

        private RectTransform _partyHead, _friendHead, _guildNone, _guildHead, _list;
        private TMP_InputField _friendName, _guildName, _donate, _notice;
        private TextMeshProUGUI _partyInfo, _guildInfo;
        private Button _btnNotice;

        protected override void Build()
        {
            Title = "Xã hội";
            HotKey = Key.O;
            Size = new Vector2(620, 540);
            var body = CreateBody();

            string[] names = { "Nhóm", "Bạn bè", "Gia tộc" };
            for (int i = 0; i < 3; i++)
            {
                var t = (Tab)i;
                _tabs[i] = UIKit.Button("Tab" + i, body, names[i], () => SwitchTab(t), 17);
                ((RectTransform)_tabs[i].transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 130, 0), new Vector2(124, 36), new Vector2(0, 1));
            }

            // ---- phần đầu từng thẻ (giữ cố định để ô nhập không mất chữ khi danh sách vẽ lại) ----
            _partyHead = Head(body, "PartyHead", 60);
            _partyInfo = UIKit.Text("Info", _partyHead, "", 15, TextAlignmentOptions.MidlineLeft, UIKit.DimText);
            _partyInfo.rectTransform.Fill(4, 140, 0, 0);
            var leave = UIKit.Button("Leave", _partyHead, "Rời nhóm", GameActions.PartyLeave, 15);
            ((RectTransform)leave.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(130, 38), new Vector2(1, 0.5f));

            _friendHead = Head(body, "FriendHead", 60);
            _friendName = UIKit.Input("Name", _friendHead, "Tên nhân vật", 16, TMP_InputField.ContentType.Standard, 16);
            ((RectTransform)_friendName.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(260, 38), new Vector2(0, 0.5f));
            var add = UIKit.Button("Add", _friendHead, "Kết bạn", () =>
            {
                if (!string.IsNullOrWhiteSpace(_friendName.text)) { GameActions.FriendRequest(_friendName.text.Trim()); _friendName.text = ""; }
            }, 15);
            ((RectTransform)add.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(268, 0), new Vector2(110, 38), new Vector2(0, 0.5f));
            var reload = UIKit.Button("Reload", _friendHead, "Làm mới", GameActions.FriendList, 15);
            ((RectTransform)reload.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 38), new Vector2(1, 0.5f));

            _guildNone = Head(body, "GuildNone", 60);
            _guildName = UIKit.Input("GName", _guildNone, "Tên gia tộc (3–12 ký tự)", 16, TMP_InputField.ContentType.Standard, 12);
            ((RectTransform)_guildName.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(260, 38), new Vector2(0, 0.5f));
            var create = UIKit.Button("Create", _guildNone, "Lập gia tộc", () =>
            {
                if (!string.IsNullOrWhiteSpace(_guildName.text)) GameActions.GuildCreate(_guildName.text.Trim());
            }, 15);
            ((RectTransform)create.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(268, 0), new Vector2(140, 38), new Vector2(0, 0.5f));

            _guildHead = Head(body, "GuildHead", 150);
            _guildInfo = UIKit.Text("Info", _guildHead, "", 15, TextAlignmentOptions.TopLeft);
            _guildInfo.rectTransform.Fill(4, 4, 0, 48);
            _donate = UIKit.Input("Donate", _guildHead, "Số yên góp", 15, TMP_InputField.ContentType.IntegerNumber, 9);
            ((RectTransform)_donate.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 4), new Vector2(120, 36), Vector2.zero);
            var donate = UIKit.Button("DonateBtn", _guildHead, "Góp quỹ", () =>
            {
                if (int.TryParse(_donate.text, out int v) && v > 0) { GameActions.GuildDonate(v); _donate.text = ""; }
            }, 14);
            ((RectTransform)donate.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(124, 4), new Vector2(90, 36), Vector2.zero);
            _notice = UIKit.Input("Notice", _guildHead, "Thông báo mới", 15, TMP_InputField.ContentType.Standard, 100);
            ((RectTransform)_notice.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(222, 4), new Vector2(170, 36), Vector2.zero);
            _btnNotice = UIKit.Button("NoticeBtn", _guildHead, "Đăng", () =>
            {
                if (!string.IsNullOrWhiteSpace(_notice.text)) { GameActions.GuildNotice(_notice.text.Trim()); _notice.text = ""; }
            }, 14);
            ((RectTransform)_btnNotice.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(396, 4), new Vector2(70, 36), Vector2.zero);
            var gLeave = UIKit.Button("GLeave", _guildHead, "Rời tộc", () =>
                ConfirmWindow.Ask("Rời gia tộc? (Tộc trưởng rời khi còn 1 người = giải tán)", "Rời", GameActions.GuildLeave, "Ở lại"), 14);
            ((RectTransform)gLeave.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 4), new Vector2(100, 36), new Vector2(1, 0));

            _list = UIKit.ScrollList("List", body, 4);

            GameData.OnChanged += k =>
            {
                if (k == DataKind.Party || k == DataKind.Friends || k == DataKind.Guild || k == DataKind.Character) RefreshIfOpen();
            };
        }

        private static RectTransform Head(RectTransform body, string name, float h)
        {
            var rt = UIKit.Rect(name, body);
            rt.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(0, h), new Vector2(0.5f, 1));
            return rt;
        }

        private void SwitchTab(Tab t)
        {
            _tab = t;
            if (t == Tab.Friends) GameActions.FriendList();
            if (t == Tab.Guild) GameActions.GuildInfo();
            Refresh();
        }

        protected override void Refresh()
        {
            for (int i = 0; i < 3; i++) _tabs[i].image.color = (int)_tab == i ? UIKit.ButtonHot : UIKit.ButtonColor;
            bool hasGuild = GameData.Guild.Has;
            _partyHead.gameObject.SetActive(_tab == Tab.Party);
            _friendHead.gameObject.SetActive(_tab == Tab.Friends);
            _guildNone.gameObject.SetActive(_tab == Tab.Guild && !hasGuild);
            _guildHead.gameObject.SetActive(_tab == Tab.Guild && hasGuild);
            float headH = _tab == Tab.Guild && hasGuild ? 150 : 60;
            ((RectTransform)_list.parent).Fill(0, 0, 44 + headH + 6, 0);

            UIKit.Clear(_list);
            switch (_tab)
            {
                case Tab.Party: DrawParty(); break;
                case Tab.Friends: DrawFriends(); break;
                default: DrawGuild(); break;
            }
        }

        private Image Row(string text, float h = 46)
        {
            var row = UIKit.Panel("Row", _list, UIKit.SlotColor).Height(h);
            var t = UIKit.Text("T", row.transform, text, 15);
            t.rectTransform.Fill(8, 250, 2, 2);
            return row;
        }

        private static void RowBtn(Image row, string label, System.Action a, int slotFromRight)
        {
            var b = UIKit.Button(label, row.transform, label, a, 13);
            ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6 - slotFromRight * 82, 0), new Vector2(78, 32), new Vector2(1, 0.5f));
        }

        private void DrawParty()
        {
            if (GameData.Party.Count == 0)
            {
                _partyInfo.text = "Bạn chưa có nhóm. Chọn 1 người chơi → [Tương tác] → Mời nhóm.";
                return;
            }
            bool iLead = GameData.PartyLeader == LocalPlayerState.Id;
            _partyInfo.text = $"Nhóm {GameData.Party.Count} người — EXP quái chia đều cho người ở gần." + (iLead ? " <color=#fd5>(Bạn là đội trưởng)</color>" : "");
            foreach (var m in GameData.Party)
            {
                int id = m.id;
                string lead = m.id == GameData.PartyLeader ? " <color=#fd5>(Đội trưởng)</color>" : "";
                var row = Row($"<b>{m.name}</b>{lead}  <color=#9aa>Lv {m.level}</color>\n<color=#f66>HP {m.hp:N0}/{m.maxHp:N0}</color>");
                if (iLead && id != LocalPlayerState.Id) RowBtn(row, "Đá", () => GameActions.PartyKick(id), 0);
            }
        }

        private void DrawFriends()
        {
            if (GameData.Friends.Count == 0) { UIKit.Text("Empty", _list, "Chưa có bạn bè.", 15).Height(30); return; }
            foreach (var f in GameData.Friends)
            {
                int id = f.id; string name = f.name;
                var row = Row($"<b>{f.name}</b>  <color=#9aa>Lv {f.level}</color>  " + (f.online ? "<color=#6f6>online</color>" : "<color=#888>offline</color>"));
                RowBtn(row, "Xoá", () => ConfirmWindow.Ask($"Xoá {name} khỏi danh sách bạn?", "Xoá", () => GameActions.FriendRemove(id)), 0);
                RowBtn(row, "Gửi thư", () => Open<MailWindow>().Compose(name), 1);
            }
        }

        private void DrawGuild()
        {
            var g = GameData.Guild;
            if (!g.Has) { UIKit.Text("Hint", _list, "Lập gia tộc tốn phí (server báo khi bấm). Hoặc nhờ tộc trưởng/trưởng lão mời bạn.", 15).Height(60); return; }
            _guildInfo.text = $"<size=20><b>{g.name}</b></size>  <color=#9aa>Cấp {g.level} · {g.members.Count}/{g.capacity} người</color>\n" +
                              $"Quỹ tộc: <color=#fd5>{g.fund:N0} yên</color>   Chức của bạn: <color=#7cf>{GuildInfo.RankName(g.myRank)}</color>\n" +
                              $"<color=#ddd>Thông báo: {(string.IsNullOrEmpty(g.notice) ? "(chưa có)" : g.notice)}</color>\n<color=#888>Chat gia tộc: gõ \"/g nội dung\"</color>";
            _notice.gameObject.SetActive(g.myRank >= 1);
            _btnNotice.gameObject.SetActive(g.myRank >= 1);

            foreach (var m in g.members)
            {
                int id = m.id; string name = m.name;
                var row = Row($"<b>{m.name}</b>  <color=#7cf>{GuildInfo.RankName(m.rank)}</color>  <color=#9aa>Lv {m.level}</color>  " +
                              (m.online ? "<color=#6f6>online</color>" : "<color=#888>offline</color>"));
                if (id == LocalPlayerState.Id) continue;
                int slot = 0;
                if (g.myRank > m.rank) RowBtn(row, "Đá", () => ConfirmWindow.Ask($"Đá {name} khỏi gia tộc?", "Đá", () => GameActions.GuildKick(id)), slot++);
                if (g.myRank == 2)
                {
                    if (m.rank == 0) RowBtn(row, "Trưởng lão", () => GameActions.GuildPromote(id, 1), slot++);
                    else RowBtn(row, "Hạ chức", () => GameActions.GuildPromote(id, 0), slot++);
                    RowBtn(row, "Nhường", () => ConfirmWindow.Ask($"Nhường chức tộc trưởng cho {name}?", "Nhường", () => GameActions.GuildPromote(id, 2)), slot);
                }
            }
        }
    }
}
