using System;
using System.Collections;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Map;
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
            // 5g. GĐ10 — bục 1 chiều (Đồi Hoa Cúc: bục hàng 6, cột 30..36, đất dưới cao 4), minimap, bản đồ lớn, anim chiêu
            GameActions.Chat(1, "/tele 2 33.5 4.1");
            yield return WaitUntil(() => GameData.MapName == "Đồi Hoa Cúc", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            var me = NetworkPlayerManager.Instance.localPlayer;
            Check($"Map có lớp Platform (bục 1 chiều) có PlatformEffector2D",
                MapManager.Instance.currentMapInstance.transform.Find("Platform")?.GetComponent<PlatformEffector2D>() != null);
            me.GetComponent<Rigidbody2D>().velocity = new Vector2(0, 15f);      // nhảy thẳng lên từ dưới bục
            yield return new WaitForSecondsRealtime(2f);
            Check($"Nhảy xuyên bục từ dưới lên và đứng trên bục (y = {me.transform.position.y:0.00}, cần ~7)", Mathf.Abs(me.transform.position.y - 7f) < 0.3f);
            Check("Bấm xuống trên bục → rơi xuyên", me.TryDropThrough());
            yield return new WaitForSecondsRealtime(1.5f);
            Check($"Rơi về mặt đất dưới bục (y = {me.transform.position.y:0.00}, cần ~4)", Mathf.Abs(me.transform.position.y - 4f) < 0.3f);
            Check($"Minimap vẽ từ tilemap ({MapImage.Width}x{MapImage.Height})", MapImage.Texture != null && MapImage.Width == 76 && GameObject.Find("Minimap") != null);
            var wm = GameWindow.Open<WorldMapWindow>();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("WorldMap");
            wm.Hide();
            var vis = me.GetComponent<PlayerVisualController>();
            vis.PlaySkill(6);
            Check($"Chiêu Trảm Phong → anim {vis.mainSkeleton.AnimationName}", vis.mainSkeleton.AnimationName == "Skill20");
            yield return new WaitForSecondsRealtime(1f);
            vis.PlaySkill(1);
            string a1 = vis.mainSkeleton.AnimationName;
            yield return new WaitForSecondsRealtime(0.6f);
            vis.PlaySkill(1);
            Check($"Đòn cơ bản đấm – đá luân phiên ({a1} → {vis.mainSkeleton.AnimationName})", a1 != vis.mainSkeleton.AnimationName);
            yield return Shot("Platform");

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

            // 5d. GĐ9 — Chợ: tới Chủ Chợ → mở chợ (Hành trang tự mở cạnh) → treo bán 1 bình máu → thấy ở "Hàng của tôi" → huỷ
            GameActions.Chat(1, "/item 1 3");
            var seller = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(10) : null;
            Check("Có NPC Chủ Chợ ở Làng Lá", seller != null);
            if (seller != null)
            {
                var sp2 = seller.transform.position;
                GameActions.Chat(1, $"/go {(sp2.x + 0.6f).ToString(inv)} {(sp2.y + 0.05f).ToString(inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            var mk = GameWindow.Get<MarketWindow>();
            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            dlg.Choose("Chợ");
            yield return WaitUntil(() => mk.IsOpen, 5);
            Check("Chủ Chợ → mở cửa sổ Chợ + Hành trang", mk.IsOpen && GameWindow.Get<InventoryWindow>().IsOpen);
            yield return Shot("Market");
            int potIdx = GameData.Inventory.FindIndex(x => x.tpl == 1 && !x.locked);
            if (potIdx >= 0)
            {
                GameActions.MarketSell(potIdx, 1, 777);
                yield return WaitUntil(() => GameData.MarketMine.Exists(x => x.status == 0 && x.price == 777), 5);
            }
            var mine = GameData.MarketMine.Find(x => x.status == 0 && x.price == 777);
            Check("Treo bán bình máu 777 yên → hiện trong Hàng của tôi", mine != null);
            GameActions.MarketSearch(0, 0, 0, "");
            yield return WaitUntil(() => GameData.MarketRows.Exists(x => x.price == 777), 5);
            yield return Shot("MarketList");
            if (mine != null)
            {
                GameActions.MarketCancel(mine.id);
                yield return WaitUntil(() => !GameData.MarketMine.Exists(x => x.id == mine.id && x.status == 0), 5);
                Check("Huỷ bán → món biến khỏi danh sách", !GameData.MarketMine.Exists(x => x.id == mine.id && x.status == 0));
            }
            mk.Hide(); GameWindow.Get<InventoryWindow>().Hide();

            // 5e. GĐ9 — Phẩm chất + khảm ngọc: Găng Tinh Thiết 10% → Thợ Rèn → Khảm ngọc → khảm Xích Ngọc → mô tả có phẩm chất + ngọc
            GameActions.Chat(1, "/lv 20");
            GameActions.Chat(1, "/item 56 1");
            GameActions.Chat(1, "/item 90 2");
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == 56) && GameData.Inventory.Exists(x => x.tpl == 90), 5);
            GameActions.Chat(1, "/bonus " + GameData.Inventory.FindIndex(x => x.tpl == 56) + " 9");
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == 56 && x.bonus == 9), 5);
            Check("Ô đồ nhận phẩm chất 9% (định dạng ô đồ mới)", GameData.Inventory.Exists(x => x.tpl == 56 && x.bonus == 9));
            var smith2 = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(2) : null;
            if (smith2 != null)
            {
                var sp3 = smith2.transform.position;
                GameActions.Chat(1, $"/go {(sp3.x + 0.6f).ToString(inv)} {(sp3.y + 0.05f).ToString(inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            GameHud.TalkToNearestNpc();
            yield return WaitUntil(() => dlg.IsOpen, 5);
            dlg.Choose("Khảm ngọc");
            var gw2 = GameWindow.Get<GemWindow>();
            yield return WaitUntil(() => gw2.IsOpen, 5);
            Check("Thợ Rèn → mở cửa sổ Khảm ngọc", gw2.IsOpen);
            int eqI = GameData.Inventory.FindIndex(x => x.tpl == 56), gemI = GameData.Inventory.FindIndex(x => x.tpl == 90);
            if (eqI >= 0 && gemI >= 0) GameActions.GemSocket(eqI, gemI);
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == 56 && x.gems.Count == 1), 5);
            Check("Khảm Xích Ngọc vào Găng → ô đồ có 1 ngọc", GameData.Inventory.Exists(x => x.tpl == 56 && x.gems.Count == 1));
            yield return Shot("Gem");
            gw2.Hide();
            var gl = GameData.Inventory.Find(x => x.tpl == 56);
            string desc = gl != null ? InventoryWindow.Describe(gl) : "";
            Check("Mô tả trang bị có 'Phẩm chất' + 'Lỗ khảm 1/2'", desc.Contains("Phẩm chất") && desc.Contains("Lỗ khảm: 1/2"));
            var invw = GameWindow.Open<InventoryWindow>();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("BagQuality");
            invw.Hide();

            // 5f. GĐ9 — Quà online + giftcode (mã tạo bằng GM, mỗi lần chạy 1 mã mới)
            GameActions.Chat(1, "/daily");
            GameActions.Chat(1, "/onlinemin 12");
            var aw = GameWindow.Open<ActivityWindow>();
            yield return WaitUntil(() => GameData.OnlineGifts.Count == 4 && GameData.OnlineMinutesNow >= 10, 5);
            Check($"Hoạt động có 4 mốc quà online, đã online {GameData.OnlineMinutesNow} phút", GameData.OnlineGifts.Count == 4 && GameData.OnlineMinutesNow >= 10);
            GameActions.OnlineClaim(0);
            yield return WaitUntil(() => GameData.OnlineClaimedAt(0), 5);
            Check("Nhận quà online 10 phút", GameData.OnlineClaimedAt(0));
            string code = "AUTO" + (DateTime.Now.Ticks % 1000000);
            GameActions.Chat(1, $"/giftcode {code} 5 1");
            yield return new WaitForSecondsRealtime(0.8f);
            GameData.GiftcodeMessage = "";
            GameActions.GiftcodeUse(code.ToLower());
            yield return WaitUntil(() => GameData.GiftcodeMessage.Length > 0, 5);
            Check("Nhập giftcode → thành công", GameData.GiftcodeMessage.Contains("thành công"));
            yield return Shot("ActivityOnline");
            aw.Hide();

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

            // 7. GĐ11 — mất kết nối (GM tự kick mình) → màn đăng nhập mới → đăng nhập lại: HUD của scene phải hiện lại
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-maint") < 0)
            {
                var hudCanvas = GameObject.Find("HUDCanvas");
                Check("Trước khi rớt mạng: thấy HUD (máu / ô chiêu / nút đánh)", hudCanvas != null && hudCanvas.activeSelf);
                GameActions.Chat(1, "/kick " + LocalPlayerState.Name);
                yield return WaitUntil(() => LocalPlayerState.Id < 0, 10);
                Check("Bị ngắt kết nối → dọn phiên", LocalPlayerState.Id < 0 && Map.PortalMarkers.Current.Count == 0);
                yield return new WaitForSecondsRealtime(2f);        // màn đăng nhập mới dựng + tự kết nối lại
                var auth2 = FindAnyObjectByType<GameAuthManager>();
                Check("Hiện lại màn đăng nhập", auth2 != null);
                if (auth2 != null) auth2.AutoLogin(Arg("-user", "clientbot"), Arg("-pass", "test1234"));
                yield return WaitUntil(() => NetworkPlayerManager.Instance?.localPlayer != null && LocalPlayerState.Id >= 0, 25);
                Check("Đăng nhập lại vào map", NetworkPlayerManager.Instance?.localPlayer != null);
                yield return new WaitForSecondsRealtime(1.5f);
                Check("Sau khi đăng nhập lại HUD hiện lại", hudCanvas != null && hudCanvas.activeSelf);
                yield return Shot("Relogin");
            }

            Check("Không có Exception trong suốt phiên", _exceptions == 0);
            Debug.Log($"[AutoTest] KẾT QUẢ: {_pass} PASS, {_fail} FAIL");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(_fail);
        }
    }
}
