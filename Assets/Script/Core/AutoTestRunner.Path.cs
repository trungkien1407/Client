using System.Collections;
using System.Linq;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Core
{
    // Autotest — "Tự đi" tới nơi làm nhiệm vụ qua nhiều map (Combat.AutoPath, mục 5m).
    public partial class AutoTestRunner
    {
        /// <summary>
        /// Cửa trường giữa Làng Lá: hố đậy bục 1 chiều → đi ngang qua KHÔNG lọt xuống; đứng trên nắp bấm ↓ (rơi xuyên bục) → vào trường.
        /// </summary>
        private IEnumerator CheckVillageDoor()
        {
            GameActions.Chat(1, "/tele 1 19 4.1");   // đứng ngay trên nắp hố cửa Y Thuật Đường
            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);
            yield return new WaitForSecondsRealtime(2f);
            var me = NetworkPlayerManager.Instance.localPlayer;
            Check($"Đứng / nhảy qua cửa trường giữa làng → nắp bục đỡ, không lọt xuống (y {me.transform.position.y:0.0})", GameData.MapName == "Làng Lá" && me.transform.position.y > 3.5f);
            bool dropped = me.TryDropThrough();
            yield return WaitUntil(() => GameData.MapName == "Y Thuật Đường", 4);
            Check($"Đứng trên nắp cửa bấm ↓ → rơi xuống, vào Y Thuật Đường (rơi {dropped})", GameData.MapName == "Y Thuật Đường");
        }

        /// <summary>
        /// Mục 5m: đứng trong trường của hệ mình (map 12 / 13 / 14, chỉ có 1 cổng về Làng Lá) → mở sổ tay → bấm "Tự đi"
        /// một nhiệm vụ ở Làng Lá → nhân vật phải tự chạy vào cổng, sang Làng Lá, rồi tới nơi (NPC / quái).
        /// </summary>
        private IEnumerator TestAutoPath()
        {
            var me = NetworkPlayerManager.Instance.localPlayer;
            int cls = me != null ? me.GetComponent<PlayerVisualController>().ClassType : 1;
            int school = 11 + Mathf.Clamp(cls, 1, 3);
            string schoolName = new[] { "Đấu Sĩ Đường", "Y Thuật Đường", "Ảnh Sát Đường" }[school - 12];
            GameActions.Chat(1, "/tele " + school);
            yield return WaitUntil(() => GameData.MapName == schoolName, 8);
            yield return new WaitForSecondsRealtime(1f);

            var qw = GameWindow.Open<QuestWindow>();
            yield return WaitUntil(() => GameData.QuestGuide.Count > 0, 5);
            yield return null;
            // Ưu tiên việc gặp NPC (nhận / trả) ở Làng Lá; không có thì việc đánh quái ở Làng Lá
            var row = GameData.QuestGuide.FirstOrDefault(g => g.status != 0 && g.where.Contains("Làng Lá"))
                      ?? GameData.QuestGuide.FirstOrDefault(g => g.where.Contains("Làng Lá"));
            Check($"Sổ tay có nhiệm vụ ở Làng Lá để thử Tự đi (đang ở {schoolName})", row != null);
            if (row == null) { qw.Hide(); yield break; }

            bool clicked = qw.ClickGo(row.questId);
            Check("Mỗi dòng sổ tay có nút \"Tự đi\" — bấm thì đóng sổ tay", clicked && !qw.IsOpen);
            yield return WaitUntil(() => AutoPath.LastKind >= 0 && AutoPath.LastText.Length > 0, 5);
            Check($"Server chỉ đường: chạy tới cổng ({AutoPath.LastText})", AutoPath.LastKind == 1 && AutoPath.IsActive);
            yield return Shot("AutoPath_School");

            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 25);
            Check("Tự chạy vào cổng → sang Làng Lá", GameData.MapName == "Làng Lá");
            yield return WaitUntil(() => !AutoPath.IsActive, 45);
            Check($"Ở Làng Lá: tự tới nơi ({AutoPath.LastText})", !AutoPath.IsActive && AutoPath.LastKind == 2);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("AutoPath_Arrived");

            // Dọn: đóng hội thoại NPC nếu đang mở (tới nơi = tự nói chuyện)
            var dlg = GameWindow.Get<NpcDialogWindow>();
            if (dlg != null && dlg.IsOpen) dlg.Hide();
            AutoPath.Stop();
        }
    }
}
