#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System.Collections;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Core
{
    // 2026-11-15: chế độ CHƠI KHÁCH + xác thực tài khoản (chạy riêng: NinjaOnline.exe -autotest -guest).
    public partial class AutoTestRunner
    {
        /// <summary>
        /// Luồng đầy đủ như người chơi thật:
        ///   1. Bấm "Chơi ngay" → server tạo khach_xxxxxxxx + mật khẩu ngẫu nhiên → máy lưu (mã hoá) → màn tạo nhân vật.
        ///   2. Tạo nhân vật → vào game → ACCOUNT_INFO báo là khách, CHƯA đủ điều kiện → Cài đặt không có nút Xác thực.
        ///   3. Cố gửi ACCOUNT_VERIFY khi mới cấp 1 → server chặn (mã 4) — chống sửa client hiện nút.
        ///   4. Bật tự đánh tới cấp 3 → server gửi lại ACCOUNT_INFO (đủ điều kiện) → nút hiện → xác thực bằng tên / mật khẩu / email mới.
        ///   5. Đăng xuất → đăng nhập lại bằng tên mới → không còn là khách.
        /// </summary>
        private IEnumerator GuestFlow(GameAuthManager auth)
        {
            SavedLogin.ClearGuest();
            SavedLogin.Clear();
            yield return null;
            yield return Shot("Guest_0_login");
            auth.OnGuestClick();
            var ccm = FindAnyObjectByType<CharacterCreationManager>(FindObjectsInactive.Include);
            yield return WaitUntil(() => (ccm != null && ccm.gameObject.activeInHierarchy) || LocalPlayerState.Id >= 0, 15);
            Check($"Bấm \"Chơi ngay\" → server tạo tài khoản khách ({SavedLogin.GuestUser}), máy lưu mã hoá",
                SavedLogin.HasGuest && AuthMessages.IsGuestName(SavedLogin.GuestUser));
            string name = "Khach" + Random.Range(10000, 99999);
            GameActions.CreateCharacter(name, 1);
            yield return WaitUntil(() => LocalPlayerState.Id >= 0 && NetworkPlayerManager.Instance?.localPlayer != null, 20);
            Check($"Tạo nhân vật {name} → vào game", LocalPlayerState.Id >= 0);
            yield return WaitUntil(() => GameData.IsGuest, 10);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("ACCOUNT_INFO: là khách, cấp 1 chưa xác thực được", GameData.IsGuest && !GameData.CanVerify);

            var settings = GameWindow.Get<SettingsWindow>();
            settings.Show();
            yield return null;
            var verifyBtn = settings.transform.Find("Body/Verify") ?? FindDeep(settings.transform, "Verify");
            var passBtn = FindDeep(settings.transform, "Password");
            Check("Cài đặt (khách chưa đủ điều kiện): ẩn nút Xác thực + ẩn Đổi mật khẩu",
                (verifyBtn == null || !verifyBtn.gameObject.activeSelf) && (passBtn == null || !passBtn.gameObject.activeSelf));
            yield return Shot("Guest_1_settings_an");
            settings.Hide();

            // Server phải tự chặn dù client có gửi (người sửa client)
            AccountVerifyWindow.LastStatus = -99;
            long tick = System.DateTime.UtcNow.Ticks % 100000000;
            GameActions.AccountVerify("tk" + tick, "matkhau123", $"tk{tick}@test.local");
            yield return WaitUntil(() => AccountVerifyWindow.LastStatus != -99, 8);
            Check($"Cấp 1, chưa trả nhiệm vụ → server chặn xác thực (mã {AccountVerifyWindow.LastStatus}, cần 4)", AccountVerifyWindow.LastStatus == 4);
            GameWindow.Get<AccountVerifyWindow>().Hide();

            // Đủ điều kiện bằng đường "xong nhiệm vụ đầu" như người chơi mới: Hokage → nhận "Làm quen Thợ Rèn" → tự đi tới
            // Thợ Rèn nói chuyện → về Hokage trả. (Không được thì tự đánh lên cấp 3.)
            var dlg = GameWindow.Get<NpcDialogWindow>();
            var hok = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(1) : null;
            var smith = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(2) : null;
            if (hok != null && smith != null)
            {
                yield return Talk(dlg, hok.GetId());
                yield return Pick(dlg, "Nhiệm vụ");
                yield return Pick(dlg, "[Mới]");
                yield return Pick(dlg, "Nhận nhiệm vụ");
                dlg.Hide();
                int qid = 0;
                foreach (var kv in GameData.MyQuests) { qid = kv.Key; break; }
                Check($"Nhận nhiệm vụ đầu ở Hokage (#{qid})", qid > 0);
                yield return Talk(dlg, smith.GetId());   // nói chuyện với Thợ Rèn (đứng gần, trong tầm 4 ô) = xong nhiệm vụ "đi gặp"
                dlg.Hide();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Talk(dlg, hok.GetId());
                yield return Pick(dlg, "Nhiệm vụ");
                yield return Pick(dlg, "[Trả]");
                dlg.Hide();
                yield return WaitUntil(() => GameData.CanVerify, 5);
            }
            if (!GameData.CanVerify)   // dự phòng: tự đánh quái tới cấp 3 (tối đa 4 phút)
            {
                if (!AutoPlay.IsOn) AutoPlay.Toggle();
                yield return WaitUntil(() => GameData.Me.level >= 3 && GameData.CanVerify, 240);
                if (AutoPlay.IsOn) AutoPlay.Toggle();
            }
            yield return new WaitForSecondsRealtime(1f);
            Check($"Trả nhiệm vụ đầu (cấp {GameData.Me.level}) → server báo đủ điều kiện xác thực", GameData.CanVerify);

            settings.Show();
            yield return null;
            verifyBtn = FindDeep(settings.transform, "Verify");
            Check("Đủ điều kiện → Cài đặt hiện nút Xác thực", verifyBtn != null && verifyBtn.gameObject.activeSelf);
            yield return Shot("Guest_2_settings_hien");
            settings.Hide();

            var win = GameWindow.Get<AccountVerifyWindow>();
            win.Open();
            yield return null;
            yield return Shot("Guest_3_xac_thuc");
            string newUser = "tk" + tick, newPass = "matkhau123";
            AccountVerifyWindow.LastStatus = -99;
            // điền form như người chơi rồi bấm nút
            SetField(win, "User", newUser); SetField(win, "Pass", newPass); SetField(win, "Again", newPass); SetField(win, "Email", $"tk{tick}@test.local");
            FindDeep(win.transform, "Ok").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitUntil(() => AccountVerifyWindow.LastStatus != -99, 10);
            yield return new WaitForSecondsRealtime(1f);
            Check($"Xác thực thành công (mã {AccountVerifyWindow.LastStatus}) → hết là khách, máy bỏ thông tin khách, lưu tài khoản mới",
                AccountVerifyWindow.LastStatus == 0 && !GameData.IsGuest && !SavedLogin.HasGuest && SavedLogin.User == newUser);
            yield return Shot("Guest_4_xong");

            // Đăng nhập lại bằng tên mới → nhân vật cũ, không còn là khách
            GameActions.Logout();
            yield return WaitUntil(() => LocalPlayerState.Id < 0, 10);
            yield return new WaitForSecondsRealtime(2f);
            var auth2 = FindAnyObjectByType<GameAuthManager>();
            if (auth2 != null) auth2.AutoLogin(newUser, newPass);
            yield return WaitUntil(() => LocalPlayerState.Id >= 0 && NetworkPlayerManager.Instance?.localPlayer != null, 20);
            yield return new WaitForSecondsRealtime(2f);
            Check($"Đăng nhập lại bằng {newUser} → đúng nhân vật {name}, không còn là khách",
                LocalPlayerState.Id >= 0 && LocalPlayerState.Name == name && !GameData.IsGuest);
        }

        /// <summary>Nói chuyện với 1 NPC (id thực thể), chờ hộp thoại mở.</summary>
        private IEnumerator Talk(NpcDialogWindow dlg, int npcId)
        {
            dlg.Hide();
            GameActions.NpcTalk(npcId);
            yield return WaitUntil(() => dlg.IsOpen, 5);
        }

        /// <summary>Chọn lựa chọn có chữ {text} (bấm "Tiếp" qua các trang hội thoại nếu chưa thấy), chờ hộp thoại kế.</summary>
        private IEnumerator Pick(NpcDialogWindow dlg, string text)
        {
            for (int i = 0; i < 6; i++)
            {
                if (!dlg.IsOpen) yield return WaitUntil(() => dlg.IsOpen, 5);
                if (dlg.Choose(text)) { yield return WaitUntil(() => dlg.IsOpen, 3); yield break; }
                if (!dlg.Choose("Tiếp")) yield break;
                yield return WaitUntil(() => dlg.IsOpen, 3);
            }
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        private static void SetField(Component root, string name, string value)
        {
            var f = FindDeep(root.transform, name)?.GetComponent<TMPro.TMP_InputField>();
            if (f != null) f.text = value;
        }
    }
}
#endif
