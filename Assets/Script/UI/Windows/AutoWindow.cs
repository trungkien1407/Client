using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// TỰ ĐỘNG (phím U, nút "Tự động" trong Cài đặt): tuỳ chọn của AutoPlay + nút bật / tắt (phím Z, nút Auto trên HUD).
    ///   - Chiêu ĐÁNH: auto luôn dùng chiêu đang chọn trên thanh phím (không có tuỳ chọn).
    ///   - Chiêu BUFF: danh sách chiêu hỗ trợ của hệ (icon + tên), bấm Bật / Tắt từng chiêu → hồi xong là auto dùng.
    /// Dòng trạng thái cho biết có "Tự động luyện tập" còn hạn (dịch chuyển tới quái) hay không (chạy bộ).
    /// Tuỳ chọn lưu ở AutoSettings (PlayerPrefs trên máy).
    /// </summary>
    public class AutoWindow : GameWindow
    {
        private const int Rows = 4;   // 0 tìm quái · 1 nhặt đồ · 2 bình máu · 3 bình chakra
        private const float RowH = 56, BuffH = 50, ListTop = -72 - Rows * RowH - 6;
        private static readonly string[] Labels = { "Tự tìm quái khắp map", "Tự nhặt đồ", "Uống bình máu khi HP dưới", "Uống bình chakra khi MP dưới" };
        private static readonly string[] Hints =
        {
            "tắt = chỉ đánh quái quanh chỗ đứng (5 ô)", "đồ rơi trong 6 ô", "chạy cả khi tắt tự động", "chạy cả khi tắt tự động",
        };

        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button[] _toggle = new Button[Rows];
        [SerializeField] private TextMeshProUGUI _hpPct, _mpPct;
        [SerializeField] private Button _hpMinus, _hpPlus, _mpMinus, _mpPlus, _onOff;
        [SerializeField] private RectTransform _buffList;   // hàng chiêu buff dựng lại mỗi lần vẽ (theo chiêu đã học)

        protected override void Configure()
        {
            Title = "Tự động";
            HotKey = Key.U;
            Size = new Vector2(520, 640);
        }

        protected override void Build()
        {
            _status = UIKit.Text("Status", Body, "", 16, TextAlignmentOptions.TopLeft);
            _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 62), new Vector2(0.5f, 1));
            for (int i = 0; i < Rows; i++)
            {
                float y = -72 - i * RowH;
                var l = UIKit.Text("L" + i, Body, $"{Labels[i]}\n<size=12><color=#9aa>{Hints[i]}</color></size>", 16);
                l.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(250, 50), new Vector2(0, 1));
                _toggle[i] = UIKit.Button("T" + i, Body, "Bật", null, 16);
                ((RectTransform)_toggle[i].transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, y - 4), new Vector2(76, 40), new Vector2(1, 1));
            }
            (_hpMinus, _hpPct, _hpPlus) = Percent(2);
            (_mpMinus, _mpPct, _mpPlus) = Percent(3);

            var head = UIKit.Text("BuffHead", Body, "<b>Chiêu buff tự dùng</b>  <size=12><color=#9aa>hồi xong là dùng · chiêu đánh: luôn dùng chiêu đang chọn</color></size>", 16);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, ListTop), new Vector2(0, 28), new Vector2(0.5f, 1));
            _buffList = UIKit.Rect("BuffList", Body);
            _buffList.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, ListTop - 32), new Vector2(0, 4 * BuffH), new Vector2(0.5f, 1));

            _onOff = UIKit.Button("OnOff", Body, "Bật tự động (Z)", null, 18);
            ((RectTransform)_onOff.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(240, 44), new Vector2(0.5f, 0));
        }

        /// <summary>[-] 50% [+] giữa nhãn và nút Bật của dòng ngưỡng %.</summary>
        private (Button, TextMeshProUGUI, Button) Percent(int row)
        {
            float y = -72 - row * RowH;
            var minus = UIKit.Button("M" + row, Body, "-", null, 18);
            ((RectTransform)minus.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, y - 4), new Vector2(40, 40), new Vector2(1, 1));
            var v = UIKit.Text("V" + row, Body, "", 17, TextAlignmentOptions.Center, UIKit.ButtonHot);
            v.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-128, y), new Vector2(60, 44), new Vector2(1, 1));
            var plus = UIKit.Button("P" + row, Body, "+", null, 18);
            ((RectTransform)plus.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-84, y - 4), new Vector2(40, 40), new Vector2(1, 1));
            return (minus, v, plus);
        }

        protected override void Bind()
        {
            _toggle[0].onClick.AddListener(() => { AutoSettings.FindMobs = !AutoSettings.FindMobs; Refresh(); });
            _toggle[1].onClick.AddListener(() => { AutoSettings.PickItems = !AutoSettings.PickItems; Refresh(); });
            _toggle[2].onClick.AddListener(() => { AutoSettings.PotionHp = !AutoSettings.PotionHp; Refresh(); });
            _toggle[3].onClick.AddListener(() => { AutoSettings.PotionMp = !AutoSettings.PotionMp; Refresh(); });
            _hpMinus.onClick.AddListener(() => { AutoSettings.HpPercent -= 10; Refresh(); });
            _hpPlus.onClick.AddListener(() => { AutoSettings.HpPercent += 10; Refresh(); });
            _mpMinus.onClick.AddListener(() => { AutoSettings.MpPercent -= 10; Refresh(); });
            _mpPlus.onClick.AddListener(() => { AutoSettings.MpPercent += 10; Refresh(); });
            _onOff.onClick.AddListener(() => { Hide(); AutoPlay.Toggle(); });   // bật xong đóng cửa sổ: mở cửa sổ thì không đánh được
            AutoPlay.OnToggled += _ => RefreshIfOpen();
            GameData.OnChanged += k => { if (k == DataKind.Inventory || k == DataKind.Skills || k == DataKind.Templates) RefreshIfOpen(); };
        }

        protected override void Refresh()
        {
            float left = GameData.AutoTrainSecondsLeft();
            _status.text = (AutoPlay.IsOn ? "<color=#7f7><b>Đang tự động</b></color>" : "<b>Tự động đang tắt</b>") + "\n" +
                (left > 0 ? $"<color=#7cf>Tự động luyện tập: còn {GameData.FormatDuration(left)} — dịch chuyển thẳng tới quái.</color>"
                          : "<size=14><color=#bbb>Không có Tự động luyện tập: chạy bộ tới quái. Mua ở Hokage (xu / lượng) để dịch chuyển.</color></size>");
            bool[] on = { AutoSettings.FindMobs, AutoSettings.PickItems, AutoSettings.PotionHp, AutoSettings.PotionMp };
            for (int i = 0; i < Rows; i++) _toggle[i].SetLabel(OnOffText(on[i]));
            _hpPct.text = $"{AutoSettings.HpPercent}%";
            _mpPct.text = $"{AutoSettings.MpPercent}%";
            _onOff.SetLabel(AutoPlay.IsOn ? "Tắt tự động (Z)" : "Bật tự động (Z)");
            BuildBuffRows();
        }

        private static string OnOffText(bool on) => on ? "<color=#1a7a1a>Bật</color>" : "<color=#8a2a2a>Tắt</color>";

        /// <summary>1 hàng / chiêu hỗ trợ của hệ: icon · tên (chưa học thì mờ + "chưa học") · nút Bật / Tắt.</summary>
        private void BuildBuffRows()
        {
            for (int i = _buffList.childCount - 1; i >= 0; i--) Destroy(_buffList.GetChild(i).gameObject);
            var skills = AutoPlay.BuffSkills();
            if (skills.Count == 0)
            {
                var none = UIKit.Text("None", _buffList, "<color=#9aa>Hệ này chưa có chiêu hỗ trợ.</color>", 15);
                none.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 30), new Vector2(0.5f, 1));
                return;
            }
            for (int i = 0; i < skills.Count && i < 4; i++)
            {
                var s = skills[i];
                bool learned = GameData.MySkills.ContainsKey(s.id);
                var row = UIKit.Rect("Buff" + s.id, _buffList);
                row.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -i * BuffH), new Vector2(0, BuffH - 4), new Vector2(0.5f, 1));
                ItemIcon.Add(row, s.iconId, ItemIcon.Place.Left, 42);
                var name = UIKit.Text("Name", row, learned ? s.name : $"<color=#888>{s.name}  <size=12>(chưa học)</size></color>", 16, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(52, 0), new Vector2(-140, 0), new Vector2(0, 0.5f));
                int id = s.id;
                var btn = UIKit.Button("T", row, OnOffText(AutoSettings.Buff(id)), () => { AutoSettings.SetBuff(id, !AutoSettings.Buff(id)); Refresh(); }, 16);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(76, 40), new Vector2(1, 0.5f));
                btn.interactable = learned;
            }
        }
    }
}
