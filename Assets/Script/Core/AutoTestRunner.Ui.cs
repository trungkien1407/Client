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
    // Autotest — cửa sổ UI, chặn thao tác khi mở cửa sổ, hình NPC + mốc cổng, gói xã hội, chat, chọn khu (mục 3).
    public partial class AutoTestRunner
    {
        /// <summary>Mục 3: mở từng cửa sổ, chat, chọn khu, công tắc âm thanh.</summary>
        private IEnumerator TestWindows()
        {
            // 3. Mở từng cửa sổ (bắt lỗi dựng UI)
            _excBeforeUi = _exceptions;
            GameWindow.Open<CharacterWindow>(); yield return null;
            GameWindow.Open<InventoryWindow>(); yield return null;
            GameWindow.Open<SkillWindow>(); yield return null;
            GameWindow.Open<QuestWindow>(); yield return null;
            Check("Mở 4 cửa sổ không lỗi", _exceptions == _excBeforeUi);
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
            Check("Mở 7 cửa sổ GĐ2–GĐ4 không lỗi", _exceptions == _excBeforeUi);

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
                int hudMoneyShown = 0, hudMoneyFound = 0;   // chữ "Money" của HUD = không nằm trong cửa sổ nào
                foreach (var t in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
                    if (t.name == "Money" && t.GetComponentInParent<GameWindow>(true) == null)
                    {
                        hudMoneyFound++;
                        if (t.text.Contains("Yên") || t.text.Contains("Lượng")) hudMoneyShown++;
                    }
                Check($"Ngoài màn hình (HUD) không hiện Yên / Lượng — chỉ xem trong Hành trang ({hudMoneyFound} ô, {hudMoneyShown} còn tiền)",
                    hudMoneyFound > 0 && hudMoneyShown == 0);
                var expTextGo = GameObject.Find("ExpText");
                var expTmp = expTextGo != null ? expTextGo.GetComponent<TMPro.TextMeshProUGUI>() : null;
                Check("Thanh EXP dưới cùng chỉ hiện màu (không chữ cấp / %)", expTmp == null || string.IsNullOrEmpty(expTmp.text));
                set.Show();
                GameWindow.SideBySide(set, bag);
                bag.Hide(); yield return null;
                Check("Cặp cửa sổ (… | Hành trang): đóng 1 cái → đóng cả 2", !set.IsOpen && !bag.IsOpen && !UIRoot.GameplayBlocked);
                // Cài đặt: bấm ô % = tắt / bật nhạc nền, hiệu ứng (nhớ mức âm lượng cũ)
                set.Show(); yield return null;
                string volBefore = set.MusicText;
                set.ToggleMusic(); set.ToggleSfx(); yield return null;
                bool offOk = !AudioManager.MusicOn && !AudioManager.SfxOn && !AudioManager.MusicAudible && set.MusicText == "TẮT" && set.SfxText == "TẮT";
                set.ToggleMusic(); set.ToggleSfx(); yield return null;
                bool onOk = AudioManager.MusicOn && AudioManager.SfxOn && set.MusicText == volBefore && set.SfxText != "TẮT";
                set.Hide();
                Check($"Cài đặt: bấm ô % tắt nhạc nền + hiệu ứng (hiện TẮT, nhạc câm) → bấm lại bật, về mức cũ {volBefore} ({offOk}/{onOk})", offOk && onOk);
            }

            // 3d. Hình NPC theo server (npc_template.art — demo: data/art_demo.json) + mốc cổng có mũi tên
            {
                bool anyArt = false;
                foreach (var a in GameData.NpcArt.Values) if (a >= 0) { anyArt = true; break; }
                if (anyArt && NetworkNpcManager.Instance != null)
                {
                    int withArt = 0, total = 0;
                    foreach (var npc in NetworkNpcManager.Instance.All) { total++; if (npc != null && npc.AppliedArt >= 0) withArt++; }
                    Check($"NPC dùng hình riêng theo server: {withArt}/{total}", total > 0 && withArt == total);
                    // GĐ26: cỡ vẽ theo npc_template.scale (art_demo.json đặt cho cao ~150 px như người chơi)
                    int scaled = 0, wrong = 0;
                    foreach (var npc in NetworkNpcManager.Instance.All)
                    {
                        if (npc == null || !GameData.NpcScale.TryGetValue(npc.TemplateId, out float wantScale)) continue;
                        if (Mathf.Abs(npc.AppliedScale - wantScale) < 0.01f) scaled++; else wrong++;
                    }
                    if (GameData.NpcScale.Count > 0)
                        Check($"NPC vẽ đúng cỡ theo npc_template.scale ({scaled} đúng, {wrong} sai)", scaled > 0 && wrong == 0);
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
        }
    }
}
#endif
