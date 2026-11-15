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
    /// BẢNG NHÂN VẬT (phím C): cấp, EXP, HP/MP, sát thương, né/chí mạng, tiền + CỘNG ĐIỂM TIỀM NĂNG.
    /// Bấm [+1]/[+5] → gửi ADD_POTENTIAL; server tính lại và trả CHARACTER_INFO → bảng tự vẽ lại.
    /// 2026-11-15: kẻ BẢNG (UITable) thay khối chữ — trái: thông tin từng hàng "nhãn | giá trị"; phải: bảng tiềm năng
    /// "chỉ số | điểm | cộng" có nút +1 / +5 trong hàng.
    /// </summary>
    public class CharacterWindow : GameWindow
    {
        [SerializeField] private TextMeshProUGUI _points;
        [SerializeField] private TextMeshProUGUI[] _head = new TextMeshProUGUI[2];          // hàng tiêu đề: tên | cấp
        [SerializeField] private TextMeshProUGUI[] _infoValues = new TextMeshProUGUI[InfoLabels.Length];
        [SerializeField] private TextMeshProUGUI[] _statValues = new TextMeshProUGUI[4];
        [SerializeField] private Button[] _add1 = new Button[4], _add5 = new Button[4];

        private static readonly string[] InfoLabels =
            { "EXP", "HP", "MP", "Sát thương cộng", "Né", "Chí mạng", "Phòng thủ", "Tốc chạy", "Điểm PK", "Yên", "Xu", "Lượng" };
        private static readonly string[] StatNames = { "Sức mạnh", "Thân pháp", "Chakra", "Thể lực" };
        private static readonly string[] StatHints = { "+2 sát thương", "+0.2% né, +0.1% chí mạng", "+5 MP tối đa", "+10 HP tối đa" };
        private static readonly float[] InfoCols = { 0.42f, 0.58f };
        private static readonly float[] StatCols = { 0.47f, 0.17f, 0.36f };

        protected override void Configure()
        {
            Title = "Nhân vật";
            HotKey = Key.C;
            Size = new Vector2(780, 540);   // = CharacterTabs.GroupSize (mọi tab cùng khung)
        }

        protected override void Build()
        {
            // ---- trái: bảng thông tin ----
            var info = new UITable("InfoTable", Body, 28);
            info.Root.Place(new Vector2(0, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-10, 0), new Vector2(0, 1));
            _head = info.Header(InfoCols, "Nhân vật", "Cấp 1");
            for (int i = 0; i < InfoLabels.Length; i++) _infoValues[i] = info.Row(InfoCols, InfoLabels[i], "")[1];
            info.Finish();

            // ---- phải: điểm còn + bảng tiềm năng ----
            _points = UIKit.Text("Points", Body, "", 18, TextAlignmentOptions.MidlineLeft, UIKit.ButtonHot);
            _points.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(1, 1), Vector2.zero, new Vector2(-10, 32), new Vector2(1, 1));

            var pot = new UITable("PotTable", Body, 28);
            pot.Root.Place(new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -38), new Vector2(-10, 0), new Vector2(1, 1));
            var h = pot.Header(StatCols, "Tiềm năng", "Điểm", "Cộng");
            h[1].alignment = TextAlignmentOptions.Center;
            h[2].alignment = TextAlignmentOptions.Center;
            for (int i = 0; i < 4; i++)
            {
                var row = pot.Custom(58, StatCols);
                var name = UIKit.Text("Stat" + i, UITable.Cell(row, 0f, StatCols[0]),
                    $"{StatNames[i]}\n<size=12><color=#9aa>{StatHints[i]}</color></size>", 16);
                name.rectTransform.Fill();
                _statValues[i] = UIKit.Text("Val" + i, UITable.Cell(row, StatCols[0], StatCols[0] + StatCols[1]), "0", 20, TextAlignmentOptions.Center, UIStyle.Gold);
                _statValues[i].rectTransform.Fill();
                _statValues[i].fontStyle = FontStyles.Bold;
                var btns = UITable.Cell(row, StatCols[0] + StatCols[1], 1f, 6f);
                _add1[i] = UIKit.Button("Add1_" + i, btns, "+1", null, 16);
                ((RectTransform)_add1[i].transform).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(-4, 36), new Vector2(0, 0.5f));
                _add5[i] = UIKit.Button("Add5_" + i, btns, "+5", null, 16);
                ((RectTransform)_add5[i].transform).Place(new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(-4, 36), new Vector2(1, 0.5f));
            }
            pot.Finish();

            var note = UIKit.Text("PotNote", Body, "Mỗi lần lên cấp được 5 điểm tiềm năng. Đã cộng thì không rút lại được — chọn theo hệ của bạn.",
                13, TextAlignmentOptions.TopLeft, UIKit.DimText);
            note.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -38 - pot.Height - 10), new Vector2(-10, 44), new Vector2(1, 1));
        }

        protected override void Bind()
        {
            UITable.AddBorder(Body.Find("InfoTable"));   // viền bảng không lưu trong prefab (xem UITable.AddBorder)
            UITable.AddBorder(Body.Find("PotTable"));
            for (int i = 0; i < 4; i++)
            {
                byte stat = (byte)i;
                _add1[i].onClick.AddListener(() => GameActions.AddPotential(stat, 1));
                _add5[i].onClick.AddListener(() => GameActions.AddPotential(stat, (short)Mathf.Min(5, GameData.Me.potential)));
            }
            GameData.OnChanged += k => { if (k == DataKind.Character) RefreshIfOpen(); };
            // Thú cưng / Hành trang / Kỹ năng: chuyển bằng tab phía trên cửa sổ (CharacterTabs) — không cần nút riêng nữa
        }

        protected override void Refresh()
        {
            var c = GameData.Me;
            float expPct = c.expToNext > 0 ? 100f * c.exp / c.expToNext : 0;
            _head[0].text = LocalPlayerState.Name;
            _head[1].text = $"Cấp {c.level}";
            string[] v =
            {
                c.expToNext > 0 ? $"{c.exp:N0} / {c.expToNext:N0}  <color=#9aa>({expPct:F1}%)</color>" : "cấp tối đa",
                $"<color=#ff8a8a>{LocalPlayerState.Hp:N0}</color> / {c.maxHp:N0}",
                $"<color=#8ac8ff>{LocalPlayerState.Mp:N0}</color> / {c.maxMp:N0}",
                $"+{c.bonusDamage}",
                $"{c.dodge * 100:F1}%",
                $"{c.crit * 100:F1}%",
                $"{c.defense}  <size=12><color=#9aa>(giảm {c.reduction * 100:F0}% sát thương)</color></size>",
                $"{c.moveSpeed:F1}",
                $"{c.pkPoint}",
                $"<color=#ffd75a>{c.yen:N0}</color>",
                $"<color=#7fd8ff>{c.xu:N0}</color>",
                $"<color=#ff9ad5>{c.luong:N0}</color>",
            };
            for (int i = 0; i < _infoValues.Length && i < v.Length; i++) _infoValues[i].text = v[i];
            _points.text = $"Điểm tiềm năng còn: <b>{c.potential}</b>";
            int[] sv = { c.sucManh, c.thanPhap, c.chakra, c.theLuc };
            for (int i = 0; i < 4; i++)
            {
                _statValues[i].text = sv[i].ToString();
                _add1[i].interactable = c.potential > 0;
                _add5[i].interactable = c.potential > 0;
            }
        }
    }
}
