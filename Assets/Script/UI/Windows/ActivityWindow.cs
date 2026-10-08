using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// HOẠT ĐỘNG HẰNG NGÀY (phím H) — GĐ9.
    ///   Trên:  thanh điểm 0..100 + 5 nút mốc (đủ điểm thì sáng, bấm để nhận rương).
    ///   Giữa:  danh sách việc nên làm hôm nay + tiến độ + điểm.
    ///   Dưới:  lịch sự kiện (boss thế giới, Lôi đài, lượt phó bản...) — chữ do server gửi.
    /// Mọi con số đều do server tính (ACTIVITY_INFO); client chỉ hiển thị và gửi "nhận mốc i".
    /// </summary>
    public class ActivityWindow : GameWindow
    {
        private Image _bar;
        private TextMeshProUGUI _points;
        private RectTransform _miles, _list;
        private TMP_InputField _code;
        private TextMeshProUGUI _codeMsg;

        protected override void Build()
        {
            Title = "Hoạt động hằng ngày";
            HotKey = Key.H;
            Size = new Vector2(620, 560);
            var body = CreateBody();

            _points = UIKit.Text("Points", body, "", 18, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f));
            _points.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 26), new Vector2(0.5f, 1));
            _bar = UIKit.Bar("Bar", body, new Color(0.95f, 0.6f, 0.15f));
            ((RectTransform)_bar.transform.parent).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30), new Vector2(0, 14), new Vector2(0.5f, 1));

            _miles = UIKit.Rect("Miles", body);
            _miles.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 58), new Vector2(0.5f, 1));
            var h = _miles.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childForceExpandWidth = true; h.childControlWidth = true; h.childControlHeight = true;

            _list = UIKit.ScrollList("List", body, 4);
            ((RectTransform)_list.parent).Fill(0, 0, 116, 48);

            // Giftcode (dưới cùng): ô nhập + nút Nhận; kết quả hiện bên phải
            _code = UIKit.Input("Code", body, "Nhập giftcode...", 16, TMP_InputField.ContentType.Standard, 32);
            ((RectTransform)_code.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 4), new Vector2(230, 38), Vector2.zero);
            var use = UIKit.Button("UseCode", body, "Nhận quà", () => { GameActions.GiftcodeUse(_code.text); _code.text = ""; }, 15);
            ((RectTransform)use.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(236, 4), new Vector2(110, 38), Vector2.zero);
            _codeMsg = UIKit.Text("CodeMsg", body, "", 13, TextAlignmentOptions.MidlineLeft);
            _codeMsg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(352, 4), new Vector2(-352, 38), Vector2.zero);

            GameData.OnChanged += k => { if (k == DataKind.Activity || k == DataKind.Templates) RefreshIfOpen(); };
        }

        /// <summary>Mở cửa sổ = xin số liệu mới (lịch boss "đang xuất hiện" đổi theo thời gian).</summary>
        protected override void Refresh()
        {
            if (!_requested) { _requested = true; GameActions.ActivityRequest(); }
            Draw();
        }
        private bool _requested;
        private void OnDisable() => _requested = false;

        private void Draw()
        {
            int max = GameData.Milestones.Count > 0 ? GameData.Milestones[GameData.Milestones.Count - 1].need : 100;
            _points.text = $"Điểm hôm nay: <b>{GameData.ActivityPoints}</b> / {max}   <size=14><color=#9aa>(reset 0h mỗi ngày)</color></size>";
            _bar.fillAmount = max > 0 ? Mathf.Clamp01(GameData.ActivityPoints / (float)max) : 0;

            UIKit.Clear(_miles);
            for (int i = 0; i < GameData.Milestones.Count; i++)
            {
                int idx = i;
                var m = GameData.Milestones[i];
                bool claimed = GameData.MilestoneClaimed(i), ready = GameData.ActivityPoints >= m.need;
                string label = $"<b>{m.need} điểm</b>\n<size=12>{(claimed ? "đã nhận" : ready ? "BẤM NHẬN" : "chưa đủ")}</size>";
                var b = UIKit.Button("M" + i, _miles, label, () => GameActions.ActivityClaim(idx), 14);
                b.image.color = claimed ? new Color(0.2f, 0.35f, 0.2f) : ready ? UIKit.ButtonHot : UIKit.ButtonColor;
                b.interactable = ready && !claimed;
                // Di chuột / giữ ngón tay để xem quà: hiện luôn dưới dạng chú thích ở danh sách bên dưới
            }

            _codeMsg.text = GameData.GiftcodeMessage;
            UIKit.Clear(_list);

            // ---- Quà online (GĐ9 phần 4) ----
            if (GameData.OnlineGifts.Count > 0)
            {
                int mins = GameData.OnlineMinutesNow;
                UIKit.Text("HO", _list, $"<b><color=#fd5>Quà online hôm nay</color></b>  <size=13><color=#9aa>đã online {mins} phút</color></size>", 16).Height(26);
                var row = UIKit.Rect("OnlineRow", _list).Height(54);
                var h2 = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h2.spacing = 6; h2.childForceExpandWidth = true; h2.childControlWidth = true; h2.childControlHeight = true;
                for (int i = 0; i < GameData.OnlineGifts.Count; i++)
                {
                    int idx = i;
                    var g = GameData.OnlineGifts[i];
                    bool got = GameData.OnlineClaimedAt(i), ready = mins >= g.need;
                    var b = UIKit.Button("O" + i, row, $"<b>{g.need} phút</b>\n<size=11>{(got ? "đã nhận" : g.reward)}</size>", () => GameActions.OnlineClaim(idx), 13);
                    b.image.color = got ? new Color(0.2f, 0.35f, 0.2f) : ready ? UIKit.ButtonHot : UIKit.ButtonColor;
                    b.interactable = ready && !got;
                }
            }

            UIKit.Text("H1", _list, "<b><color=#fd5>Việc nên làm hôm nay</color></b>", 16).Height(24);
            foreach (var a in GameData.Activities)
            {
                bool done = a.pts >= a.maxPts;
                string prog = a.target > 1 ? $"{a.progress}/{a.target}" : (a.progress >= a.target ? "xong" : "chưa");
                string line = $"{(done ? "<color=#7f7>[Xong]</color>" : "•")} {a.name}   <color=#ccc>{prog}</color>   <color=#fd5>+{a.pts}/{a.maxPts} điểm</color>";
                UIKit.Text("A", _list, line, 15).Height(24);
            }

            UIKit.Text("H2", _list, "\n<b><color=#fd5>Quà các mốc</color></b>", 16).Height(40);
            for (int i = 0; i < GameData.Milestones.Count; i++)
                UIKit.Text("R", _list, $"{GameData.Milestones[i].need} điểm: <color=#ddd>{GameData.Milestones[i].reward}</color>", 14).Height(22);

            UIKit.Text("H3", _list, "\n<b><color=#fd5>Lịch sự kiện</color></b>", 16).Height(40);
            foreach (var l in GameData.EventSchedule) UIKit.Text("S", _list, "• " + l, 14).Height(40); // dòng dài → chừa 2 hàng
            if (GameData.EventSchedule.Count == 0) UIKit.Text("S", _list, "<color=#888>(đang tải...)</color>", 14).Height(24);
        }
    }
}
