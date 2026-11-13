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
    // Autotest — đánh quái, tự chạy tới mục tiêu, hồi chiêu, tự chọn mục tiêu (mục 5–5a2) + chiêu cấp cao.
    public partial class AutoTestRunner
    {
        /// <summary>Mục 5–5a2: đánh quái, Enter tự chạy tới, hồi chiêu phía client, tự chọn mục tiêu.</summary>
        private IEnumerator TestCombat()
        {
            // 5. Đánh quái gần nhất
            int skill = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
            if (skill < 0 && GameData.MySkills.Count > 0) foreach (var k in GameData.MySkills.Keys) { skill = k; break; }
            SkillCaster.TryCast(skill);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Bấm đánh không lỗi", _exceptions == _excBeforeUi);

            // 5a. Enter (ActionInput): chọn quái ở xa → tự chạy tới rồi đánh
            var meMove = NetworkPlayerManager.Instance?.localPlayer;
            var farMob = meMove != null ? NetworkMobManager.Instance?.FindNearestAlive(meMove.transform.position, 30f) : null;
            if (farMob != null)
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;   // máy tiếng Việt viết "1,5" — server cần "1.5"
                // Đứng NGOÀI tầm chiêu đang chọn (Sakura đánh xa 5 ô — cách 5 ô là đánh luôn, không cần chạy) + 4 ô.
                // Quái đi lang thang: chờ xong mà nó đã tới trong tầm → dịch ra xa lại, tối đa 3 lần
                float enterRange = SkillCaster.SkillRange(SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1);
                for (int tries = 0; tries < 3; tries++)
                {
                    ChatBox.Send(1, "", "/go " + (farMob.transform.position.x - enterRange - 4f).ToString("0.0", ci) + " " + (farMob.transform.position.y + 0.5f).ToString("0.0", ci));
                    yield return new WaitForSecondsRealtime(1.2f);
                    if (farMob.DistanceFrom(meMove.transform.position) > enterRange + 1.5f) break;
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
            // Âm thanh (có file demo / chính thức mới kiểm): map có quái → nhạc bãi luyện
            int audioFiles = 0;
            foreach (var k in GameAudio.AllKeys) if (AudioManager.Has(k)) audioFiles++;
            if (audioFiles == 0) Debug.Log("[AutoTest] INFO chưa có file âm thanh (Resources/Audio) — bỏ qua ca âm thanh");
            else
            {
                Check($"Âm thanh: có {audioFiles}/{GameAudio.AllKeys.Length} file theo tên GameAudio", audioFiles == GameAudio.AllKeys.Length);
                // Cóc Chúa (boss) đứng ở Đồi Hoa Cúc → gần nó là nhạc boss, xa là nhạc bãi luyện
                yield return new WaitForSecondsRealtime(1.2f);   // GameAudio xét boss mỗi giây
                var meA = NetworkPlayerManager.Instance?.localPlayer;
                bool nearBoss = meA != null && NetworkMobManager.Instance.AnyBossNear(meA.transform.position, GameAudio.BossRange);
                string wantMusic = nearBoss ? GameAudio.BgmBoss : GameAudio.BgmField;
                Check($"Vào Đồi Hoa Cúc (map quái{(nearBoss ? ", đứng gần boss" : "")}) → {wantMusic} (đang phát: {AudioManager.CurrentMusic})", AudioManager.CurrentMusic == wantMusic);
            }
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
        }

        private int _slot9Skill = -1;   // chiêu gán vào ô 9 (CheckHighSkill) — đăng nhập lại phải còn (server lưu 9 ô)

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
                // Thanh chiêu 9 ô: gán chiêu biến thân vào ô 9 (server lưu) rồi BẤM ô 9 để dùng (= phím 9 / chạm trên điện thoại)
                var bar = SkillBarManager.Instance;
                var barRt = bar != null ? (RectTransform)bar.skillSlots[0].transform.parent : null;
                float shown = barRt != null ? 120f * barRt.localScale.x : 0f;
                // ô cuối không lấn sang nút Auto (Auto đã dời lên dưới bản đồ nhỏ) — so khung màn hình của ô 9 với nút Auto
                var autoBtn = GameObject.Find("AutoBtn");
                bool overlap = false;
                if (bar != null && autoBtn != null)
                {
                    var a = new Vector3[4]; var b = new Vector3[4];
                    ((RectTransform)bar.skillSlots[bar.skillSlots.Length - 1].transform).GetWorldCorners(a);
                    ((RectTransform)autoBtn.transform).GetWorldCorners(b);
                    overlap = a[0].x < b[2].x && a[2].x > b[0].x && a[0].y < b[2].y && a[2].y > b[0].y;
                }
                Check($"Thanh chiêu đủ 9 ô, ô nhỏ lại (~{shown:0} px, trước 120), không đè nút Auto",
                    bar != null && bar.skillSlots.Length == 9 && shown < 100f && !overlap);
                if (bar != null && bar.skillSlots.Length == 9)
                {
                    GameActions.SetShortcut(8, tf);
                    bar.AssignSkillToSlot(8, tf);
                    _slot9Skill = tf;
                    bar.SelectSlotAndUse(8);
                }
                else SkillCaster.TryCast(tf);
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

        /// <summary>
        /// Nút trên màn hình (UI.HudPad): giữ nút trái / phải → nhân vật đi (như bấm phím) và nút lún xuống;
        /// thả ra → nút về như cũ. Phím cũng làm nút sáng theo (HudPad đọc ManualInput — gồm cả phím lẫn nút màn hình).
        /// </summary>
        private IEnumerator CheckHudPad()
        {
            var pad = UI.HudPad.Instance;
            yield return WaitUntil(() => pad != null && pad.Left != null, 3);
            Check("Nút trái / phải / nhảy / đánh trên màn hình đã nối (HudPad)", pad != null && pad.Left != null && pad.Right != null && pad.JumpPad != null);
            if (pad == null || pad.Left == null) yield break;
            var me = NetworkPlayerManager.Instance.localPlayer;
            var ev = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            float x0 = me.transform.position.x;
            UnityEngine.EventSystems.ExecuteEvents.Execute(pad.Left.gameObject, ev, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(0.6f);
            bool leftShown = pad.Left.Shown;
            float x1 = me.transform.position.x;
            UnityEngine.EventSystems.ExecuteEvents.Execute(pad.Left.gameObject, ev, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(pad.Right.gameObject, ev, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(0.6f);
            bool rightShown = pad.Right.Shown;
            float x2 = me.transform.position.x;
            UnityEngine.EventSystems.ExecuteEvents.Execute(pad.Right.gameObject, ev, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            yield return null;
            yield return null;
            Check($"Giữ nút trái / phải trên màn hình → nhân vật đi ({x1 - x0:0.00} / {x2 - x1:0.00})", x1 < x0 - 0.2f || x2 > x1 + 0.2f);
            Check("Đang giữ thì nút lún xuống, thả thì về như cũ", leftShown && rightShown && !pad.Left.Shown && !pad.Right.Shown);
        }

        /// <summary>
        /// Quái chết (MobController.PlayDeath): hất nhẹ ra xa người chơi → rơi xuyên nền → mờ dần rồi mới biến mất;
        /// trong lúc đó không chọn làm mục tiêu được (Tab / bấm chuột / SelectTarget đều bỏ qua xác).
        /// </summary>
        private IEnumerator CheckMobDeathFx()
        {
            GameActions.Chat(1, "/tele 2");   // Đồi Hoa Cúc: nhiều quái
            yield return WaitUntil(() => GameData.MapName == "Đồi Hoa Cúc", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            var me = NetworkPlayerManager.Instance.localPlayer;
            var mob = NetworkMobManager.Instance != null ? NetworkMobManager.Instance.FindNearestAlive(me.transform.position, 60f) : null;
            Check("Có quái để thử hiệu ứng chết", mob != null);
            if (mob == null) yield break;
            // Chọn quái: tên + cấp chỉ hiện ở thanh mục tiêu phía trên (1 dòng, nằm trên thanh máu của khung); trên đầu quái không có chữ
            var targeting0 = FindAnyObjectByType<PlayerTargeting>();
            if (targeting0 != null) targeting0.SelectTarget(mob);
            yield return null;
            var top = UISetup.Instance != null ? UISetup.Instance.targetName : null;
            if (top != null) top.ForceMeshUpdate();
            Check($"Thanh mục tiêu: tên + cấp quái 1 dòng ({(top != null ? top.GetParsedText() : "?")})",
                top != null && top.textInfo.lineCount == 1 && top.GetParsedText().Contains("Lv"));
            var hpFillTop = UISetup.Instance != null ? UISetup.Instance.targetHpFill : null;
            Check("Tên trên thanh mục tiêu không đè thanh máu của khung",
                top == null || hpFillTop == null || top.rectTransform.TransformPoint(top.rectTransform.rect.min).y >= hpFillTop.rectTransform.TransformPoint(hpFillTop.rectTransform.rect.max).y - 1f);
            int withName = 0;   // không quái nào còn chữ tên trên đầu (tên chỉ ở thanh mục tiêu)
            foreach (var m in FindObjectsByType<Entities.MobController>(FindObjectsSortMode.None))
                if (m.GetComponentInChildren<TMPro.TextMeshPro>(true) != null) withName++;
            Check($"Mọi quái đều không hiện tên trên đầu — chỉ thanh máu khi được chọn ({withName} con còn chữ)", withName == 0);
            yield return Shot("TargetBarName");
            Vector3 p0 = mob.transform.position;
            GameActions.Chat(1, "/killall");
            yield return WaitUntil(() => mob.IsDead, 3);
            yield return new WaitForSecondsRealtime(0.7f);   // nảy lên ~0,35 ô rồi rơi: tới 0,7 giây đã xuống dưới chỗ cũ
            Vector3 p1 = mob.transform.position;
            var targeting = FindAnyObjectByType<PlayerTargeting>();
            if (targeting != null) targeting.SelectTarget(mob);
            Check($"Quái chết: bị hất ngang ({p1.x - p0.x:0.00}) rồi rơi xuyên nền ({p1.y - p0.y:0.00})",
                mob.IsDead && Mathf.Abs(p1.x - p0.x) > 0.5f && p1.y < p0.y - 0.3f);
            Check("Xác quái đang rơi không chọn làm mục tiêu được", targeting == null || !ReferenceEquals(targeting.currentTarget, mob));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("Rơi xong thì xác biến mất", !mob.gameObject.activeInHierarchy || NetworkMobManager.Instance.GetMob(mob.GetId()) != mob);
        }
    }
}
