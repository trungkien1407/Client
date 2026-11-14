#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System.Collections;
using System.Linq;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Fx;
using Assets.Script.Manager;
using Assets.Script.Player;
using UnityEngine;

namespace Assets.Script.Core
{
    // Autotest "-crowdshots": cảnh MMO — nhiều người chơi (bot test-client/TestCrowd) cùng khu đánh boss thế giới; kiểm + chụp ảnh rồi thoát.
    public partial class AutoTestRunner
    {
        /// <summary>
        /// GM /lv 120 + học chiêu cấp cao → /boss 1 (Cửu Vĩ Ốc ở Đồi Hoa Cúc, khu 1) → sang đứng cạnh boss → chờ bot TestCrowd vào cùng khu (chạy song song, xem
        /// test-client/TestCrowd.java) → kiểm thấy nhiều người chơi khác → chụp lúc tập trung / cùng đánh / hạ boss.
        /// </summary>
        private IEnumerator CrowdShots()
        {
            int want = Mathf.Max(3, int.TryParse(Arg("-crowd", "8"), out int c) ? c : 8);
            // cấp 120 + học 4 chiêu cấp cao và chiêu biến thân của hệ → chụp lúc dùng chiêu lớn giữa đám đông
            var vis = NetworkPlayerManager.Instance.localPlayer.GetComponent<PlayerVisualController>();
            int cls = vis != null ? vis.ClassType : 1;
            int[] high = cls == 1 ? new[] { 12, 13, 14, 15 } : cls == 2 ? new[] { 16, 17, 18, 19 } : new[] { 20, 21, 22, 23 };
            int tf = cls == 1 ? 24 : cls == 2 ? 25 : 26;
            GameActions.Chat(1, "/lv 120");
            yield return WaitUntil(() => GameData.Me.level >= 100, 4);
            foreach (int sk in high.Append(tf))
            {
                if (!GameData.Skills.ContainsKey(sk) || GameData.MySkills.ContainsKey(sk)) continue;
                GameActions.Chat(1, "/sp 1");
                yield return new WaitForSecondsRealtime(0.4f);
                GameActions.UpgradeSkill(sk);
                yield return WaitUntil(() => GameData.MySkills.ContainsKey(sk), 3);
            }
            GameActions.Chat(1, "/boss 1");
            yield return new WaitForSecondsRealtime(1f);
            GameActions.Chat(1, "/tele 2 9.5 4.1");
            yield return WaitUntil(() => GameData.MapName == "Đồi Hoa Cúc", 8);
            yield return new WaitForSecondsRealtime(2f);
            var me = NetworkPlayerManager.Instance.localPlayer;
            var boss = NetworkMobManager.Instance != null ? NetworkMobManager.Instance.FindNearestAlive(me.transform.position, 25f) : null;
            Check("Boss thế giới đã xuất hiện ở Đồi Hoa Cúc", boss != null);

            // chờ đám đông (bot TestCrowd) vào cùng khu
            int Others() => FindObjectsByType<RemotePlayer>(FindObjectsSortMode.None).Count(r => r.isActiveAndEnabled);
            yield return WaitUntil(() => Others() >= want, 120);
            int seen = Others();
            Check($"Thấy {seen} người chơi khác cùng khu (cần ≥ {want}) — mỗi người là 1 kết nối riêng tới server", seen >= want);
            yield return new WaitForSecondsRealtime(4f);   // để bot chạy tới quanh boss
            CloseTips();
            if (boss != null) FindAnyObjectByType<PlayerTargeting>()?.SelectTarget(boss);
            yield return Shot("Crowd_1_TapTrung");

            // biến thân → chụp
            if (GameData.MySkills.ContainsKey(tf) && vis != null)
            {
                yield return WaitUntil(() => !vis.IsActionLocked, 3);
                SkillCaster.TryCast(tf);
                yield return WaitUntil(() => vis.IsTransformed, 4);
                yield return new WaitForSecondsRealtime(0.8f);
                CloseTips(); yield return Shot("Crowd_2_BienThan");
            }
            // từng chiêu lớn: chụp lúc vật bay đang bay + lúc nổ trúng boss (đám đông xung quanh vẫn đánh)
            int n = 0;
            foreach (int sk in high.Reverse())
            {
                if (!GameData.MySkills.ContainsKey(sk) || vis == null) continue;
                GameActions.Chat(1, "/hp");
                if (boss != null) FindAnyObjectByType<PlayerTargeting>()?.SelectTarget(boss);
                yield return WaitUntil(() => !vis.IsActionLocked && !LocalPlayerState.IsStunned && SkillCooldowns.Ready(sk), 6);
                int f0 = CharFx.Flights, i0 = CharFx.Impacts;
                SkillCaster.TryCast(sk);
                yield return WaitUntil(() => CharFx.Flights > f0 || CharFx.Impacts > i0, 2);
                yield return new WaitForSecondsRealtime(0.12f);
                CloseTips(); yield return Shot($"Crowd_3_Chieu{++n}_{sk}");
                yield return WaitUntil(() => CharFx.Impacts > i0, 2);
                yield return new WaitForSecondsRealtime(0.1f);
                CloseTips(); yield return Shot($"Crowd_3_Chieu{n}_{sk}_No");
                yield return new WaitForSecondsRealtime(0.6f);
            }
            Check($"Dùng {n} chiêu cấp cao giữa đám đông (có ảnh lúc bay + lúc nổ)", n > 0);
            Check($"Boss nhận đòn của nhiều người (máu {(boss != null ? boss.GetCurrentHp() : 0)}/{(boss != null ? boss.GetMaxHp() : 0)})",
                boss != null && boss.GetCurrentHp() < boss.GetMaxHp());
            GameActions.Chat(1, "/killall");
            yield return new WaitForSecondsRealtime(1.8f);
            CloseTips();
            yield return Shot("Crowd_4_HaBoss");
        }
    }
}
#endif
