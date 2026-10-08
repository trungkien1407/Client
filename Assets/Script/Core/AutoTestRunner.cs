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

        /// <summary>
        /// Chụp màn hình (chỉ khi chạy có tham số "-shots &lt;thư mục&gt;", và KHÔNG dùng -nographics) để xem bố cục UI.
        /// </summary>
        private IEnumerator Shot(string name)
        {
            string dir = Arg("-shots", null);
            if (string.IsNullOrEmpty(dir)) yield break;
            yield return null;
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
            yield return null;
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
            if (auth != null)
            {
                auth.ShowLoginForm(Arg("-user", "clientbot"));
                yield return Shot("00_login"); // form đăng nhập + nút "Ghi nhớ đăng nhập"
                Check("Ghi nhớ đăng nhập mặc định bật", SavedLogin.Remember);
                auth.AutoLogin(Arg("-user", "clientbot"), Arg("-pass", "test1234"));
            }

            yield return WaitUntil(() => LocalPlayerState.Id >= 0, 15);
            Check("Đăng nhập thành công", LocalPlayerState.Id >= 0);
            Check("Mật khẩu ghi nhớ đã mã hoá (không lưu chữ thường) và đọc lại đúng",
                SavedLogin.StoredIsEncrypted(Arg("-pass", "test1234")) && SavedLogin.LoadPassword() == Arg("-pass", "test1234"));

            yield return WaitUntil(() => NetworkPlayerManager.Instance != null && NetworkPlayerManager.Instance.localPlayer != null, 20);
            Check("Nhân vật xuất hiện trong map", NetworkPlayerManager.Instance?.localPlayer != null);

            // 2. Dữ liệu server
            yield return WaitUntil(() => GameData.Items.Count > 0 && GameData.Skills.Count > 0 && GameData.Me.level > 0, 10);
            yield return null;
            GameWindow.Get<TutorialWindow>().Hide(); // hướng dẫn tân thủ tự hiện lần đầu — đóng để test tiếp
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

            // 3b. Cửa sổ GĐ2–GĐ4 (mở rỗng cũng không được lỗi)
            GameWindow.Open<SocialWindow>(); yield return null;
            GameWindow.Open<MailWindow>(); yield return null;
            GameWindow.Open<LeaderboardWindow>(); yield return null;
            GameWindow.Open<SettingsWindow>(); yield return null;
            GameWindow.Open<UpgradeWindow>(); yield return null;
            GameWindow.Open<StorageWindow>(); yield return null;
            GameWindow.Get<PlayerActionWindow>().ShowFor(0, "Test"); yield return null;
            Check("Mở 7 cửa sổ GĐ2–GĐ4 không lỗi", _exceptions == before);
            if (!string.IsNullOrEmpty(Arg("-shots", null)))
            {
                // Chụp từng cửa sổ riêng (đóng hết rồi mở 1 cái) để kiểm bố cục
                GameWindow[] all =
                {
                    GameWindow.Get<SocialWindow>(), GameWindow.Get<MailWindow>(), GameWindow.Get<LeaderboardWindow>(),
                    GameWindow.Get<SettingsWindow>(), GameWindow.Get<UpgradeWindow>(), GameWindow.Get<StorageWindow>(),
                    GameWindow.Get<PlayerActionWindow>(), GameWindow.Get<InventoryWindow>(), GameWindow.Get<TutorialWindow>(),
                    GameWindow.Get<CharacterWindow>(), GameWindow.Get<SkillWindow>(), // GĐ7: phòng thủ, cổng cấp kỹ năng
                };
                foreach (var w in all) w.Hide();
                yield return Shot("00_hud");
                foreach (var w in all)
                {
                    w.Show();
                    yield return new WaitForSecondsRealtime(0.6f); // chờ dữ liệu server (thư, xếp hạng...)
                    yield return Shot(w.GetType().Name);
                    w.Hide();
                }
                // Cặp cửa sổ cạnh nhau (giao dịch + túi) — kiểm vừa màn hình
                GameWindow.Get<TradeWindow>().OpenWithBag();
                yield return Shot("TradePair");
                GameWindow.Get<TradeWindow>().Hide(); GameWindow.Get<InventoryWindow>().Hide();
            }
            GameWindow.Get<SocialWindow>().Hide(); GameWindow.Get<MailWindow>().Hide(); GameWindow.Get<LeaderboardWindow>().Hide();
            GameWindow.Get<SettingsWindow>().Hide(); GameWindow.Get<UpgradeWindow>().Hide(); GameWindow.Get<StorageWindow>().Hide();
            GameWindow.Get<PlayerActionWindow>().Hide(); GameWindow.Get<InventoryWindow>().Hide();

            // 3c. Gói xã hội: xếp hạng + gia tộc + thư phải có phản hồi
            bool gotTop = false, gotGuild = false, gotMail = false;
            System.Action<DataKind> onData = k => { if (k == DataKind.Top) gotTop = true; if (k == DataKind.Guild) gotGuild = true; if (k == DataKind.Mails) gotMail = true; };
            GameData.OnChanged += onData;
            GameActions.Top(0); GameActions.GuildInfo(); GameActions.MailList();
            yield return WaitUntil(() => gotTop && gotGuild && gotMail, 5);
            GameData.OnChanged -= onData;
            Check("Nhận TOP_LIST / GUILD_INFO / MAIL_LIST", gotTop && gotGuild && gotMail);

            // 3d. Chat uGUI + chọn khu (GĐ5)
            var zw = GameWindow.Open<ZoneWindow>();
            yield return WaitUntil(() => zw.ZoneCount > 0, 5);
            Check($"Mở Chọn khu → nhận ZONE_LIST ({zw.ZoneCount} khu)", zw.ZoneCount > 0);
            yield return Shot("ZoneWindow");
            zw.Hide();
            GameWindow.Get<ChatWindow>().OpenAndFocus();
            ChatBox.Send(1, "", "xin chao tu autotest");
            int linesBefore = ChatBox.Lines.Count;
            yield return WaitUntil(() => ChatBox.Lines.Count > linesBefore, 5);
            Check("Gửi chat khu → nhận lại tin của mình", ChatBox.Lines.Count > linesBefore);
            yield return Shot("ChatWindow");
            GameWindow.Get<ChatWindow>().Hide();
            // Báo lỗi về server (ErrorReporter chỉ chạy ở bản build): kiểm ở logs/client_errors.log bên server
            Debug.LogError("[AutoTest] Lỗi thử — ErrorReporter phải gửi dòng này về server");
            Check("Túi đồ theo ô có sức chứa", GameData.BagCapacity > 0);

            // 4. Nói chuyện NPC gần nhất → phải hiện hội thoại
            var dlg = GameWindow.Get<NpcDialogWindow>();
            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            Check("Nói chuyện NPC → hiện hội thoại", dlg.IsOpen);
            dlg.Hide();

            // 4b. GĐ2–GĐ3 qua UI thật: Thợ Rèn → Nâng cấp / Rương đồ; bật-tắt Đồ sát (tài khoản test là GM)
            GameActions.Chat(1, "/item 10 1"); GameActions.Chat(1, "/item 40 5"); GameActions.Chat(1, "/yen 100000");
            // GĐ8: tới cạnh Thợ Rèn theo vị trí thật trong map (không ghi cứng toạ độ làng)
            var smith = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(2) : null;
            var sp = smith != null ? smith.transform.position : NetworkPlayerManager.Instance.localPlayer.transform.position;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            GameActions.Chat(1, $"/go {(sp.x + 0.6f).ToString(inv)} {(sp.y + 0.05f).ToString(inv)}");
            yield return new WaitForSecondsRealtime(1.5f);
            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            bool chose = dlg.Choose("Nâng cấp");
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

            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            dlg.Choose("Rương");
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

            // 5. Đánh quái gần nhất
            int skill = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
            if (skill < 0 && GameData.MySkills.Count > 0) foreach (var k in GameData.MySkills.Keys) { skill = k; break; }
            CombatNetwork.Instance?.TryUseSkill(skill);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Bấm đánh không lỗi", _exceptions == before);

            // 5b. GĐ8 — thế giới mới (map dựng từ tools/map_design.py): tên map (MAP_INFO), chữ chỉ đường ở cổng,
            //     dịch chuyển qua vài map để chụp ảnh kiểm tra bằng mắt.
            Check($"Nhận MAP_INFO → biết tên map đang đứng ({GameData.MapName})", !string.IsNullOrEmpty(GameData.MapName));
            Check("Có chữ chỉ đường ở cổng (PortalMarker)", GameObject.Find("PortalMarker") != null);
            foreach (var (mapId, mapName) in new[] { (7, "Thung Lũng Đá"), (13, "Y Thuật Đường"), (8, "Đầm Lầy Sương Mù"), (11, "Núi Tuyết") })
            {
                GameActions.Chat(1, "/tele " + mapId);
                yield return WaitUntil(() => GameData.MapName == mapName, 8);
                yield return new WaitForSecondsRealtime(1.5f); // chờ quái / NPC hiện + chữ tên map
                Check($"Sang map {mapId} ({mapName}) — nhận đúng tên map", GameData.MapName == mapName);
                yield return Shot("Map_" + mapId);
            }
            GameActions.Chat(1, "/tele 1");
            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);

            // 5c. GĐ9 — hoạt động hằng ngày, sổ tay nhiệm vụ, cẩm nang, mẹo theo cấp, hội thoại nhiều trang
            GameActions.Chat(1, "/daily");
            yield return new WaitForSecondsRealtime(0.5f);
            var act = GameWindow.Open<ActivityWindow>();
            yield return WaitUntil(() => GameData.Activities.Count > 0 && GameData.EventSchedule.Count > 0, 5);
            Check($"Hoạt động: {GameData.Activities.Count} việc, {GameData.Milestones.Count} mốc, lịch {GameData.EventSchedule.Count} dòng",
                GameData.Activities.Count == 7 && GameData.Milestones.Count == 5 && GameData.EventSchedule.Count >= 3);
            GameActions.Chat(1, "/act dungeon 2");
            yield return WaitUntil(() => GameData.ActivityPoints >= 20, 5);
            Check("Đủ 20 điểm → nút Hoạt động báo có rương", GameData.ActivityClaimable);
            yield return Shot("Activity");
            GameActions.ActivityClaim(0);
            yield return WaitUntil(() => GameData.MilestoneClaimed(0), 5);
            Check("Nhận mốc 20 điểm", GameData.MilestoneClaimed(0));
            act.Hide();

            var qw = GameWindow.Open<QuestWindow>();
            yield return WaitUntil(() => GameData.QuestGuide.Count > 0, 5);
            Check($"Sổ tay nhiệm vụ: {GameData.QuestGuide.Count} dòng có nơi đến", GameData.QuestGuide.Count > 0 && !string.IsNullOrEmpty(GameData.QuestGuide[0].where));
            yield return Shot("QuestGuide");
            qw.Hide();

            var gw = GameWindow.Get<GuideWindow>();
            gw.ShowTopic(7);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Mở Cẩm nang tới chủ đề Boss thế giới", gw.IsOpen);
            yield return Shot("Guide");
            gw.Hide();

            var tip = GameWindow.Get<TipWindow>();
            GameActions.Chat(1, "/exp 200000");   // vượt cấp 12 → server gửi GUIDE_TIP
            yield return WaitUntil(() => tip.IsOpen, 5);
            Check("Lên cấp mở tính năng → hiện khung Mẹo", tip.IsOpen);
            yield return Shot("Tip");
            tip.Hide();

            var hok = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(1) : null;
            if (hok != null)
            {
                var hkPos = hok.transform.position;
                GameActions.Chat(1, $"/go {(hkPos.x + 0.6f).ToString(inv)} {(hkPos.y + 0.05f).ToString(inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            dlg.Choose("Nhiệm vụ");
            yield return WaitUntil(() => dlg.IsOpen, 5);
            dlg.Choose("[Mới]");
            yield return WaitUntil(() => dlg.IsOpen && dlg.Text.Contains("(1/"), 5);
            Check("Nhận nhiệm vụ → hội thoại nhiều trang (1/N + Tiếp theo)", dlg.IsOpen && dlg.Text.Contains("(1/"));
            yield return Shot("DialogPage1");
            dlg.Choose("Tiếp");
            yield return WaitUntil(() => dlg.IsOpen && dlg.Text.Contains("Bạn:"), 5);
            Check("Trang 2: lời của người chơi", dlg.Text.Contains("Bạn:"));
            yield return Shot("DialogPage2");
            dlg.Hide();

            // 6. (chỉ khi có tham số -maint, đăng nhập bằng người chơi THƯỜNG — GM không bị đưa ra)
            //    Người kiểm thử hẹn bảo trì trên trang quản trị → thấy đồng hồ đếm ngược → hết giờ bị đưa ra với lý do.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-maint") >= 0)
            {
                Debug.Log("[AutoTest] CHỜ BẢO TRÌ — hẹn bảo trì trên trang quản trị trong vòng 90 giây");
                yield return WaitUntil(() => GameData.MaintEndTime > Time.time, 90);
                Check("Nhận đồng hồ đếm ngược bảo trì (SERVER_NOTICE loại 2)", GameData.MaintEndTime > Time.time);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("Maint_countdown");
                yield return WaitUntil(() => PopupAndLoad.Instance != null && (PopupAndLoad.Instance.LastMessage ?? "").Contains("bảo trì"), 90);
                yield return new WaitForSecondsRealtime(1f); // để màn đăng nhập mới dựng xong (Start chạy) rồi mới kiểm tra
                Check("Hết giờ: bị đưa ra, popup ghi lý do bảo trì và VẪN HIỆN trên màn đăng nhập",
                    (PopupAndLoad.Instance?.LastMessage ?? "").Contains("bảo trì") && PopupAndLoad.Instance.IsShowing);
                yield return Shot("Maint_kicked");
            }

            Check("Không có Exception trong suốt phiên", _exceptions == 0);
            Debug.Log($"[AutoTest] KẾT QUẢ: {_pass} PASS, {_fail} FAIL");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(_fail);
        }
    }
}
