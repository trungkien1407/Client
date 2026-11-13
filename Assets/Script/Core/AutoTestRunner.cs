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
    /// <summary>
    /// KIỂM THỬ TỰ ĐỘNG BẢN BUILD (smoke test) — chỉ chạy khi mở game với tham số dòng lệnh:
    ///   NinjaOnline.exe -autotest -user clientbot -pass test1234 [-batchmode -nographics] -logFile test.log
    ///
    /// Game tự: đăng nhập → chờ vào map → kiểm dữ liệu server gửi → mở từng cửa sổ UI → đánh quái
    /// → nói chuyện NPC → ghi "[AutoTest] PASS/FAIL ..." vào log → tự thoát (exit code = số lỗi).
    /// Mọi Exception trong lúc chạy cũng bị đếm là FAIL. Người chơi bình thường không bao giờ kích hoạt.
    /// Mã chia theo mảng: AutoTestRunner.Ui / Npc / Combat / World / Pets / Auto / Economy / Session.cs (partial class).
    /// </summary>
    public partial class AutoTestRunner : MonoBehaviour
    {
        private int _pass, _fail, _exceptions;
        // Dùng chung giữa các phần (mỗi phần ở 1 file AutoTestRunner.*.cs)
        private int _excBeforeUi;              // số Exception lúc bắt đầu mở cửa sổ (mục 3) — mục 5 so lại
        private NpcDialogWindow _dlg;          // cửa sổ hội thoại NPC (mục 4, 5c–5e)
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;   // số thập phân dấu chấm cho lệnh /go

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
            // "-mountsonly": chỉ chụp 24 thú cưỡi để chỉnh cỡ / chỗ ngồi (PetMount.Tune) rồi thoát — nhanh hơn chạy đủ autotest
            // "-backdroponly": chỉ chạy phần nền nhiều lớp (chụp ảnh từng chủ đề) — chỉnh nền nhanh
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-backdroponly") >= 0)
            {
                yield return CheckBackdrop();
                Debug.Log($"[AutoTest] KẾT QUẢ: {_pass} PASS, {_fail} FAIL");
                Application.Quit(_fail);
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mountsonly") >= 0)
            {
                yield return MountShots();
                Debug.Log($"[AutoTest] KẾT QUẢ: {_pass} PASS, {_fail} FAIL");
                Application.Quit(_fail);
                yield break;
            }

            yield return TestWindows();
            yield return TestNpc();
            yield return TestCombat();
            yield return TestWorld();
            yield return TestAutoAndDungeon();
            yield return TestEconomy();
            yield return CheckBackdrop();
            yield return CheckHudPad();
            yield return CheckMobDeathFx();
            yield return CheckVillageDoor();
            yield return TestAutoPath();
            yield return TestSession();

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
