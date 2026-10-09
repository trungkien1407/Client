using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// TƯƠNG TÁC VỚI NGƯỜI CHƠI KHÁC: chọn 1 người chơi (bấm vào nhân vật / Tab) → nút [Tương tác] trên HUD → cửa sổ này.
    /// Mời nhóm · Giao dịch · Kết bạn · Tỉ thí · Mời vào gia tộc · Gửi thư. Server kiểm tra hết (khoảng cách, cấp, quyền...).
    /// </summary>
    public class PlayerActionWindow : GameWindow
    {
        private static readonly string[] Labels = { "Mời vào nhóm", "Giao dịch", "Kết bạn", "Tỉ thí", "Mời vào gia tộc", "Gửi thư" };

        [SerializeField] private TextMeshProUGUI _info;
        [SerializeField] private Button[] _actions = new Button[6];
        private int _id;
        private string _name;

        protected override void Configure()
        {
            Title = "Người chơi";
            Size = new Vector2(360, 420);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _info = UIKit.Text("Info", body, "", 16, TextAlignmentOptions.Center);
            _info.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 50), new Vector2(0.5f, 1));
            for (int i = 0; i < Labels.Length; i++)
            {
                _actions[i] = UIKit.Button("A" + i, body, Labels[i], null, 17);
                ((RectTransform)_actions[i].transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56 - i * 50), new Vector2(260, 44), new Vector2(0.5f, 1));
            }
        }

        protected override void Bind()
        {
            // Cùng thứ tự với Labels
            System.Action[] acts =
            {
                () => GameActions.PartyInvite(_id),
                () => GameActions.TradeRequest(_id),
                () => GameActions.FriendRequest(_name),
                () => GameActions.DuelRequest(_id),
                () => GameActions.GuildInvite(_id),
                () => Open<MailWindow>().Compose(_name),
            };
            for (int i = 0; i < acts.Length; i++)
            {
                var a = acts[i];
                _actions[i].onClick.AddListener(() => { a(); Hide(); });
            }
        }

        public void ShowFor(int playerId, string name)
        {
            _id = playerId;
            _name = name;
            Show();
        }

        protected override void Refresh()
        {
            SetTitle(_name);
            GameData.Pvp.TryGetValue(_id, out var pvp);
            string guild = pvp != null && !string.IsNullOrEmpty(pvp.guild) ? $"Gia tộc: {pvp.guild}" : "Chưa có gia tộc";
            string pk = pvp == null ? "" : pvp.pkMode == 1 ? "  <color=#ffa030>[Đồ sát]</color>" : "";
            if (pvp != null && pvp.pkPoint > 0) pk += $"  <color=#f55>PK {pvp.pkPoint}</color>";
            _info.text = $"<b>{_name}</b>\n<size=14><color=#9aa>{guild}</color>{pk}</size>";
        }
    }
}
