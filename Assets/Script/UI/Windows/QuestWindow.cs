using System.Text;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.UI.Windows
{
    /// <summary>NHIỆM VỤ ĐANG LÀM (phím L): tên, mô tả, tiến độ, NPC cần gặp để trả, phần thưởng.</summary>
    public class QuestWindow : GameWindow
    {
        private RectTransform _list;

        protected override void Build()
        {
            Title = "Nhiệm vụ";
            HotKey = Key.L;
            Size = new Vector2(560, 460);
            var body = CreateBody();
            _list = UIKit.ScrollList("List", body, 6);
            ((RectTransform)_list.parent).Fill();
            GameData.OnChanged += k => { if (k == DataKind.Quests || k == DataKind.Inventory || k == DataKind.Templates) RefreshIfOpen(); };
        }

        protected override void Refresh()
        {
            UIKit.Clear(_list);
            if (GameData.MyQuests.Count == 0)
            {
                UIKit.Text("Empty", _list, "Chưa có nhiệm vụ. Hãy nói chuyện với <b>Hokage</b> ở Làng Lá.", 17).Height(60);
                return;
            }
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
                sb.Append($"\n<size=14><color=#fd5>Thưởng: {q.rewardExp} EXP, {q.rewardYen} yên");
                if (q.rewardItemId > 0) sb.Append($", {GameData.ItemName(q.rewardItemId)} x{q.rewardItemQty}");
                sb.Append("</color></size>");
                if (done) sb.Append($"\n<color=#7cf>→ Về gặp {GameData.NpcName(q.npcId)} để trả</color>");

                var row = UIKit.Panel("Q" + q.id, _list, UIKit.SlotColor).Height(done ? 150 : 128);
                UIKit.Text("Text", row.transform, sb.ToString(), 16, TextAlignmentOptions.TopLeft).rectTransform.Fill(8, 8, 6, 6);
            }
        }
    }
}
