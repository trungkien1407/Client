using System.Linq;
using System.Text;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// SỔ TAY NHIỆM VỤ (phím L).
    ///   Đang làm:     tên, mô tả, tiến độ, NƠI CẦN TỚI (map có quái / NPC), phần thưởng, NPC trả.
    ///   Có thể nhận:  nhiệm vụ đã mở khoá + NPC giao ở làng nào (GĐ9 — người mới luôn biết việc kế tiếp).
    /// "Nơi cần tới" do server dựng (QUEST_GUIDE) vì chỉ server biết quái/NPC nằm ở map nào và nhiệm vụ nào đã mở khoá.
    /// </summary>
    public class QuestWindow : GameWindow
    {
        [SerializeField] private RectTransform _list;
        private bool _requested;
        private int _knownQuestCount = -1;

        protected override void Configure()
        {
            Title = "Nhiệm vụ";
            HotKey = Key.L;
            Size = new Vector2(600, 520);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _list = UIKit.ScrollList("List", body, 6);
            ((RectTransform)_list.parent).Fill();
        }

        protected override void Bind()
        {
            GameData.OnChanged += k =>
            {
                if (k == DataKind.Quests && IsOpen && GameData.MyQuests.Count != _knownQuestCount) _requested = false; // nhận / trả nhiệm vụ → xin lại sổ tay
                if (k == DataKind.Quests || k == DataKind.Inventory || k == DataKind.Templates || k == DataKind.QuestGuide) RefreshIfOpen();
            };
        }

        private void OnDisable() => _requested = false;

        private string WhereOf(int questId)
        {
            var g = GameData.QuestGuide.FirstOrDefault(r => r.questId == questId);
            return g != null ? g.where : null;
        }

        protected override void Refresh()
        {
            if (!_requested) { _requested = true; _knownQuestCount = GameData.MyQuests.Count; GameActions.QuestGuide(); }
            UIKit.Clear(_list);

            UIKit.Text("H1", _list, "<b><color=#fd5>Đang làm</color></b>", 17).Height(26);
            if (GameData.MyQuests.Count == 0)
                UIKit.Text("Empty", _list, "<color=#bbb>Chưa nhận nhiệm vụ nào — xem mục \"Có thể nhận\" bên dưới.</color>", 15).Height(30);
            foreach (var kv in GameData.MyQuests)
            {
                if (!GameData.Quests.TryGetValue(kv.Key, out var q)) continue;
                int progress = q.type == 2 ? Mathf.Min(q.targetCount, GameData.CountItem(q.targetId)) : kv.Value[0];
                bool done = kv.Value[1] == 1 || (q.type == 2 && progress >= q.targetCount);

                var sb = new StringBuilder();
                sb.Append($"<b>{q.name}</b>  {(done ? "<color=#7f7>[Hoàn thành]</color>" : "")}\n");
                sb.Append($"<size=14><color=#bbb>{q.description}</color></size>\n");
                sb.Append(q.type switch
                {
                    0 => $"Hạ {GameData.MobName(q.targetId)}: {progress}/{q.targetCount}",
                    1 => $"Gặp {GameData.NpcName(q.targetId)}",
                    2 => $"Thu thập {GameData.ItemName(q.targetId)}: {progress}/{q.targetCount}",
                    _ => ""
                });
                string where = WhereOf(q.id);
                if (!done && !string.IsNullOrEmpty(where)) sb.Append($"\n<color=#7cf>Nơi: {where}</color>");
                sb.Append($"\n<size=14><color=#fd5>Thưởng: {(q.rewardExp > 0 ? q.rewardExp + " EXP, " : "")}{q.rewardYen} yên");
                if (q.rewardItemId > 0) sb.Append($", {GameData.ItemName(q.rewardItemId)} x{q.rewardItemQty}");
                sb.Append("</color></size>");
                if (done) sb.Append($"\n<color=#7cf>→ Về gặp {(string.IsNullOrEmpty(where) ? GameData.NpcName(q.npcId) : where)} để trả</color>");

                var row = UIKit.Panel("Q" + q.id, _list, UIKit.SlotColor).Height(150);
                UIKit.Text("Text", row.transform, sb.ToString(), 16, TextAlignmentOptions.TopLeft).rectTransform.Fill(8, 8, 6, 6);
            }

            UIKit.Text("H2", _list, "\n<b><color=#fd5>Có thể nhận</color></b>  <size=13><color=#9aa>(đến gặp NPC để nhận)</color></size>", 17).Height(44);
            int n = 0;
            foreach (var g in GameData.QuestGuide)
            {
                if (g.status != 1 || !GameData.Quests.TryGetValue(g.questId, out var q)) continue;
                string tag = g.daily ? "<color=#8cf>[Ngày]</color>" : "<color=#fd5>[Mới]</color>";
                string lv = q.levelRequire > 0 ? $"  <color=#999>cấp {q.levelRequire}</color>" : "";
                var row = UIKit.Panel("A" + q.id, _list, new Color(0.12f, 0.15f, 0.2f)).Height(52);
                UIKit.Text("Text", row.transform, $"{tag} <b>{q.name}</b>{lv}\n<size=14><color=#7cf>Nhận ở: {g.where}</color></size>", 15,
                    TextAlignmentOptions.MidlineLeft).rectTransform.Fill(8, 8, 2, 2);
                n++;
            }
            if (n == 0) UIKit.Text("None", _list, GameData.QuestGuide.Count == 0 && _requested
                ? "<color=#888>(đang tải...)</color>" : "<color=#888>Không có nhiệm vụ mới — lên cấp để mở thêm.</color>", 15).Height(28);
        }
    }
}
