using System.Collections.Generic;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// KỸ NĂNG (phím K): danh sách kỹ năng của hệ mình. Mỗi dòng: cấp hiện tại, thông số cấp kế,
    /// nút [Học]/[Nâng] (tốn 1 điểm kỹ năng) và nút [1]..[5] để gán vào thanh phím tắt.
    /// </summary>
    public class SkillWindow : GameWindow
    {
        private RectTransform _list;
        private TextMeshProUGUI _points;

        protected override void Build()
        {
            Title = "Kỹ năng";
            HotKey = Key.K;
            Size = new Vector2(640, 500);
            var body = CreateBody();
            _points = UIKit.Text("Points", body, "", 18, TextAlignmentOptions.MidlineLeft, UIKit.ButtonHot);
            _points.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 28), new Vector2(0.5f, 1));
            _list = UIKit.ScrollList("List", body, 6);
            ((RectTransform)_list.parent).Fill(0, 0, 32, 0);

            GameData.OnChanged += k => { if (k == DataKind.Skills || k == DataKind.Character || k == DataKind.Templates) RefreshIfOpen(); };
        }

        protected override void Refresh()
        {
            _points.text = $"Điểm kỹ năng: {GameData.Me.skillPoints}   (lên cấp +1 điểm)";
            UIKit.Clear(_list);

            var skills = new List<SkillTpl>();
            foreach (var s in GameData.Skills.Values)
                if ((s.classId == 0 || s.classId == GameData.ClassType) && s.levels.Count > 0) skills.Add(s);
            skills.Sort((a, b) => a.id.CompareTo(b.id));

            foreach (var s in skills)
            {
                GameData.MySkills.TryGetValue(s.id, out int cur);
                var row = UIKit.Panel("Row" + s.id, _list, UIKit.SlotColor).Height(96);

                var curLv = s.Level(cur);
                var next = s.Level(cur + 1);
                string info = $"<b>{s.name}</b>  <color=#fd5>Cấp {cur}/{s.maxLevel}</color>\n<size=13><color=#bbb>{s.description}</color></size>\n";
                if (curLv != null) info += $"<size=14>Hiện tại: {curLv.damage} sát thương · {curLv.manaUse} MP · hồi {curLv.coolDown / 1000f:0.#}s{(curLv.aoe > 0 ? " · diện rộng" : "")}</size>\n";
                if (next != null) info += $"<size=14><color=#7cf>Cấp kế: {next.damage} sát thương · {next.manaUse} MP</color></size>";
                var text = UIKit.Text("Info", row.transform, info, 16, TextAlignmentOptions.TopLeft);
                text.rectTransform.Fill(8, 210, 4, 4);

                int id = s.id;
                if (next != null)
                {
                    var up = UIKit.Button("Up", row.transform, cur == 0 ? "Học" : "Nâng", () => GameActions.UpgradeSkill(id));
                    ((RectTransform)up.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -6), new Vector2(190, 34), new Vector2(1, 1));
                    up.interactable = GameData.Me.skillPoints > 0;
                }
                if (cur > 0 && s.type == 1) // chỉ chiêu chủ động mới gán phím
                {
                    for (int k = 0; k < 5; k++)
                    {
                        int slot = k;
                        var b = UIKit.Button("Key" + k, row.transform, (k + 1).ToString(), () => AssignShortcut(slot, id), 16);
                        ((RectTransform)b.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8 - (4 - k) * 38, 6), new Vector2(34, 32), new Vector2(1, 0));
                    }
                }
            }
        }

        /// <summary>Gán skill vào ô phím: báo server lưu + cập nhật thanh skill trên HUD ngay.</summary>
        private static void AssignShortcut(int slot, int skillId)
        {
            GameActions.SetShortcut(slot, skillId);
            var bar = SkillBarManager.Instance;
            if (bar == null) return;
            for (int i = 0; i < bar.skillSlots.Length; i++)
                if (bar.skillSlots[i] != null && bar.skillSlots[i].assignedSkillId == skillId) bar.AssignSkillToSlot(i, -1);
            bar.AssignSkillToSlot(slot, skillId);
        }
    }
}
