using System;
using System.Collections;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.UI;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// KIỂM THỬ TỰ ĐỘNG BẢN BUILD (smoke test) — chỉ chạy khi mở game với tham số dòng lệnh:
    ///   NinjaOnline.exe -autotest -user clientbot -pass test1234 [-batchmode -nographics] -logFile test.log
    ///
    /// Game tự: đăng nhập → chờ vào map → kiểm dữ liệu server gửi → mở từng cửa sổ UI → đánh quái
    /// → nói chuyện NPC → ghi "[AutoTest] PASS/FAIL ..." vào log → tự thoát (exit code = số lỗi).
    /// Mọi Exception trong lúc chạy cũng bị đếm là FAIL. Người chơi bình thường không bao giờ kích hoạt.
    /// </summary>
    public class AutoTestRunner : MonoBehaviour
    {
        private int _pass, _fail, _exceptions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") < 0) return;
            var go = new GameObject("[AutoTest]");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoTestRunner>();
        }

        private static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception) { _exceptions++; Debug.Log("[AutoTest] EXCEPTION " + msg); }
        }

        private void Check(string name, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Debug.Log($"[AutoTest] {(ok ? "PASS" : "FAIL")} {name}");
        }

        private static IEnumerator WaitUntil(Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Start()
        {
            Debug.Log("[AutoTest] Bắt đầu");
            // 1. Đăng nhập (dùng đúng đường đi của nút Đăng nhập)
            yield return WaitUntil(() => FindAnyObjectByType<GameAuthManager>() != null, 15);
            yield return new WaitForSecondsRealtime(2f); // để AppFlowManager kết nối + CHECK_VERSION
            var auth = FindAnyObjectByType<GameAuthManager>();
            Check("Có màn hình đăng nhập", auth != null);
            if (auth != null) auth.AutoLogin(Arg("-user", "clientbot"), Arg("-pass", "test1234"));

            yield return WaitUntil(() => LocalPlayerState.Id >= 0, 15);
            Check("Đăng nhập thành công", LocalPlayerState.Id >= 0);

            yield return WaitUntil(() => NetworkPlayerManager.Instance != null && NetworkPlayerManager.Instance.localPlayer != null, 20);
            Check("Nhân vật xuất hiện trong map", NetworkPlayerManager.Instance?.localPlayer != null);

            // 2. Dữ liệu server
            yield return WaitUntil(() => GameData.Items.Count > 0 && GameData.Skills.Count > 0 && GameData.Me.level > 0, 10);
            Check($"Nhận dữ liệu game (items={GameData.Items.Count}, skills={GameData.Skills.Count}, quests={GameData.Quests.Count})",
                GameData.Items.Count > 0 && GameData.Skills.Count > 0);
            Check("Nhận bảng nhân vật", GameData.Me.level > 0 && GameData.Me.expToNext > 0);
            yield return new WaitForSecondsRealtime(2f);
            Check("Thấy quái trong map", FindObjectsByType<Entities.MobController>(FindObjectsSortMode.None).Length > 0);

            // 3. Mở từng cửa sổ (bắt lỗi dựng UI)
            int before = _exceptions;
            GameWindow.Open<CharacterWindow>(); yield return null;
            GameWindow.Open<InventoryWindow>(); yield return null;
            GameWindow.Open<SkillWindow>(); yield return null;
            GameWindow.Open<QuestWindow>(); yield return null;
            Check("Mở 4 cửa sổ không lỗi", _exceptions == before);
            GameWindow.Get<CharacterWindow>().Hide(); GameWindow.Get<InventoryWindow>().Hide();
            GameWindow.Get<SkillWindow>().Hide(); GameWindow.Get<QuestWindow>().Hide();

            // 4. Nói chuyện NPC gần nhất → phải hiện hội thoại
            var dlg = GameWindow.Get<NpcDialogWindow>();
            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            Check("Nói chuyện NPC → hiện hội thoại", dlg.IsOpen);
            dlg.Hide();

            // 5. Đánh quái gần nhất
            int skill = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
            if (skill < 0 && GameData.MySkills.Count > 0) foreach (var k in GameData.MySkills.Keys) { skill = k; break; }
            CombatNetwork.Instance?.TryUseSkill(skill);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Bấm đánh không lỗi", _exceptions == before);

            Check("Không có Exception trong suốt phiên", _exceptions == 0);
            Debug.Log($"[AutoTest] KẾT QUẢ: {_pass} PASS, {_fail} FAIL");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(_fail);
        }
    }
}
