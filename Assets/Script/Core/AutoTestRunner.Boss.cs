#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System.Collections;
using System.Linq;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.Skill;
using UnityEngine;

namespace Assets.Script.Core
{
    // Autotest "-bossshots": gọi 3 boss thế giới, đứng đánh, chụp ảnh (dùng cho báo cáo / slide), rồi thoát.
    public partial class AutoTestRunner
    {
        /// <summary>
        /// Mỗi boss: GM /boss i → dịch chuyển tới cạnh boss (khu 1 — khu boss) → chọn boss → ra chiêu liên tục vài giây (GM /hp giữ máu)
        /// → chụp lúc đánh → /killall hạ boss → chụp đồ rơi + thông báo top sát thương.
        /// </summary>
        private IEnumerator BossShots()
        {
            var bosses = new[] { (i: 1, map: 2, name: "Đồi Hoa Cúc", x: 11.5f), (i: 2, map: 7, name: "Thung Lũng Đá", x: 44.5f), (i: 3, map: 11, name: "Núi Tuyết", x: 11.5f) };
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            GameActions.Chat(1, "/lv 30");   // cấp 30 trước khi đánh → không lên cấp giữa chừng (bảng "Mẹo" bật lên che ảnh)
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var b in bosses)
            {
                GameActions.Chat(1, "/boss " + b.i);
                yield return new WaitForSecondsRealtime(1f);
                GameActions.Chat(1, "/tele " + b.map + " " + (b.x - 3f).ToString("0.0", ci) + " 4.1");
                yield return WaitUntil(() => GameData.MapName == b.name, 8);
                yield return new WaitForSecondsRealtime(2f);
                var me = NetworkPlayerManager.Instance.localPlayer;
                var boss = NetworkMobManager.Instance != null ? NetworkMobManager.Instance.FindNearestAlive(me.transform.position, 20f) : null;
                Check($"Boss thế giới {b.i} xuất hiện ở {b.name}", boss != null);
                if (boss == null) continue;
                var targeting = FindAnyObjectByType<PlayerTargeting>();
                targeting?.SelectTarget(boss);
                CloseTips(); yield return Shot($"Boss{b.i}_1_XuatHien");

                var skills = GameData.MySkills.Keys.OrderByDescending(k => k).ToList();
                float until = Time.realtimeSinceStartup + 7f;
                int k = 0, shot = 0;
                while (Time.realtimeSinceStartup < until)
                {
                    GameActions.Chat(1, "/hp");
                    if (skills.Count > 0) SkillCaster.TryCast(skills[k++ % skills.Count]);
                    else CombatInput.CastSelected();
                    yield return new WaitForSecondsRealtime(0.45f);
                    if (shot == 0 && Time.realtimeSinceStartup > until - 5f) { shot++; CloseTips(); yield return Shot($"Boss{b.i}_2_DanhNhau"); }
                    if (shot == 1 && Time.realtimeSinceStartup > until - 2f) { shot++; CloseTips(); yield return Shot($"Boss{b.i}_3_DanhNhau"); }
                }
                GameActions.Chat(1, "/killall");
                yield return new WaitForSecondsRealtime(1.5f);
                CloseTips(); yield return Shot($"Boss{b.i}_4_HaGuc");
                yield return new WaitForSecondsRealtime(1f);
            }
            GameActions.Chat(1, "/tele 1");
            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);
        }

        /// <summary>Đóng mẹo / hướng dẫn tự bật (lên cấp, lần đầu vào game) để ảnh chụp không bị che.</summary>
        private static void CloseTips()
        {
            foreach (var w in new UI.Kit.GameWindow[] { UI.Kit.GameWindow.Get<UI.Windows.TipWindow>(), UI.Kit.GameWindow.Get<UI.Windows.TutorialWindow>() })
                if (w != null && w.IsOpen) w.Hide();
        }
    }
}
#endif
