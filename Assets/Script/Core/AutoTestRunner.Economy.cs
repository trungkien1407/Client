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
    // Autotest — hoạt động hằng ngày, nhiệm vụ, chợ, phẩm chất + khảm ngọc, quà online + giftcode (mục 5c–5f).
    public partial class AutoTestRunner
    {
        /// <summary>Mục 5c–5f: hoạt động, nhiệm vụ nhiều trang, chợ, khảm ngọc, giftcode.</summary>
        private IEnumerator TestEconomy()
        {
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
                GameActions.Chat(1, $"/go {(hkPos.x + 0.6f).ToString(Inv)} {(hkPos.y + 0.05f).ToString(Inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            Assets.Script.Data.NpcNetwork.TalkToNearest();
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            _dlg.Choose("Nhiệm vụ");
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            _dlg.Choose("[Mới]");
            yield return WaitUntil(() => _dlg.IsOpen && _dlg.Text.Contains("(1/"), 5);
            Check("Nhận nhiệm vụ → hội thoại nhiều trang (1/N + Tiếp theo)", _dlg.IsOpen && _dlg.Text.Contains("(1/"));
            yield return Shot("DialogPage1");
            _dlg.Choose("Tiếp");
            yield return WaitUntil(() => _dlg.IsOpen && _dlg.Text.Contains("Bạn:"), 5);
            Check("Trang 2: lời của người chơi", _dlg.Text.Contains("Bạn:"));
            yield return Shot("DialogPage2");
            _dlg.Hide();

            // 5d. GĐ9 — Chợ: tới Chủ Chợ → mở chợ (Hành trang tự mở cạnh) → treo bán 1 bình máu → thấy ở "Hàng của tôi" → huỷ
            GameActions.Chat(1, "/item 1 3");
            var seller = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.FindByTemplate(10) : null;
            Check("Có NPC Chủ Chợ ở Làng Lá", seller != null);
            if (seller != null)
            {
                var sp2 = seller.transform.position;
                GameActions.Chat(1, $"/go {(sp2.x + 0.6f).ToString(Inv)} {(sp2.y + 0.05f).ToString(Inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            var mk = GameWindow.Get<MarketWindow>();
            Assets.Script.Data.NpcNetwork.TalkToNearest();
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            _dlg.Choose("Chợ");
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
                GameActions.Chat(1, $"/go {(sp3.x + 0.6f).ToString(Inv)} {(sp3.y + 0.05f).ToString(Inv)}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
            Assets.Script.Data.NpcNetwork.TalkToNearest();
            yield return WaitUntil(() => _dlg.IsOpen, 5);
            _dlg.Choose("Khảm ngọc");
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
        }
    }
}
#endif
