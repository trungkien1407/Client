#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Map;
using Assets.Script.Player;
using Assets.Script.UI;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;
using UnityEngine.Tilemaps;
using Assets.Script.Constants;
using Assets.Script.Fx;
using Assets.Script.Skill;

namespace Assets.Script.Core
{
    // Autotest — nói chuyện NPC, Thợ Rèn: nâng cấp / rương đồ (mục 4).
    public partial class AutoTestRunner
    {
        /// <summary>Mục 4: hội thoại NPC, Thợ Rèn → Nâng cấp / Rương đồ, bật / tắt Đồ sát.</summary>
        private IEnumerator TestNpc()
        {
            // 4. Nói chuyện NPC gần nhất → phải hiện hội thoại
            _dlg = GameWindow.Get<NpcDialogWindow>();
            Assets.Script.Data.NpcNetwork.TalkToNearest();
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            Check("Nói chuyện NPC → hiện hội thoại", _dlg.IsOpen);
            _dlg.Hide();

            // 4b. GĐ2–GĐ3 qua UI thật: Thợ Rèn → Nâng cấp / Rương đồ; bật-tắt Đồ sát (tài khoản test là GM)
            GameActions.Chat(1, "/item 10 1"); GameActions.Chat(1, "/item 40 5"); GameActions.Chat(1, "/yen 100000");
            // GĐ8: tới cạnh Thợ Rèn theo vị trí thật trong map (không ghi cứng toạ độ làng)
            var smith = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(2) : null;
            var sp = smith != null ? smith.transform.position : NetworkPlayerManager.Instance.localPlayer.transform.position;
            GameActions.Chat(1, $"/go {(sp.x + 0.6f).ToString(Inv)} {(sp.y + 0.05f).ToString(Inv)}");
            yield return new WaitForSecondsRealtime(1.5f);
            Assets.Script.Data.NpcNetwork.TalkToNearest();
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            bool chose = _dlg.Choose("Nâng cấp");
            var up = GameWindow.Get<UpgradeWindow>();
            yield return WaitUntil(() => up.IsOpen && GameData.Upgrade != null, 5);
            Check("Thợ Rèn → mở bảng Nâng cấp", chose && up.IsOpen && GameData.Upgrade != null);
            int eqIdx = -1;
            for (int i = 0; i < GameData.Inventory.Count && eqIdx < 0; i++)
                if (GameData.Items.TryGetValue(GameData.Inventory[i].tpl, out var it) && it.IsEquip && GameData.Inventory[i].level < 5) eqIdx = i;
            if (GameData.Upgrade != null && eqIdx >= 0)
            {
                up.Select(eqIdx);
                GameActions.Upgrade(GameData.Upgrade.npcId, eqIdx, false);
                yield return WaitUntil(() => up.LastResult >= 0, 5);
                yield return Shot("UpgradeResult");
            }
            Check("Nâng cấp có kết quả (UPGRADE_RESULT)", up.LastResult >= 0 && up.LastResult != 3);
            up.Hide();

            Assets.Script.Data.NpcNetwork.TalkToNearest();
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            _dlg.Choose("Rương");
            var st = GameWindow.Get<StorageWindow>();
            yield return WaitUntil(() => st.IsOpen, 5);
            Check("Thợ Rèn → mở Rương đồ (STORAGE_DATA)", st.IsOpen);
            yield return Shot("StorageOpen");
            st.Hide(); GameWindow.Get<InventoryWindow>().Hide();

            if (GameData.Me.level < 10) { GameActions.Chat(1, "/lv 10"); yield return new WaitForSecondsRealtime(0.5f); } // Đồ sát cần cấp 10
            GameActions.SetPkMode(1);
            yield return WaitUntil(() => GameData.MyPkMode == 1, 5);
            Check("Bật Đồ sát → server xác nhận (PLAYER_PVP_INFO)", GameData.MyPkMode == 1);
            yield return Shot("PkOn");
            GameActions.SetPkMode(0);
            yield return WaitUntil(() => GameData.MyPkMode == 0, 5);
        }
    }
}
#endif
