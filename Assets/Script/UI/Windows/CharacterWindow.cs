using Assets.Script.Data;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// BẢNG NHÂN VẬT (phím C): cấp, EXP, HP/MP, sát thương, né/chí mạng, tiền + CỘNG ĐIỂM TIỀM NĂNG.
    /// Bấm [+1]/[+5] → gửi ADD_POTENTIAL; server tính lại và trả CHARACTER_INFO → bảng tự vẽ lại.
    /// </summary>
    public class CharacterWindow : GameWindow
    {
        private TextMeshProUGUI _info, _points;
        private readonly TextMeshProUGUI[] _statValues = new TextMeshProUGUI[4];

        private static readonly string[] StatNames = { "Sức mạnh", "Thân pháp", "Chakra", "Thể lực" };
        private static readonly string[] StatHints = { "+2 sát thương", "+0.2% né, +0.1% chí mạng", "+5 MP tối đa", "+10 HP tối đa" };

        protected override void Build()
        {
            Title = "Nhân vật";
            HotKey = Key.C;
            Size = new Vector2(460, 470);
            var body = CreateBody();

            _info = UIKit.Text("Info", body, "", 18, TextAlignmentOptions.TopLeft);
            _info.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 200), new Vector2(0.5f, 1));

            _points = UIKit.Text("Points", body, "", 19, TextAlignmentOptions.MidlineLeft, UIKit.ButtonHot);
            _points.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -205), new Vector2(0, 30), new Vector2(0.5f, 1));

            for (int i = 0; i < 4; i++)
            {
                byte stat = (byte)i;
                float y = -245 - i * 46;
                var name = UIKit.Text("Stat" + i, body, $"{StatNames[i]}\n<size=13><color=#9aa>{StatHints[i]}</color></size>", 17);
                name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(200, 44), new Vector2(0, 1));
                _statValues[i] = UIKit.Text("Val" + i, body, "0", 20, TextAlignmentOptions.Center);
                _statValues[i].rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(205, y), new Vector2(70, 44), new Vector2(0, 1));
                var b1 = UIKit.Button("Add1_" + i, body, "+1", () => GameActions.AddPotential(stat, 1));
                ((RectTransform)b1.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(290, y - 4), new Vector2(60, 36), new Vector2(0, 1));
                var b5 = UIKit.Button("Add5_" + i, body, "+5", () => GameActions.AddPotential(stat, (short)Mathf.Min(5, GameData.Me.potential)));
                ((RectTransform)b5.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(360, y - 4), new Vector2(60, 36), new Vector2(0, 1));
            }

            GameData.OnChanged += k => { if (k == DataKind.Character) RefreshIfOpen(); };
        }

        protected override void Refresh()
        {
            var c = GameData.Me;
            float expPct = c.expToNext > 0 ? 100f * c.exp / c.expToNext : 0;
            _info.text =
                $"<b>{LocalPlayerState.Name}</b>   Cấp <b>{c.level}</b>\n" +
                $"EXP: {c.exp:N0} / {c.expToNext:N0}  ({expPct:F1}%)\n" +
                $"HP: {LocalPlayerState.Hp}/{c.maxHp}    MP: {LocalPlayerState.Mp}/{c.maxMp}\n" +
                $"Sát thương cộng thêm: +{c.bonusDamage}\n" +
                $"Né: {c.dodge * 100:F1}%    Chí mạng: {c.crit * 100:F1}%\n" +
                $"Phòng thủ: {c.defense}  <size=13><color=#9aa>(giảm {c.reduction * 100:F0}% sát thương quái cùng cấp)</color></size>\n" +
                $"Tốc chạy: {c.moveSpeed:F1}    Điểm PK: {c.pkPoint}\n" +
                $"Yên: {c.yen:N0}   Xu: {c.xu:N0}   Lượng: {c.luong:N0}";
            _points.text = $"Điểm tiềm năng còn: {c.potential}";
            int[] v = { c.sucManh, c.thanPhap, c.chakra, c.theLuc };
            for (int i = 0; i < 4; i++) _statValues[i].text = v[i].ToString();
        }
    }
}
