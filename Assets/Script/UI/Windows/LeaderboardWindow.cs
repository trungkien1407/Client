using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// BẢNG XẾP HẠNG (server tính lại mỗi 5 phút): Cấp độ · Tài phú (yên) · Gia tộc · Cừu sát (điểm PK).
    /// Bấm thẻ → gửi TOP_REQUEST(type) → TOP_LIST về thì vẽ.
    /// </summary>
    public class LeaderboardWindow : GameWindow
    {
        private static readonly string[] TabNames = { "Cấp độ", "Tài phú", "Gia tộc", "Cừu sát" };
        private static readonly string[] ValueNames = { "Cấp", "Yên", "Cấp tộc", "Điểm PK" };
        private readonly Button[] _tabs = new Button[4];
        private RectTransform _list;
        private int _type;

        protected override void Build()
        {
            Title = "Bảng xếp hạng";
            Size = new Vector2(560, 540);
            var body = CreateBody();
            for (int i = 0; i < 4; i++)
            {
                int t = i;
                _tabs[i] = UIKit.Button("Tab" + i, body, TabNames[i], () => { _type = t; GameActions.Top(t); Refresh(); }, 16);
                ((RectTransform)_tabs[i].transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 134, 0), new Vector2(128, 36), new Vector2(0, 1));
            }
            _list = UIKit.ScrollList("List", body, 3);
            ((RectTransform)_list.parent).Fill(0, 0, 44, 0);
            GameData.OnChanged += k => { if (k == DataKind.Top) RefreshIfOpen(); };
        }

        private void OnEnable() { if (Assets.Script.Player.LocalPlayerState.Id >= 0) GameActions.Top(_type); }

        protected override void Refresh()
        {
            for (int i = 0; i < 4; i++) _tabs[i].image.color = i == _type ? UIKit.ButtonHot : UIKit.ButtonColor;
            UIKit.Clear(_list);
            if (!GameData.Tops.TryGetValue(_type, out var rows) || rows.Count == 0)
            {
                UIKit.Text("Empty", _list, "Chưa có dữ liệu (bảng cập nhật 5 phút/lần).", 15).Height(30);
                return;
            }
            foreach (var r in rows)
            {
                string medal = r.rank == 1 ? "#ffd84a" : r.rank == 2 ? "#d8e0ea" : r.rank == 3 ? "#e09a5a" : "#ffffff";
                var row = UIKit.Panel("Row", _list, UIKit.SlotColor).Height(40);
                var t = UIKit.Text("T", row.transform,
                    $"<color={medal}><b>#{r.rank}</b></color>   <b>{r.name}</b>  <color=#9aa>{r.extra}</color>", 16);
                t.rectTransform.Fill(10, 150, 0, 0);
                var v = UIKit.Text("V", row.transform, $"{ValueNames[_type]}: <color=#fd5>{r.value:N0}</color>", 15, TextAlignmentOptions.MidlineRight);
                v.rectTransform.Fill(300, 10, 0, 0);
            }
        }
    }
}
