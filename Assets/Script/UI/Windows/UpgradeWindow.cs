using System.Text;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// NÂNG CẤP TRANG BỊ +0 → +16 (mở khi nói chuyện Thợ Rèn → "Nâng cấp", server gửi UPGRADE_OPEN).
    ///  - Trái: các trang bị trong túi (kèm +N). Bấm để chọn.
    ///  - Phải: tỉ lệ thành công, đá cần (đang có), yên cần, chỉ số trước/sau, tuỳ chọn Bùa bảo hộ.
    ///  - Bấm [Nâng cấp] → gửi UPGRADE_ITEM(npcId, bagIndex, dùng bùa). Kết quả UPGRADE_RESULT hiện ngay dưới.
    /// Luật (server quyết định): trượt dưới +5 giữ cấp, +5..+9 tụt 1, từ +10 tụt 2; Bùa bảo hộ giữ nguyên cấp.
    /// </summary>
    public class UpgradeWindow : GameWindow
    {
        [SerializeField] private RectTransform _list;
        [SerializeField] private TextMeshProUGUI _detail, _result;
        [SerializeField] private Button _btnUp, _btnProtect;
        private int _npcId = -1, _sel = -1;
        private bool _useProtect;

        protected override void Configure()
        {
            Title = "Nâng cấp trang bị";
            Size = new Vector2(760, 500);
        }

        protected override void Build()
        {
            var body = CreateBody();

            _list = UIKit.ScrollList("Equips", body, 4);
            ((RectTransform)_list.parent).Place(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(300, 0), new Vector2(0, 0.5f));

            var right = UIKit.Panel("Right", body, new Color(0, 0, 0, 0.3f)).rectTransform;
            right.Fill(310, 0, 0, 0);
            _detail = UIKit.Text("Detail", right, "Chọn 1 trang bị bên trái", 17, TextAlignmentOptions.TopLeft);
            _detail.rectTransform.Fill(12, 12, 10, 150);
            _btnProtect = UIKit.Button("Protect", right, "", null, 15);
            ((RectTransform)_btnProtect.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 104), new Vector2(-24, 36), new Vector2(0.5f, 0));
            _btnUp = UIKit.Button("Upgrade", right, "NÂNG CẤP", null, 20);
            ((RectTransform)_btnUp.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(220, 46), new Vector2(0.5f, 0));
            _btnUp.image.color = UIKit.ButtonHot;
            _result = UIKit.Text("Result", right, "", 16, TextAlignmentOptions.Center);
            _result.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(-16, 42), new Vector2(0.5f, 0));
        }

        protected override void Bind()
        {
            _btnProtect.onClick.AddListener(() => { _useProtect = !_useProtect; Refresh(); });
            _btnUp.onClick.AddListener(OnUpgrade);
            GameData.OnChanged += k =>
            {
                if (k == DataKind.Inventory || k == DataKind.Money || k == DataKind.Character || k == DataKind.Upgrade) RefreshIfOpen();
            };
        }

        /// <summary>Kết quả lần nâng gần nhất (-1 = chưa có) — AutoTest đọc.</summary>
        public int LastResult { get; private set; } = -1;

        /// <summary>Chọn ô túi (bagIndex) từ code.</summary>
        public void Select(int bagIndex) { _sel = bagIndex; RefreshIfOpen(); }

        public void ShowFor(int npcId)
        {
            LastResult = -1;
            _npcId = npcId;
            _result.text = "";
            Show();
        }

        protected override void Refresh()
        {
            UIKit.Clear(_list);
            bool any = false;
            for (int i = 0; i < GameData.Inventory.Count; i++)
            {
                var s = GameData.Inventory[i];
                if (!GameData.Items.TryGetValue(s.tpl, out var t) || !t.IsEquip) continue;
                any = true;
                int idx = i;
                var b = UIKit.Button("Eq" + i, _list, GameData.ColoredName(s.tpl, s.level) + (s.locked ? " <size=12><color=#aaa>(khoá)</color></size>" : ""),
                    () => { _sel = idx; _result.text = ""; Refresh(); }, 16).Height(44);
                b.image.color = idx == _sel ? UIKit.ButtonHot : UIKit.SlotColor;
            }
            if (!any) UIKit.Text("Empty", _list, "Túi không có trang bị.\n(Tháo trang bị đang mặc ra túi để nâng cấp.)", 15).Height(60);

            UpdateDetail();
        }

        private void UpdateDetail()
        {
            var t = GameData.Upgrade;
            var s = GameData.BagAt(_sel);
            bool isEquip = s != null && GameData.Items.TryGetValue(s.tpl, out var tpl) && tpl.IsEquip;
            int protectHave = t != null ? GameData.CountItem(t.protectItemId) : 0;
            _btnProtect.SetLabel($"{(_useProtect ? "[x]" : "[  ]")} Dùng {GameData.ItemName(t?.protectItemId ?? 0)} (có {protectHave}) — giữ cấp khi trượt");
            _btnProtect.interactable = protectHave > 0;
            _btnProtect.gameObject.SetActive(t != null);
            if (protectHave == 0) _useProtect = false;

            if (t == null || !isEquip) { _detail.text = "Chọn 1 trang bị bên trái"; _btnUp.interactable = false; return; }
            if (s.level >= t.maxLv) { _detail.text = $"{GameData.ColoredName(s.tpl, s.level)}\n\nĐã đạt cấp tối đa +{t.maxLv}."; _btnUp.interactable = false; return; }

            int next = s.level + 1;
            int stones = GameData.CountItem(t.stoneId[next]);
            bool okStone = stones >= t.stoneQty[next], okYen = GameData.Me.yen >= t.yen[next];
            int pctNow = s.level < t.statPercent.Length ? t.statPercent[s.level] : 0;
            int pctNext = next < t.statPercent.Length ? t.statPercent[next] : 0;
            string fail = s.level < 5 ? "giữ nguyên cấp" : s.level < 10 ? "tụt 1 cấp" : "tụt 2 cấp";

            var sb = new StringBuilder();
            sb.Append($"<size=20>{GameData.ColoredName(s.tpl, s.level)}  →  {GameData.ColoredName(s.tpl, next)}</size>\n\n");
            sb.Append($"Tỉ lệ thành công: <b><color={(t.rate[next] >= 50 ? "#7f7" : t.rate[next] >= 20 ? "#fd5" : "#f77")}>{t.rate[next]}%</color></b>\n");
            sb.Append($"Chỉ số trang bị: +{pctNow}% → <color=#7f7>+{pctNext}%</color>\n");
            sb.Append($"Cần: <color={(okStone ? "#7f7" : "#f77")}>{t.stoneQty[next]} {GameData.ItemName(t.stoneId[next])} (có {stones})</color>\n");
            sb.Append($"Phí: <color={(okYen ? "#7f7" : "#f77")}>{t.yen[next]:N0} yên</color> (có {GameData.Me.yen:N0})\n");
            sb.Append($"<color=#bbb>Nếu trượt: {(_useProtect ? "giữ nguyên cấp (Bùa bảo hộ)" : fail)}</color>");
            _detail.text = sb.ToString();
            _btnUp.interactable = okStone && okYen;
        }

        private void OnUpgrade()
        {
            if (_npcId < 0 || GameData.BagAt(_sel) == null) return;
            _result.text = "<color=#aaa>Đang nâng cấp...</color>";
            GameActions.Upgrade(_npcId, _sel, _useProtect);
        }

        /// <summary>Kết quả từ server: 0 thành công / 1 trượt giữ cấp / 2 trượt tụt cấp / 3 lỗi.</summary>
        public void ShowResult(int result, int level, string msg)
        {
            LastResult = result;
            string color = result == 0 ? "#6f6" : result == 3 ? "#f77" : "#fd5";
            _result.text = $"<color={color}>{msg}</color>";
            // [CẦN ĐIỀN khi có âm thanh] phát tiếng "keng" khi thành công / tiếng vỡ khi trượt: AudioManager.Play("upgrade_ok"/"upgrade_fail")
            Assets.Script.Core.AudioManager.Play(result == 0 ? "upgrade_ok" : "upgrade_fail");
            RefreshIfOpen();
        }
    }
}
