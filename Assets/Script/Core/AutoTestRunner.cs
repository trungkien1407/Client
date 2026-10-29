using System;
using System.Collections;
using System.Collections.Generic;
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
        /// <summary>
        /// GĐ12 — hình theo dữ liệu server: quái có monster_template.art dùng bộ hình MobAnim, hiệu ứng chiêu (GAME_DATA_EFFECTS + kho ảnh)
        /// diễn được. Chỉ kiểm khi server có cấu hình hình (db/demo_art.sql hoặc hình riêng) — repo sạch thì bỏ qua.
        /// </summary>
        private IEnumerator CheckArtPipeline()
        {
            bool anyArt = false;
            foreach (var t in GameData.Mobs.Values) if (t.art >= 0) { anyArt = true; break; }
            if (anyArt)
            {
                int withArt = 0;
                foreach (var m in UnityEngine.Object.FindObjectsByType<Assets.Script.Entities.MobController>(FindObjectsSortMode.None))
                    if (m.AppliedArt >= 0) withArt++;
                Check($"Quái dùng bộ hình theo server (MobAnim): {withArt} con", withArt > 0);
            }
            // Quái thường hồi sinh thành Thủ lĩnh (ngẫu nhiên khi hồi sinh; GM /tinhanh để thử) → tên đỏ "[Thủ lĩnh]", CÙNG kích cỡ quái thường
            GameActions.Chat(1, "/tinhanh 2");
            Assets.Script.Entities.MobController leader = null;
            yield return WaitUntil(() =>
            {
                foreach (var m in UnityEngine.Object.FindObjectsByType<Assets.Script.Entities.MobController>(FindObjectsSortMode.None))
                    if (m.Rank == 2 && !m.IsDead) { leader = m; return true; }
                return false;
            }, 5);
            Assets.Script.Entities.MobController normal = null;   // cùng loại quái, hạng thường → so kích cỡ
            if (leader != null)
                foreach (var m in UnityEngine.Object.FindObjectsByType<Assets.Script.Entities.MobController>(FindObjectsSortMode.None))
                    if (m != leader && m.Rank == 0 && m.templateId == leader.templateId) { normal = m; break; }
            Check($"GM /tinhanh 2 → thấy quái Thủ lĩnh (hạng 2), cùng kích cỡ quái thường ({(leader != null ? leader.transform.localScale.x : 0):0.00} / {(normal != null ? normal.transform.localScale.x : 0):0.00})",
                leader != null && (normal == null || Mathf.Abs(leader.transform.localScale.x - normal.transform.localScale.x) < 0.01f));
            yield return Shot("LeaderMob");

            if (GameData.Effects.Count == 0) yield break;
            var me = NetworkPlayerManager.Instance.localPlayer.transform.position;
            int shown = 0;
            foreach (var s in GameData.Skills.Values)
            {
                if (s.fxHit >= 0) Fx.EffectPlayer.Play(s.fxHit, me + new Vector3(1.5f + shown * 1.6f, 0f, 0f));
                if (s.fxCast >= 0) Fx.EffectPlayer.Play(s.fxCast, me + new Vector3(1.5f + shown * 1.6f, 0f, 0f));
                if (s.fxHit >= 0 || s.fxCast >= 0) shown++;
                if (shown >= 6) break;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Check($"Hiệu ứng chiêu theo dữ liệu: {GameData.Effects.Count} mẫu, diễn thử {shown}", shown > 0 && GameObject.Find("Effects") != null);
            yield return Shot("SkillFx");
        }

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

        /// <summary>
        /// GĐ24 — cấp tối đa 120 + chiêu cấp 100 có vật bay Spine riêng: /lv 125 → kẹt ở 120 → học chiêu có vật bay của hệ
        /// (Vĩ Thú Ngọc / Băng Tiêu / Mũi Tên Indra) → đánh quái gần → vật bay phát rồi nổ ở mục tiêu. Xong trả cấp cũ.
        /// </summary>
        private IEnumerator CheckHighSkill(PlayerVisualController vis)
        {
            int skill = vis.ClassType == 1 ? 15 : vis.ClassType == 2 ? 17 : 23;
            if (!GameData.Skills.ContainsKey(skill)) { Debug.Log("[AutoTest] INFO server chưa chạy patch_gd24_chieu_cao — bỏ qua chiêu cấp cao"); yield break; }
            int oldLv = GameData.Me.level;
            GameActions.Chat(1, "/lv 125");
            yield return WaitUntil(() => GameData.Me.level >= 100, 4);
            Check($"Cấp tối đa 120 (/lv 125 → cấp {GameData.Me.level})", GameData.Me.level == 120);
            Check("Cấp tối đa: hết EXP lên cấp (expToNext = 0, HUD ghi \"cấp tối đa\")", GameData.Me.expToNext == 0);
            GameActions.Chat(1, "/sp 1");
            yield return new WaitForSecondsRealtime(0.5f);
            GameActions.UpgradeSkill(skill);
            yield return WaitUntil(() => GameData.MySkills.ContainsKey(skill), 4);
            Check($"Học chiêu cấp 100: {GameData.Skills[skill].name}", GameData.MySkills.ContainsKey(skill));

            // GĐ25 — biến thân: học + dùng chiêu biến thân của hệ → skin Spine đổi sang dạng biến thân, HUD có icon "BT" + đồng hồ
            int tf = vis.ClassType == 1 ? 24 : vis.ClassType == 2 ? 25 : 26;
            bool tfOn = false;
            if (GameData.Skills.ContainsKey(tf))
            {
                GameActions.Chat(1, "/sp 1");
                yield return new WaitForSecondsRealtime(0.5f);
                GameActions.UpgradeSkill(tf);
                yield return WaitUntil(() => GameData.MySkills.ContainsKey(tf), 4);
                yield return WaitUntil(() => !vis.IsActionLocked, 3);
                SkillCaster.TryCast(tf);
                yield return WaitUntil(() => vis.IsTransformed, 4);
                tfOn = vis.IsTransformed;
                Check($"Biến thân {GameData.Skills[tf].name} → skin Spine {vis.SkinName} (khác Normal)", tfOn && vis.SkinName != "Normal");
                Check($"HUD: icon biến thân + đồng hồ ({LocalPlayerState.EffectLeft(StatusEffect.Transform):0} giây)",
                    LocalPlayerState.EffectLeft(StatusEffect.Transform) > 10f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("BienThan");
            }
            var mob = NetworkMobManager.Instance?.FindNearestAlive(vis.transform.position, 60f);
            if (mob == null || !CharFx.Loaded(vis.FxSet)) Debug.Log("[AutoTest] INFO không có quái gần / bộ FX demo — bỏ qua vật bay");
            else if (GameData.MySkills.ContainsKey(skill))
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                ChatBox.Send(1, "", "/go " + (mob.transform.position.x - 3.5f).ToString("0.0", ci) + " " + (mob.transform.position.y + 0.5f).ToString("0.0", ci));
                yield return new WaitForSecondsRealtime(1.2f);
                FindAnyObjectByType<PlayerTargeting>()?.SelectTarget(mob);
                yield return WaitUntil(() => !LocalPlayerState.IsStunned && !vis.IsActionLocked, 3);
                int f0 = CharFx.Flights, i0 = CharFx.Impacts, h0 = CombatNetwork.MyMobHits;
                SkillCaster.TryCast(skill);
                yield return WaitUntil(() => CharFx.Flights > f0, 3);
                Check($"{GameData.Skills[skill].name}: vật bay Spine ({vis.MotionOf(skill).fly}) bay từ tay tới quái", CharFx.Flights > f0);
                yield return Shot("ChieuCao_Bay");
                yield return WaitUntil(() => CharFx.Impacts > i0, 3);
                Check($"{GameData.Skills[skill].name}: tới nơi mới nổ ({vis.MotionOf(skill).impact}) + server xác nhận trúng",
                    CharFx.Impacts > i0 && CombatNetwork.MyMobHits > h0);
                yield return Shot("ChieuCao_No");
            }
            if (tfOn)
            {
                float left = LocalPlayerState.EffectLeft(StatusEffect.Transform);
                yield return WaitUntil(() => !vis.IsTransformed, left + 3f);
                Check($"Hết giờ biến thân → tự về dạng thường (skin {vis.SkinName})", !vis.IsTransformed && vis.SkinName == "Normal");
            }
            GameActions.Chat(1, "/lv " + oldLv);
            yield return WaitUntil(() => GameData.Me.level == oldLv, 4);
            GameActions.Chat(1, "/hp");
            yield return new WaitForSecondsRealtime(0.5f);
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
                // 0. Các khung màn đăng nhập (dàn lại bằng Tools/Naruto/UI/4) — chụp + kiểm nối dây
                auth.ShowRegisterForm();
                yield return Shot("00_register");
                Check("Form đăng ký kiểm trước ở máy (sai tên / mật khẩu nhập lại lệch / email sai → báo lỗi; đúng → cho gửi)",
                    AuthMessages.CheckRegisterForm("ab", "123456", "123456", "a@b.cc") != null
                    && AuthMessages.CheckRegisterForm("abcd", "123456", "654321", "a@b.cc") != null
                    && AuthMessages.CheckRegisterForm("abcd", "123456", "123456", "abc") != null
                    && AuthMessages.CheckRegisterForm("abcd_1", "123456", "123456", "a@b.cc") == null);
                auth.ShowQuickLogin(Arg("-user", "clientbot"));
                yield return Shot("00_quick");
                auth.ShowCreateCharacter();
                var ccm = FindAnyObjectByType<CharacterCreationManager>(FindObjectsInactive.Include);
                yield return WaitUntil(() => ccm != null && ccm.buttonContainer.childCount >= 3
                    && ccm.spineContainer.GetComponentInChildren<Spine.Unity.SkeletonGraphic>() != null, 10);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("00_createchar");
                Check("Màn tạo nhân vật: 3 hệ phái + hình nhân vật + nút Tạo đã nối",
                    ccm != null && ccm.buttonContainer.childCount >= 3 && ccm.btnCreate != null
                    && ccm.spineContainer.GetComponentInChildren<Spine.Unity.SkeletonGraphic>() != null);
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
            // Thanh chiêu gán lúc vào game có thể trước GAME_DATA_SKILLS → phải vẽ lại theo icon_id của server (không giữ icon cũ trong SkillDatabase)
            {
                var slot = Skill.SkillBarManager.Instance != null ? System.Array.Find(Skill.SkillBarManager.Instance.skillSlots, s => s != null && s.assignedSkillId > 0) : null;
                int iconId = slot != null && GameData.Skills.TryGetValue(slot.assignedSkillId, out var stpl) ? stpl.iconId : -1;
                var wantIcon = ImageBank.Cached(iconId);
                Check($"Ô thanh chiêu hiện icon server (chiêu {slot?.assignedSkillId}, icon {iconId})",
                    slot != null && wantIcon != null && slot.iconImg != null && slot.iconImg.sprite == wantIcon);
            }
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

            // 3c. Mở cửa sổ → chặn đánh / di chuyển; chat thì không. Cặp cửa sổ: đóng 1 = đóng cả 2. Túi hiện Xu / Lượng rõ.
            {
                GameWindow.Get<PlayerActionWindow>().Hide();
                var bag = GameWindow.Get<InventoryWindow>();
                var set = GameWindow.Get<SettingsWindow>();
                foreach (var w in new GameWindow[] { GameWindow.Get<SocialWindow>(), GameWindow.Get<MailWindow>(), GameWindow.Get<LeaderboardWindow>(),
                                                     GameWindow.Get<UpgradeWindow>(), GameWindow.Get<StorageWindow>(), set, bag }) w.Hide();
                yield return null;
                bool freeBefore = !UIRoot.GameplayBlocked;
                GameWindow.Open<ChatWindow>(); yield return null;
                bool chatFree = !UIRoot.GameplayBlocked;
                GameWindow.Get<ChatWindow>().Hide();
                bag.Show(); yield return null;
                bool blocked = UIRoot.GameplayBlocked;
                Check($"Mở Hành trang → chặn đánh / di chuyển; không mở gì / chỉ mở chat → không chặn ({freeBefore}/{chatFree}/{blocked})",
                    freeBefore && chatFree && blocked);
                bool moneyOk = false;
                foreach (var t in bag.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                    if (t.name == "Money" && t.text.Contains("Xu") && t.text.Contains("Lượng") && t.fontSize >= 16) moneyOk = true;
                Check("Hành trang hiện dải Yên · Xu · Lượng chữ to", moneyOk);
                set.Show();
                GameWindow.SideBySide(set, bag);
                bag.Hide(); yield return null;
                Check("Cặp cửa sổ (… | Hành trang): đóng 1 cái → đóng cả 2", !set.IsOpen && !bag.IsOpen && !UIRoot.GameplayBlocked);
            }

            // 3d. Hình NPC theo server (npc_template.art — demo: db/demo_art.sql) + mốc cổng có mũi tên
            {
                bool anyArt = false;
                foreach (var a in GameData.NpcArt.Values) if (a >= 0) { anyArt = true; break; }
                if (anyArt && NetworkNpcManager.Instance != null)
                {
                    int withArt = 0, total = 0;
                    foreach (var npc in NetworkNpcManager.Instance.All) { total++; if (npc != null && npc.AppliedArt >= 0) withArt++; }
                    Check($"NPC dùng hình riêng theo server: {withArt}/{total}", total > 0 && withArt == total);
                }
                var marker = GameObject.Find("PortalMarker");
                Check("Cổng chuyển map có mũi tên + tên map", marker != null && marker.transform.Find("Arrow") != null && marker.transform.Find("Name") != null);
            }
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
            Assets.Script.Data.NpcNetwork.TalkToNearest();
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
            Assets.Script.Data.NpcNetwork.TalkToNearest();
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

            Assets.Script.Data.NpcNetwork.TalkToNearest();
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
            SkillCaster.TryCast(skill);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Bấm đánh không lỗi", _exceptions == before);

            // 5a. Enter (ActionInput): chọn quái ở xa → tự chạy tới rồi đánh
            var meMove = NetworkPlayerManager.Instance?.localPlayer;
            var farMob = meMove != null ? NetworkMobManager.Instance?.FindNearestAlive(meMove.transform.position, 30f) : null;
            if (farMob != null)
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;   // máy tiếng Việt viết "1,5" — server cần "1.5"
                // Quái đi lang thang: chờ xong mà nó đã tới quá gần (Enter đánh luôn, không cần chạy) → dịch ra xa lại, tối đa 3 lần
                for (int tries = 0; tries < 3; tries++)
                {
                    ChatBox.Send(1, "", "/go " + (farMob.transform.position.x - 5f).ToString("0.0", ci) + " " + (farMob.transform.position.y + 0.5f).ToString("0.0", ci));
                    yield return new WaitForSecondsRealtime(1.2f);
                    if (farMob.DistanceFrom(meMove.transform.position) > 3.5f) break;
                }
                FindAnyObjectByType<PlayerTargeting>()?.SelectTarget(farMob);
                float d0 = farMob.DistanceFrom(meMove.transform.position);
                string where0 = $"người x={meMove.transform.position.x:0.0} y={meMove.transform.position.y:0.0}, quái x={farMob.transform.position.x:0.0}";
                ActionInput.Act();
                bool started = ActionInput.IsRunning;
                yield return WaitUntil(() => !ActionInput.IsRunning, 10);
                float d1 = farMob.DistanceFrom(meMove.transform.position);
                Check($"Enter: quái ở xa → tự chạy lại gần ({d0:0.0} → {d1:0.0}; lúc đầu {where0})", started && d1 < d0 - 1f);
                yield return Shot("AutoRun");
            }

            // 5a1. Hồi chiêu do CLIENT tự đếm: đánh → bắt đầu đếm + ô kỹ năng tối lại; bấm lại khi chưa hồi → KHÔNG gửi USE_SKILL
            int cdSkill = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
            float cdLen = SkillCooldowns.CooldownOf(cdSkill);
            Check($"Chiêu đang chọn có thời gian hồi ({cdLen:0.0} giây)", cdSkill >= 0 && cdLen > 0);
            if (cdSkill >= 0 && cdLen > 0)
            {
                yield return new WaitForSecondsRealtime(cdLen + 0.3f);   // hết hồi đòn trước
                // Đứng cạnh 1 con quái CÒN SỐNG (ca trước có thể đã hạ con kia → Enter chỉ vung suông, không gửi gói)
                var cdMob = NetworkMobManager.Instance?.FindNearestAlive(meMove.transform.position, 40f);
                if (cdMob != null)
                {
                    var ciCd = System.Globalization.CultureInfo.InvariantCulture;
                    ChatBox.Send(1, "", "/go " + (cdMob.transform.position.x - 1.2f).ToString("0.0", ciCd) + " " + (cdMob.transform.position.y + 0.5f).ToString("0.0", ciCd));
                    yield return new WaitForSecondsRealtime(1.2f);
                    FindAnyObjectByType<PlayerTargeting>()?.SelectTarget(cdMob);
                }
                // quái (có thể là Thủ lĩnh — mỗi lần hồi sinh đều có cơ hội) đánh trả, 20% choáng 1 giây → chờ hết choáng mới bấm
                yield return WaitUntil(() => !LocalPlayerState.IsStunned, 3);
                int sent0 = GameActions.UseSkillSent;
                ActionInput.Act();                                      // quái gần → đánh (xa thì tự chạy rồi đánh)
                yield return WaitUntil(() => GameActions.UseSkillSent > sent0, 6);
                Check("Đánh → gửi USE_SKILL + client bắt đầu đếm hồi chiêu", GameActions.UseSkillSent == sent0 + 1 && !SkillCooldowns.Ready(cdSkill));
                yield return null; yield return null;   // SkillSlotUI.Update vẽ lớp tối
                bool shown = false;
                foreach (var slot in FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None)) if (slot.assignedSkillId == cdSkill && slot.ShowingCooldown) shown = true;
                Check("Ô kỹ năng hiện lớp hồi chiêu", shown);
                yield return new WaitForSecondsRealtime(0.32f);         // qua nhịp chống spam 0,3 giây, vẫn trong thời gian hồi
                int sent1 = GameActions.UseSkillSent;
                if (!SkillCooldowns.Ready(cdSkill)) SkillCaster.TryCast(cdSkill);
                Check($"Bấm lại khi chưa hồi (còn {SkillCooldowns.Remaining(cdSkill):0.00} giây) → không gửi USE_SKILL", GameActions.UseSkillSent == sent1);
                yield return WaitUntil(() => SkillCooldowns.Ready(cdSkill), cdLen + 1);
                Check("Hết thời gian hồi → dùng được lại", SkillCooldowns.Ready(cdSkill));
                // Chụp liên tiếp lúc nhân vật ra chiêu thật (chỉ khi chạy -shots): xem hiệu ứng ra chiêu / trúng đòn đúng chỗ, đúng hướng
                if (!string.IsNullOrEmpty(Arg("-shots", null)))
                {
                    ActionInput.Act();
                    for (int k = 0; k < 5; k++) { yield return Shot("Cast_" + k); yield return new WaitForSecondsRealtime(0.1f); }
                }
            }

            // 5a2. Tự chọn mục tiêu (Đồi Hoa Cúc — quái rải khắp map): đứng cạnh quái A → máy tự chọn A;
            //      B cách A 7,5–11 ô: ngoài tầm tự chọn (6) nhưng chưa tới mức bỏ chọn (12) → chỉ luật "tự chuyển" mới đổi
            //      dịch tới cạnh quái B ở xa → A ra khỏi tầm → tự chuyển sang quái khác
            GameActions.Chat(1, "/tele 2");
            yield return WaitUntil(() => GameData.MapName == "Đồi Hoa Cúc", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            var tg = FindAnyObjectByType<PlayerTargeting>();
            meMove = NetworkPlayerManager.Instance?.localPlayer;
            Entities.MobController tgA = null, tgB = null;
            if (tg != null && meMove != null && NetworkMobManager.Instance != null)
                foreach (var m1 in NetworkMobManager.Instance.All)
                {
                    if (m1 == null || m1.IsDead) continue;
                    foreach (var m2 in NetworkMobManager.Instance.All)
                        if (m2 != null && !m2.IsDead && Mathf.Abs(m2.transform.position.x - m1.transform.position.x) is > 7.5f and < 11f) { tgA = m1; tgB = m2; break; }
                    if (tgA != null) break;
                }
            Check("Đồi Hoa Cúc có 2 quái cách nhau 7,5–11 ô để thử tự chọn mục tiêu", tgA != null);
            if (tgA != null)
            {
                var ci2 = System.Globalization.CultureInfo.InvariantCulture;
                tg.ClearTarget();
                ChatBox.Send(1, "", "/go " + tgA.transform.position.x.ToString("0.0", ci2) + " " + (tgA.transform.position.y + 0.5f).ToString("0.0", ci2));
                yield return WaitUntil(() => tg.currentTarget is Entities.MobController && tg.IsAutoPicked, 4);
                var picked = tg.currentTarget as Entities.MobController;
                Check($"Tự chọn mục tiêu: đứng cạnh quái → chọn quái gần nhất ({(picked != null ? picked.GetTargetName() : "không có")})", picked != null && tg.IsAutoPicked);
                yield return Shot("AutoTarget");
                ChatBox.Send(1, "", "/go " + tgB.transform.position.x.ToString("0.0", ci2) + " " + (tgB.transform.position.y + 0.5f).ToString("0.0", ci2));
                yield return WaitUntil(() => tg.currentTarget is Entities.MobController cc && cc != picked, 4);
                var c2 = tg.currentTarget as Entities.MobController;
                float dc = c2 != null ? c2.DistanceFrom(meMove.transform.position) : -1f;
                Check($"Ra khỏi tầm → tự chuyển sang quái khác ({(c2 != null ? c2.GetTargetName() : "không có")}, cách {dc:0.0})", c2 != null && c2 != picked && tg.IsAutoPicked);
            }

            // 5b. GĐ8 — thế giới mới (14 map prefab): tên map (MAP_INFO), chữ chỉ đường ở cổng,
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
                if (mapId == 7) yield return CheckArtPipeline();
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
            // Đứng yên lâu → Rigidbody2D ngủ; trước đây bấm xuống lúc này không rơi (bỏ va chạm nhưng thân vẫn treo)
            me.GetComponent<Rigidbody2D>().Sleep();
            yield return new WaitForSecondsRealtime(0.2f);
            Check("Bấm xuống trên bục (đang đứng yên lâu) → rơi xuyên", me.TryDropThrough());
            yield return new WaitForSecondsRealtime(1.5f);
            Check($"Rơi về mặt đất dưới bục (y = {me.transform.position.y:0.00}, cần ~4)", Mathf.Abs(me.transform.position.y - 4f) < 0.3f);
            Check($"Minimap vẽ từ tilemap ({MapImage.Width}x{MapImage.Height})", MapImage.Texture != null && MapImage.Width == 76 && GameObject.Find("Minimap") != null);
            var wm = GameWindow.Open<WorldMapWindow>();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("WorldMap");
            wm.Hide();
            var vis = me.GetComponent<PlayerVisualController>();
            int bigSkill = vis.ClassType == 2 ? 7 : vis.ClassType == 3 ? 8 : 6;   // chiêu cấp 5 của hệ
            string want = PlayerVisualController.SkillAnimFor(vis.ClassType, bigSkill);
            vis.PlaySkill(bigSkill);
            Check($"Chiêu {bigSkill} → anim chiêu trong Spine ({vis.mainSkeleton.AnimationName}, cần {want}) — hiệu ứng vẽ sẵn, không phát thêm fxCast",
                want != null && vis.mainSkeleton.AnimationName == want && vis.HasSkillAnim(bigSkill));
            Check($"Chiêu có mốc trúng đòn sau lúc bấm ({vis.HitTime - Time.time:0.00} giây)", vis.HitTime > Time.time);
            // Đánh theo nhịp anim: đang diễn chiêu lớn mà bấm đòn cơ bản → không cắt ngang anim
            SkillCaster.TryCast(1);
            Check($"Đang diễn chiêu → bấm đánh không cắt ngang ({vis.mainSkeleton.AnimationName})", vis.mainSkeleton.AnimationName == want && vis.IsActionLocked);
            yield return new WaitUntil(() => !vis.IsActionLocked);
            yield return new WaitForSecondsRealtime(0.1f);
            vis.PlaySkill(1);
            string a1 = vis.mainSkeleton.AnimationName;
            yield return new WaitForSecondsRealtime(0.65f);
            vis.PlaySkill(1);
            if (vis.ClassType != 2)
                Check($"Đòn cơ bản đấm – đá luân phiên ({a1} → {vis.mainSkeleton.AnimationName})", a1 != vis.mainSkeleton.AnimationName);
            Check("Đòn cơ bản không phát thêm hiệu ứng ra chiêu (đường chém xanh) — đấm / đá đã có hiệu ứng trong Spine",
                vis.HasBuiltInCastFx(1) && vis.HasBuiltInCastFx(2) && vis.HasBuiltInCastFx(3));

            // Cả 3 hệ: mọi chiêu (trừ bị động) đều tìm được anim có thật trong bộ Spine; chiêu lạ → anim dự phòng theo loại
            int myClass = vis.ClassType;
            for (int cls = 1; cls <= 3; cls++)
            {
                var load = PlayerSpawner.LoadSkeleton(cls);
                yield return new WaitUntil(() => load.IsCompleted);
                vis.SetupVisual(load.Result, null, cls);
                var missing = new List<string>();
                foreach (var s in GameData.Skills.Values)
                {
                    if ((s.classId != 0 && s.classId != cls) || s.type == 2) continue;
                    var m = vis.ResolveMotion(s.id);
                    if (m.anim == null || vis.mainSkeleton.Skeleton.Data.FindAnimation(m.anim) == null) missing.Add(s.name);
                }
                Check($"Hệ {cls}: mọi chiêu có anim ({(missing.Count == 0 ? "đủ" : "thiếu " + string.Join(", ", missing))})", missing.Count == 0);
                string basic = vis.ResolveMotion(cls).anim;
                Check($"Hệ {cls}: đòn cơ bản → {basic}", cls == 2 ? basic == AnimNames.Throw : basic == AnimNames.Punch || basic == AnimNames.Kick);
                Check($"Hệ {cls}: chiêu chưa có trong bảng → anim dự phòng ({vis.ResolveMotion(9999).anim})", vis.ResolveMotion(9999).anim != null);
                // GĐ24: vật bay / vụ nổ khai trong bảng có thật trong bộ <Tên>FX (thiếu bộ FX demo → bỏ qua, chiêu vẫn chạy)
                yield return WaitUntil(() => Fx.CharFx.Loaded(vis.FxSet), 5);
                if (Fx.CharFx.Loaded(vis.FxSet))
                {
                    var badFx = new List<string>();
                    foreach (var s in GameData.Skills.Values)
                    {
                        if (s.classId != cls) continue;
                        var m = vis.ResolveMotion(s.id);
                        if (m.fly != null && !Fx.CharFx.Has(vis.FxSet, m.fly)) badFx.Add(s.name + ":" + m.fly);
                        if (m.impact != null && !Fx.CharFx.Has(vis.FxSet, m.impact)) badFx.Add(s.name + ":" + m.impact);
                    }
                    Check($"Hệ {cls}: hiệu ứng Spine của chiêu có trong {vis.FxSet} ({(badFx.Count == 0 ? "đủ" : "thiếu " + string.Join(", ", badFx))})", badFx.Count == 0);
                }
                else Debug.Log($"[AutoTest] INFO Hệ {cls}: chưa có bộ {vis.FxSet} (demo art) — bỏ qua kiểm vật bay");
            }
            {
                var back = PlayerSpawner.LoadSkeleton(myClass);
                yield return new WaitUntil(() => back.IsCompleted);
                vis.SetupVisual(back.Result, null, myClass);
            }
            yield return Shot("Platform");
            yield return CheckHighSkill(vis);

            // 5h. Nước (Đầm Lầy Sương Mù, hồ 2: ô nước x 41..45, mặt nước y = 4, đáy y = 3): chạy → đi trên mặt nước,
            //     đứng yên → chìm, nhảy + chạy → lên bờ. Trước 2026-10-22 nước không có collider / tag → rơi qua như không khí.
            GameActions.Chat(1, "/tele 8 38.5 4.1");
            yield return WaitUntil(() => GameData.MapName == "Đầm Lầy Sương Mù", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            GameActions.Chat(1, "/killall");                       // quái chủ động gần hồ không quấy
            yield return new WaitForSecondsRealtime(0.5f);
            me = NetworkPlayerManager.Instance.localPlayer;
            var waterObj = MapManager.Instance.currentMapInstance.transform.Find("Water");
            Check("Lớp Water có TilemapCollider2D (Is Trigger) + tag Water",
                waterObj != null && waterObj.CompareTag("Water") && waterObj.GetComponent<TilemapCollider2D>() is TilemapCollider2D wc && wc.isTrigger);
            me.AutoMoveX = 1f;
            yield return WaitUntil(() => me.transform.position.x > 43f, 4);
            float ySurface = me.transform.position.y;
            yield return new WaitForSecondsRealtime(0.3f);   // hình trượt xuống ngang sóng
            float sink = me.GetComponent<PlayerVisualController>().WaterSink;
            yield return Shot("Water");
            Check($"Đi trên nước: hình hạ {sink:0.00} ô cho chân chạm ngọn sóng (Map/WaterLine {Map.WaterLine.Sink:0.00})", sink > Map.WaterLine.Sink - 0.03f);
            Check($"Chạy vào nước → đi trên mặt nước (x = {me.transform.position.x:0.0}, y = {ySurface:0.00}, cần ~4)", me.transform.position.x > 43f && Mathf.Abs(ySurface - 4f) < 0.25f);
            me.AutoMoveX = 0f;
            yield return new WaitForSecondsRealtime(2f);
            Check($"Đứng yên trên mặt nước → chìm xuống đáy hồ (y = {me.transform.position.y:0.00}, cần ~3)", Mathf.Abs(me.transform.position.y - 3f) < 0.3f);
            me.AutoMoveX = 1f;
            me.Jump();
            yield return WaitUntil(() => me.transform.position.x > 47f && me.IsGrounded, 5);
            me.AutoMoveX = 0f;
            Check($"Nhảy + chạy → lên bờ bên kia (x = {me.transform.position.x:0.0}, y = {me.transform.position.y:0.00})", me.transform.position.x > 47f && Mathf.Abs(me.transform.position.y - 4f) < 0.3f);

            // Ra chiêu (anim Spine có vẽ sẵn hiệu ứng) rồi đổi map ngay → sang map mới KHÔNG diễn tiếp chiêu cũ
            // (lỗi cũ: nhân vật bị ẩn lúc tải map, anim dừng giữa chừng → lốc xoáy đỏ Trảm Phong "đi theo" sang map mới)
            var pvc5 = me.GetComponent<PlayerVisualController>();
            bool castAnim = pvc5 != null && pvc5.PlayActionOnce("Skill10");
            GameActions.Chat(1, "/tele 1");
            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);
            yield return new WaitForSecondsRealtime(0.3f);
            var track5 = pvc5 != null ? pvc5.mainSkeleton.AnimationState.GetCurrent(0) : null;
            Check($"Ra chiêu rồi đổi map → map mới không diễn tiếp chiêu cũ (anim: {track5?.Animation?.Name})",
                castAnim && track5 != null && track5.Animation.Name != "Skill10");

            // 5i. Hình trên thân (Fx.BodyAura, vẽ bằng code): /effect tự dính choáng / bỏng → sao trên đầu, tàn lửa.
            //     Mặc vũ khí +15 → server tính hào quang bậc 3 (PLAYER_PVP_INFO) → vòng đỏ dưới chân + viền sáng ô đồ. Xong trả vũ khí cũ.
            yield return new WaitForSecondsRealtime(1f);
            me = NetworkPlayerManager.Instance.localPlayer;
            GameActions.Chat(1, "/effect 1 3");
            yield return WaitUntil(() => me.TryGetComponent(out BodyAura a1) && a1.Has(StatusEffect.Stun), 5);
            me.TryGetComponent(out BodyAura aura5i);
            Check("/effect 1 → sao choáng xoay trên đầu (BodyAura)", aura5i != null && aura5i.Has(StatusEffect.Stun));
            GameActions.Chat(1, "/effect 3 3");
            yield return WaitUntil(() => aura5i != null && aura5i.Has(StatusEffect.Burn), 5);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("StatusAura");
            Check("/effect 3 → bỏng: thân nhấp nháy cam + tàn lửa", aura5i != null && aura5i.Has(StatusEffect.Burn));

            int wpn = -1, wpnReq = int.MaxValue;   // vũ khí cấp thấp nhất hệ mình mặc được
            foreach (var it in GameData.Items.Values)
                if (it.slot == 1 && (it.classRequire == 0 || it.classRequire == GameData.ClassType) && it.levelRequire <= GameData.Me.level && it.levelRequire < wpnReq) { wpn = it.id; wpnReq = it.levelRequire; }
            GameData.Equipment.TryGetValue(1, out int oldWpn);
            GameActions.Chat(1, $"/item {wpn} 1 15");
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == wpn && x.level == 15), 5);
            GameActions.Equip(GameData.Inventory.FindIndex(x => x.tpl == wpn && x.level == 15));
            yield return WaitUntil(() => GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv) && pv.glow == 3, 5);
            Check($"Mặc {GameData.ItemName(wpn)} +15 → server báo hào quang bậc 3", GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv3) && pv3.glow == 3);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Vòng hào quang đỏ dưới chân hiện", aura5i != null && aura5i.GlowTier == 3 && aura5i.RingVisible);
            yield return Shot("Glow");
            var invGlow = GameWindow.Open<InventoryWindow>();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Ô vũ khí +15 trong Hành trang có viền sáng (ItemGlow)", invGlow.GetComponentsInChildren<ItemGlow>(true).Length > 0);
            yield return Shot("WeaponGlow");
            invGlow.Hide();
            int oldIdx = oldWpn > 0 ? GameData.Inventory.FindIndex(x => x.tpl == oldWpn) : -1;
            if (oldIdx >= 0) GameActions.Equip(oldIdx); else GameActions.Unequip(1);
            // Server gửi PLAYER_PVP_INFO trước EQUIPMENT → chờ ô vũ khí đổi xong rồi mới so bậc
            yield return WaitUntil(() => oldWpn > 0 ? GameData.Equipment.TryGetValue(1, out int w1) && w1 == oldWpn : !GameData.Equipment.ContainsKey(1), 5);
            yield return new WaitForSecondsRealtime(0.3f);
            GameData.EquipLevel.TryGetValue(1, out int oldLv);
            Check("Trả vũ khí cũ → hào quang về đúng bậc của nó", GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv1) && pv1.glow == GameData.GlowTier(oldLv));

            // 5i2. GĐ23 — ô Nón (slot 10): mặc Nón Vải (id 123, cấp 5) → server trả EQUIPMENT có ô 10; Hành trang vẽ đủ 10 ô
            GameActions.Chat(1, "/item 123 1");
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == 123), 5);
            GameActions.Equip(GameData.Inventory.FindIndex(x => x.tpl == 123));
            yield return WaitUntil(() => GameData.Equipment.TryGetValue(10, out int h0) && h0 == 123, 5);
            Check("Mặc Nón Vải → ô trang bị 10 (Nón)", GameData.Equipment.TryGetValue(10, out int hat) && hat == 123);
            var invHat = GameWindow.Open<InventoryWindow>();
            yield return new WaitForSecondsRealtime(0.5f);
            int eqCells = 0;
            foreach (var bt in invHat.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (bt.name.StartsWith("Slot")) eqCells++;
            Check($"Hành trang vẽ đủ 10 ô trang bị (đếm được {eqCells})", eqCells == GameData.EquipSlotCount);
            yield return Shot("EquipHat");
            invHat.Hide();
            GameActions.Unequip(10);
            yield return WaitUntil(() => !GameData.Equipment.ContainsKey(10), 5);

            // 5j. Bình thuốc (GĐ18): Đan Hồi Chakra (type 3) + Đan Hồi Phục (type 2) có hồi chiêu RIÊNG → dùng liền nhau đều ăn;
            //     phím Q / icon "HP" tự chọn bình (trước đây chỉ Bình Nhỏ id 1); icon HP / MP trên HUD là nút thật.
            GameActions.Chat(1, "/item 24 2"); GameActions.Chat(1, "/item 3 2"); GameActions.Chat(1, "/item 5 1");   // Bình Máu Lớn (cấp 10)
            yield return WaitUntil(() => GameData.CountItem(24) >= 2 && GameData.CountItem(3) >= 2 && GameData.CountItem(5) >= 1, 5);
            int dan24 = GameData.CountItem(24), dan3 = GameData.CountItem(3);
            GameActions.UseItem(24);
            yield return WaitUntil(() => GameData.CountItem(24) == dan24 - 1, 5);
            GameActions.UseItem(3);
            yield return WaitUntil(() => GameData.CountItem(3) == dan3 - 1, 5);
            Check($"Đan Hồi Chakra rồi Đan Hồi Phục liền nhau → cả 2 được dùng (đan chakra {dan24}→{GameData.CountItem(24)}, đan máu {dan3}→{GameData.CountItem(3)})",
                GameData.CountItem(24) == dan24 - 1 && GameData.CountItem(3) == dan3 - 1);
            GameActions.Chat(1, "/hp");
            yield return new WaitForSecondsRealtime(0.5f);
            int pickFull = QuickPotion.Pick(true);
            Check($"Máu đầy → phím Q chọn bình nhỏ nhất ({GameData.ItemName(pickFull)})", pickFull == ItemIds.HpPotion);
            LocalPlayerState.Hp = 100;   // giả máu còn ít (chỉ phía client) → phải chọn bình to nhất hiện có
            int pickLow = QuickPotion.Pick(true);
            LocalPlayerState.Hp = LocalPlayerState.MaxHp;
            Check($"Máu thiếu nhiều → chọn bình to ({GameData.ItemName(pickLow)})", GameData.Items.TryGetValue(pickLow, out var pt) && pt.hpRestore >= 300);
            GameObject hpIcon = null;
            var hudCv = GameObject.Find("HUDCanvas");
            if (hudCv != null) foreach (var b in hudCv.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (b.name == "HP") hpIcon = b.gameObject;
            Check("Icon bình HP trên HUD là nút bấm được (có số lượng)", hpIcon != null && hpIcon.GetComponent<UnityEngine.UI.Button>() != null && hpIcon.GetComponentInChildren<TMPro.TextMeshProUGUI>() != null);

            // 5k. GĐ22 — Tự động (AutoPlay, phím Z): KHÔNG có "Tự động luyện tập" → chạy bộ tìm + đánh quái; có (hạn 1 ngày tính
            //     từ lúc vào túi) → dịch chuyển tới quái; tự uống bình chakra dưới ngưỡng (cả khi tắt tự động); tự nhặt đồ rơi.
            //     Tuỳ chọn tự động lưu trên máy → chạy xong trả lại như cũ.
            {
                bool auFind = AutoSettings.FindMobs, auPick = AutoSettings.PickItems;
                bool auHp = AutoSettings.PotionHp, auMp = AutoSettings.PotionMp;
                int auMpPct = AutoSettings.MpPercent;
                var auCi = System.Globalization.CultureInfo.InvariantCulture;
                AutoSettings.FindMobs = true; AutoSettings.PickItems = false;
                AutoSettings.PotionHp = false; AutoSettings.PotionMp = false;
                GameActions.Chat(1, "/tele 2");   // Đồi Hoa Cúc: 10 quái cấp thấp, cổng ở x 0,5 và 75,5
                yield return WaitUntil(() => GameData.MapName == "Đồi Hoa Cúc", 8);
                yield return new WaitForSecondsRealtime(1.5f);
                GameActions.Chat(1, "/hp");
                var auMe = NetworkPlayerManager.Instance.localPlayer;
                Check("Chưa có Tự động luyện tập → tự động sẽ chạy bộ", GameData.AutoTrainSecondsLeft() < 0);

                int auHits = CombatNetwork.MyMobHits, auTele = AutoPlay.TeleRequests;
                AutoPlay.Instance.SetOn(true, null);
                yield return WaitUntil(() => CombatNetwork.MyMobHits > auHits, 20);
                Check($"Tự động chạy bộ → tự tìm quái + đánh ({CombatNetwork.MyMobHits - auHits} đòn trúng, không xin dịch chuyển)",
                    CombatNetwork.MyMobHits > auHits && AutoPlay.TeleRequests == auTele && AutoPlay.IsOn);
                yield return Shot("AutoWalk");
                AutoPlay.Instance.SetOn(false, null);
                yield return null;
                Check("Tắt tự động → thôi tự chạy", !AutoPlay.IsOn && Mathf.Approximately(auMe.AutoMoveX, 0f));

                // Có vật phẩm: hạn 24 giờ tính từ lúc vào túi; đứng xa quái (x 28,5: cách 2 con gần nhất 5 ô) → bật → dịch chuyển
                GameActions.Chat(1, "/item 130 1");
                yield return WaitUntil(() => GameData.AutoTrainSecondsLeft() > 0, 5);
                float auLeft = GameData.AutoTrainSecondsLeft();
                Check($"Nhận Tự động luyện tập (1 ngày) → còn {GameData.FormatDuration(auLeft)}", auLeft > 23.9f * 3600 && auLeft <= 24 * 3600);
                var auSlot = GameData.Inventory.Find(auS => auS.tpl == 130);
                Check("Mô tả vật phẩm có dòng hạn dùng", auSlot != null && InventoryWindow.Describe(auSlot).Contains("Hạn dùng: còn"));
                GameActions.Chat(1, "/go 28.5 8");
                yield return new WaitForSecondsRealtime(1.5f);
                auHits = CombatNetwork.MyMobHits; auTele = AutoPlay.TeleRequests;
                float auJump = 0f;
                Vector3 auPrev = auMe.transform.position;
                AutoPlay.Instance.SetOn(true, null);
                for (float auT = 0; auT < 10f && (auJump < 2f || CombatNetwork.MyMobHits == auHits); auT += Time.unscaledDeltaTime)
                {
                    auJump = Mathf.Max(auJump, Vector2.Distance(auPrev, auMe.transform.position));   // 1 khung nhảy xa = dịch chuyển
                    auPrev = auMe.transform.position;
                    yield return null;
                }
                Check($"Có Tự động luyện tập → dịch chuyển tới quái rồi đánh (xin {AutoPlay.TeleRequests - auTele} lần, nhảy {auJump:0.0} ô, {CombatNetwork.MyMobHits - auHits} đòn)",
                    AutoPlay.TeleRequests > auTele && auJump >= 2f && CombatNetwork.MyMobHits > auHits);
                yield return Shot("AutoTele");
                AutoPlay.Instance.SetOn(false, null);

                // Chiêu buff đã chọn trong cửa sổ Tự động → hồi xong là tự dùng (chiêu đánh vẫn là chiêu đang chọn)
                int auBuff = GameData.ClassType == 2 ? 9 : GameData.ClassType == 3 ? 11 : 10;   // Cổ Vũ / Ảnh Bộ / Thiết Thể (cấp 8)
                bool auBuffWas = AutoSettings.Buff(auBuff);
                if (!GameData.MySkills.ContainsKey(auBuff))
                {
                    GameActions.Chat(1, "/sp 1");
                    yield return new WaitForSecondsRealtime(0.5f);
                    GameActions.UpgradeSkill(auBuff);
                    yield return WaitUntil(() => GameData.MySkills.ContainsKey(auBuff), 5);
                }
                var auBuffWin = GameWindow.Open<AutoWindow>();
                yield return new WaitForSecondsRealtime(0.4f);
                bool auListed = false;
                string auBuffName = GameData.Skills.TryGetValue(auBuff, out var auBt) ? auBt.name : "?";
                foreach (var auTx in auBuffWin.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) if (auTx.text.Contains(auBuffName)) auListed = true;
                Check($"Cửa sổ Tự động liệt kê chiêu buff của hệ (icon + tên, chiêu {auBuff})", auListed);
                auBuffWin.Hide();
                AutoSettings.SetBuff(auBuff, true);
                GameActions.Chat(1, "/hp");
                yield return new WaitForSecondsRealtime(0.5f);
                AutoPlay.Instance.SetOn(true, null);
                yield return WaitUntil(() => !SkillCooldowns.Ready(auBuff), 8);
                Check($"Chiêu buff được chọn → tự động tự dùng khi hồi xong (chiêu {auBuff})", GameData.MySkills.ContainsKey(auBuff) && !SkillCooldowns.Ready(auBuff));
                AutoPlay.Instance.SetOn(false, null);
                AutoSettings.SetBuff(auBuff, auBuffWas);

                // Bình chakra: ngưỡng 90%, giả chakra còn 0 (chỉ phía client) → tự uống dù đang TẮT tự động
                int auPots0 = QuickPotion.Count(false);
                GameActions.Chat(1, "/item 2 5");
                yield return WaitUntil(() => QuickPotion.Count(false) >= auPots0 + 5, 5);
                int auPots = QuickPotion.Count(false);
                AutoSettings.MpPercent = 90; AutoSettings.PotionMp = true;
                LocalPlayerState.Mp = 0;
                yield return WaitUntil(() => QuickPotion.Count(false) < auPots, 5);
                Check($"Chakra dưới ngưỡng → tự uống bình chakra ({auPots} → {QuickPotion.Count(false)})", QuickPotion.Count(false) < auPots);
                AutoSettings.PotionMp = false;

                // Nhặt đồ: hạ hết quái (rơi Vỏ Ốc 60–100%) → đứng cách món gần nhất 3,5 ô → bật → tự đi tới nhặt
                AutoSettings.PickItems = true; AutoSettings.FindMobs = false;
                GameActions.Chat(1, "/killall");
                yield return new WaitForSecondsRealtime(2f);   // món rơi ngay chân bị nhặt sẵn (GroundItemNetwork tự nhặt khi đứng sát) → chọn món CÒN trên đất
                int auItem = -1; Vector3 auItemPos = Vector3.zero;
                yield return WaitUntil(() => GroundItemNetwork.Instance != null
                    && GroundItemNetwork.Instance.TryNearest(auMe.transform.position, 999f, null, out auItem, out auItemPos), 5);
                if (auItem >= 0)
                {
                    float auGx = auItemPos.x + (auItemPos.x > 60f ? -3.5f : 3.5f);
                    GameActions.Chat(1, "/go " + auGx.ToString("0.0", auCi) + " " + (auItemPos.y + 0.5f).ToString("0.0", auCi));
                    yield return new WaitForSecondsRealtime(1.2f);
                }
                bool auStillThere = auItem >= 0 && GroundItemNetwork.Instance.Has(auItem);
                int auBag = 0; foreach (var auS in GameData.Inventory) auBag += auS.qty;
                AutoPlay.Instance.SetOn(true, null);
                yield return WaitUntil(() => auItem >= 0 && !GroundItemNetwork.Instance.Has(auItem), 8);
                yield return new WaitForSecondsRealtime(0.5f);
                int auBag2 = 0; foreach (var auS in GameData.Inventory) auBag2 += auS.qty;
                Check($"Tự nhặt đồ: đi tới món rơi gần nhất + nhặt (túi {auBag} → {auBag2}, món còn trên đất lúc bật: {auStillThere})",
                    auStillThere && !GroundItemNetwork.Instance.Has(auItem) && auBag2 > auBag);
                AutoPlay.Instance.SetOn(false, null);

                var auWin = GameWindow.Open<AutoWindow>();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot("AutoWindow");
                auWin.Hide();
                AutoSettings.FindMobs = auFind; AutoSettings.PickItems = auPick;
                AutoSettings.PotionHp = auHp; AutoSettings.PotionMp = auMp; AutoSettings.MpPercent = auMpPct;
                GameActions.Chat(1, "/tele 1");   // các mục sau cần đứng ở Làng Lá như trước
                yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);
                yield return new WaitForSecondsRealtime(1.5f);
            }

            // 5l. Phó bản (2026-10-29): Hokage → Phó bản → Vào → HUD có nút "Rời" → rời → về Làng Lá ngay
            //     (map phó bản không có cổng ra — trước kẹt tới hết giờ nếu không thoát game)
            {
                var dgHk = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(1) : null;
                if (dgHk != null)
                {
                    var dgPos = dgHk.transform.position;
                    GameActions.Chat(1, $"/go {(dgPos.x + 0.6f).ToString(inv)} {(dgPos.y + 0.05f).ToString(inv)}");
                    yield return new WaitForSecondsRealtime(1.5f);
                }
                var dgDlg = GameWindow.Get<NpcDialogWindow>();
                Assets.Script.Data.NpcNetwork.TalkToNearest();
                yield return WaitUntil(() => dgDlg.IsOpen, 5);
                dgDlg.Choose("Phó bản");
                yield return new WaitForSecondsRealtime(0.8f);
                dgDlg.Choose("Vào phó bản");
                yield return WaitUntil(() => GameData.DungeonState == 2, 8);
                yield return new WaitForSecondsRealtime(1f);
                var dgLeave = GameObject.Find("LeaveDungeon");
                Check($"Trong phó bản: HUD có nút Rời ({GameData.DungeonName})", GameData.DungeonState == 2 && dgLeave != null && dgLeave.activeInHierarchy);
                yield return Shot("Dungeon");
                GameActions.DungeonLeave();
                yield return WaitUntil(() => GameData.MapName == "Làng Lá" && GameData.DungeonState == 0, 8);
                yield return new WaitForSecondsRealtime(1f);
                Check("Bấm Rời → về Làng Lá ngay, nút Rời ẩn", GameData.MapName == "Làng Lá" && (dgLeave == null || !dgLeave.activeInHierarchy));
            }

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

            // Phân Thân Chi Thuật (mọi hệ, cấp 12) — sau ca khung Mẹo (ca đó cần vượt cấp 12 bằng /exp): học → dùng → server gửi EFFECT 5 → icon + đồng hồ trên HUD, nhân vật mờ đi
            var meVis = NetworkPlayerManager.Instance.localPlayer.GetComponent<PlayerVisualController>();   // lấy lại (đã đổi map)
            if (!GameData.MySkills.ContainsKey(4) && GameData.Skills.ContainsKey(4))
            {
                if (GameData.Me.level < 12) { GameActions.Chat(1, "/lv 12"); yield return new WaitForSecondsRealtime(0.5f); }
                GameActions.Chat(1, "/sp 1");
                yield return new WaitForSecondsRealtime(0.4f);
                GameActions.UpgradeSkill(4);
                yield return WaitUntil(() => GameData.MySkills.ContainsKey(4), 4);
            }
            Check("Học được Phân Thân Chi Thuật", GameData.MySkills.ContainsKey(4));
            if (GameData.MySkills.ContainsKey(4))
            {
                yield return new WaitForSecondsRealtime(1f);   // anim chiêu trước chạy xong
                SkillCaster.TryCast(4);
                Check($"Phân Thân → anim chiêu trong Spine ({meVis.mainSkeleton.AnimationName}, cần {PlayerVisualController.SkillAnimFor(meVis.ClassType, 4)})",
                    meVis.mainSkeleton.AnimationName == PlayerVisualController.SkillAnimFor(meVis.ClassType, 4));
                yield return WaitUntil(() => LocalPlayerState.EffectLeft(Assets.Script.Constants.StatusEffect.Dodge) > 0, 4);
                yield return null;
                var icons = FindAnyObjectByType<Assets.Script.UI.StatusIconsHud>();
                var icon = icons != null ? icons.transform.Find("Fx5") : null;
                Check($"Server báo phân thân ({LocalPlayerState.EffectLeft(Assets.Script.Constants.StatusEffect.Dodge):0.0} giây) → icon trên HUD + nhân vật mờ (alpha {meVis.mainSkeleton.Skeleton.A:0.00})",
                    LocalPlayerState.EffectLeft(Assets.Script.Constants.StatusEffect.Dodge) > 5f && icon != null && icon.gameObject.activeSelf && meVis.mainSkeleton.Skeleton.A < 0.9f);
                yield return Shot("PhanThan");
            }

            var hok = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(1) : null;
            if (hok != null)
            {
                var hkPos = hok.transform.position;
                GameActions.Chat(1, $"/go {(hkPos.x + 0.6f).ToString(inv)} {(hkPos.y + 0.05f).ToString(inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            Assets.Script.Data.NpcNetwork.TalkToNearest();
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
            Assets.Script.Data.NpcNetwork.TalkToNearest();
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
            Assets.Script.Data.NpcNetwork.TalkToNearest();
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

                // 8. Đăng xuất (Cài đặt → Đăng xuất): server lưu + trả LOGOUT 0 → về màn đăng nhập → vào lại được
                var settings = GameWindow.Get<SettingsWindow>();
                settings.Show();
                yield return null;
                bool hasLogout = false;
                foreach (var b in settings.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (b.name == "Logout") hasLogout = true;
                Check("Cửa sổ Cài đặt có nút Đăng xuất", hasLogout);
                bool hasPw = false;
                foreach (var b in settings.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (b.name == "Password") hasPw = true;
                Check("Cửa sổ Cài đặt có nút Đổi mật khẩu", hasPw);
                settings.Hide();

                // Đổi mật khẩu rồi đổi lại (server kiểm mật khẩu cũ bằng bcrypt)
                ChangePasswordWindow.LastStatus = -99;
                GameActions.ChangePassword("test1234", "test12345");
                yield return WaitUntil(() => ChangePasswordWindow.LastStatus != -99, 8);
                int st1 = ChangePasswordWindow.LastStatus;
                ChangePasswordWindow.LastStatus = -99;
                GameActions.ChangePassword("test12345", "test1234");
                yield return WaitUntil(() => ChangePasswordWindow.LastStatus != -99, 8);
                Check($"Đổi mật khẩu rồi đổi lại ({st1}, {ChangePasswordWindow.LastStatus})", st1 == 0 && ChangePasswordWindow.LastStatus == 0);

                // Tiền 64-bit: vượt 2,1 tỉ (giới hạn int cũ) vẫn đúng
                long yen0 = GameData.Me.yen;
                ChatBox.Send(1, "", "/yen 3000000000");
                yield return WaitUntil(() => GameData.Me.yen >= yen0 + 3000000000L, 5);
                Check($"Yên vượt 2,1 tỉ ({GameData.Me.yen:N0})", GameData.Me.yen >= yen0 + 3000000000L && GameData.Me.yen > int.MaxValue);
                GameActions.Logout();
                yield return WaitUntil(() => LocalPlayerState.Id < 0, 10);
                Check("Đăng xuất → dọn phiên + báo đã đăng xuất", LocalPlayerState.Id < 0 && (PopupAndLoad.Instance?.LastMessage ?? "").Contains("đăng xuất"));
                yield return new WaitForSecondsRealtime(2f);
                var auth3 = FindAnyObjectByType<GameAuthManager>();
                if (auth3 != null) auth3.AutoLogin(Arg("-user", "clientbot"), Arg("-pass", "test1234"));
                yield return WaitUntil(() => NetworkPlayerManager.Instance?.localPlayer != null && LocalPlayerState.Id >= 0, 25);
                Check("Sau đăng xuất đăng nhập lại vào map", NetworkPlayerManager.Instance?.localPlayer != null && LocalPlayerState.Id >= 0);
            }

            // Skin bằng hình kho (chỉ khi máy có Resources/UISkin — demo nội bộ): khung cửa sổ, nút menu HUD thành icon
            if (Assets.Script.UI.Kit.UISkin.Enabled)
            {
                var hudMenu = GameObject.Find("Menu0");
                var menuIcon = hudMenu != null ? hudMenu.transform.Find("Icon") : null;
                Check("Nút menu HUD dùng icon trong kho (không còn nút chữ)", menuIcon != null && menuIcon.GetComponent<UnityEngine.UI.Image>().sprite != null);
            }
            Check("Không có Exception trong suốt phiên", _exceptions == 0);
            Debug.Log($"[AutoTest] KẾT QUẢ: {_pass} PASS, {_fail} FAIL");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(_fail);
        }
    }
}
