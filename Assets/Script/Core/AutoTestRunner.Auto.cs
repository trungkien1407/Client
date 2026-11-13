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
    // Autotest — Tự động (phím Z) + phó bản (mục 5k–5l).
    public partial class AutoTestRunner
    {
        /// <summary>Mục 5k–5l: Tự động đánh / nhặt / dùng bình, phó bản vào → rời.</summary>
        private IEnumerator TestAutoAndDungeon()
        {
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
                // Hệ đánh xa (Sakura 5 ô): quái gần nhất có thể đã TRONG TẦM từ chỗ đứng → đúng luật là đánh luôn, không xin dịch chuyển
                float auRange = SkillCaster.SkillRange(SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1);
                var auNear = NetworkMobManager.Instance?.FindNearestAlive(auMe.transform.position, 60f);
                bool auInRange = auNear != null && auNear.DistanceFrom(auMe.transform.position) <= auRange - 0.15f;
                bool auCame = false;
                float auJump = 0f;
                Vector3 auPrev = auMe.transform.position;
                AutoPlay.Instance.SetOn(true, null);
                for (float auT = 0; auT < 10f && (auJump < 2f || CombatNetwork.MyMobHits == auHits); auT += Time.unscaledDeltaTime)
                {
                    auJump = Mathf.Max(auJump, Vector2.Distance(auPrev, auMe.transform.position));   // 1 khung nhảy xa = dịch chuyển
                    // Quái lang thang / chủ động tự tới gần (≤ TeleMinDist) TRƯỚC khi kịp dịch chuyển → luật là đi bộ đánh, không dịch chuyển
                    if (AutoPlay.TeleRequests == auTele)
                    {
                        var auClose = NetworkMobManager.Instance?.FindNearestAlive(auMe.transform.position, 60f);
                        if (auClose != null && auClose.DistanceFrom(auMe.transform.position) <= AutoPlay.TeleMinDist) auCame = true;
                    }
                    auPrev = auMe.transform.position;
                    yield return null;
                }
                if (auInRange || (auCame && AutoPlay.TeleRequests == auTele))
                    Check($"Có Tự động luyện tập, quái {(auInRange ? $"đã trong tầm {auRange:0.#} ô" : $"tự tới gần ≤ {AutoPlay.TeleMinDist} ô")} → đánh luôn, không dịch chuyển ({CombatNetwork.MyMobHits - auHits} đòn)",
                        AutoPlay.TeleRequests == auTele && CombatNetwork.MyMobHits > auHits);
                else
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
                    GameActions.Chat(1, $"/go {(dgPos.x + 0.6f).ToString(Inv)} {(dgPos.y + 0.05f).ToString(Inv)}");
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
        }
    }
}
