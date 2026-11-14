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
    // Autotest — bảo trì (-maint), mất kết nối, đăng xuất / đăng nhập lại (mục 6–7).
    public partial class AutoTestRunner
    {
        /// <summary>Mục 6–7: bảo trì, mất kết nối → đăng nhập lại, đăng xuất.</summary>
        private IEnumerator TestSession()
        {
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
                if (_slot9Skill > 0)
                    Check($"Đăng nhập lại: ô 9 vẫn giữ chiêu {_slot9Skill} (server lưu đủ 9 ô phím tắt)",
                        SkillBarManager.Instance != null && SkillBarManager.Instance.skillSlots.Length == 9
                        && SkillBarManager.Instance.skillSlots[8].assignedSkillId == _slot9Skill);
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
        }
    }
}
#endif
