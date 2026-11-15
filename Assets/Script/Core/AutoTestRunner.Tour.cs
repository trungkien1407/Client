#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System.Collections;
using Assets.Script.Combat;
using Assets.Script.Data;
using Assets.Script.Manager;
using Assets.Script.Skill;
using Assets.Script.UI;
using UnityEngine;

namespace Assets.Script.Core
{
    // 2026-11-15: hướng dẫn lần đầu kiểu chỉ tay (GuidedTour), thú đưa tin (NoticeMascot), luật tay không không đánh được.
    public partial class AutoTestRunner
    {
        private IEnumerator CheckTourAndNotice()
        {
            // 1. Hướng dẫn: chạy từ đầu, bấm Tiếp tới hết; đếm bước có lỗ sáng chỉ vào đâu đó, chụp vài bước
            GuidedTour.Begin(0f);
            yield return WaitUntil(() => GuidedTour.Active && GuidedTour.StepIndex == 0, 3);
            yield return new WaitForSecondsRealtime(0.3f);
            Check($"Hướng dẫn tự chạy được ({GuidedTour.StepCount} bước)", GuidedTour.Active && GuidedTour.StepCount >= 10);
            Check("Đang xem hướng dẫn → chặn thao tác game (Enter = Tiếp)", UI.Kit.UIRoot.GameplayBlocked);
            yield return Shot("Tour_0");
            int holes = 0, guard = 0, last = -1;
            while (GuidedTour.Active && guard++ < 20)
            {
                GuidedTour.NextStep();
                yield return new WaitForSecondsRealtime(0.35f);
                if (!GuidedTour.Active || GuidedTour.StepIndex == last) break;
                last = GuidedTour.StepIndex;
                if (GuidedTour.HasHole) holes++;
                yield return Shot("Tour_" + last);
            }
            Check($"Hướng dẫn chỉ vào {holes} chỗ (máu, di chuyển, Hokage, nhiệm vụ, nút đánh, kỹ năng, menu, bản đồ, tự động, chat)", holes >= 7);
            if (GuidedTour.Active) GuidedTour.NextStep();   // bước cuối "Bắt đầu chơi"
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Bấm hết → hướng dẫn đóng, thao tác game mở lại", !GuidedTour.Active && !UI.Kit.UIRoot.GameplayBlocked);

            // 2. Kết thúc hướng dẫn → thú đưa tin hạ xuống với lời chào
            yield return WaitUntil(() => NoticeMascot.Current != null, 2);
            yield return new WaitForSecondsRealtime(0.9f);   // chờ hạ xong
            Check("Thú đưa tin hạ xuống mang thông báo", NoticeMascot.Current != null);
            Check("Thú chỉ mang tin quan trọng: boss thế giới / túi đầy có, tinh anh / thủ lĩnh / khoe nâng cấp thì không",
                NoticeMascot.IsImportant("Boss Cửu Vĩ Ốc (cấp 30) đã xuất hiện tại Đồi Hoa Cúc! Đang có: Cửu Vĩ Ốc (Đồi Hoa Cúc).")
                && NoticeMascot.IsImportant("Hành trang đã đầy.")
                && !NoticeMascot.IsImportant("Tinh anh Ốc Sên (cấp 3) xuất hiện!")
                && !NoticeMascot.IsImportant("Thủ lĩnh Cóc Lục (cấp 5) xuất hiện!")
                && !NoticeMascot.IsImportant("Naruto vừa nâng cấp Kiếm Gỗ lên +8!"));
            yield return Shot("NoticeMascot");

            // 3. Tay không: tháo vũ khí → bấm đánh bị chặn + thú báo; server cũng nhắc. Mặc lại.
            GameData.Equipment.TryGetValue(1, out int wpn);
            Check("Vào game đã cầm vũ khí (server tự mặc vũ khí trong túi / vũ khí gỗ của hệ)", wpn > 0);
            if (wpn > 0)
            {
                GameActions.Unequip(1);
                yield return WaitUntil(() => !GameData.Equipment.ContainsKey(1), 5);
                yield return new WaitForSecondsRealtime(4f);   // hết lần nhắc trước (3 giây / lần)
                yield return WaitUntil(() => NoticeMascot.Current == null, 12);   // lời chào cuối hướng dẫn đã kéo lên
                int skill = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : -1;
                SkillCaster.TryCast(skill);
                yield return new WaitForSecondsRealtime(0.2f);
                Check("Tháo vũ khí → bấm đánh bị chặn, thú báo \"Chưa cầm vũ khí\"",
                    SkillCaster.Unarmed && NoticeMascot.Current != null && NoticeMascot.Current.Contains("vũ khí"));
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Shot("NoticeMascot_VuKhi");
                int idx = GameData.Inventory.FindIndex(x => x.tpl == wpn);
                if (idx >= 0) GameActions.Equip(idx);
                yield return WaitUntil(() => GameData.Equipment.ContainsKey(1), 5);
                Check("Mặc lại vũ khí → đánh được", !SkillCaster.Unarmed);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return WaitUntil(() => NoticeMascot.Current == null, 12);
            CheckHudClicks();
            yield return CheckCompactHud();
            // Mũi tên nhỏ trên NPC chỉ hiện khi NPC đó đang được chọn (2026-11-16)
            var tg = FindAnyObjectByType<Player.PlayerTargeting>();
            if (tg != null) tg.ClearTarget();
            var npcs = NetworkNpcManager.Instance != null ? NetworkNpcManager.Instance.All : null;
            int nNpc = 0, nArrow = 0;
            Entities.NpcEntity first = null;
            if (npcs != null) foreach (var n in npcs) { nNpc++; if (n.ArrowShown) nArrow++; if (first == null) first = n; }
            Check($"NPC chưa chọn thì KHÔNG có mũi tên ({nArrow}/{nNpc} đang hiện)", nNpc > 0 && nArrow == 0);
            if (tg != null && first != null)
            {
                tg.SelectTarget(first);
                yield return null;
                Check("Chọn NPC → hiện mũi tên nhỏ trên tên NPC đó", first.ArrowShown);
                yield return Shot("Npc_chon");
                tg.ClearTarget();
                yield return null;
                Check("Bỏ chọn → mũi tên tắt", !first.ArrowShown);
            }
        }

        /// <summary>Khung chat + khung nhiệm vụ thu gọn mặc định, bấm nút mũi tên / "Xem" thì mở rộng (vẫn nhỏ hơn bản cũ 470×170 / 300×140).</summary>
        private IEnumerator CheckCompactHud()
        {
            var chat = FindAnyObjectByType<ChatOverlay>();
            var quest = GameObject.Find("QuestTracker");
            var qBtn = GameObject.Find("QuestExpand")?.GetComponent<UnityEngine.UI.Button>();
            if (chat == null || quest == null || qBtn == null) { Check("Có khung chat + khung nhiệm vụ + nút Xem", false); yield break; }
            if (chat.Expanded) chat.Toggle();
            var qrt = (RectTransform)quest.transform;
            if (qrt.rect.height > 60f) qBtn.onClick.Invoke();   // đang mở rộng (lần chạy trước) → thu lại
            yield return null;
            float chatSmall = ((RectTransform)chat.transform).rect.height, questSmall = qrt.rect.height;
            yield return Shot("Hud_gon");
            chat.Toggle();
            qBtn.onClick.Invoke();
            yield return null;
            float chatBig = ((RectTransform)chat.transform).rect.height, questBig = qrt.rect.height;
            yield return Shot("Hud_morong");
            Check($"Khung chat thu gọn {chatSmall:0} px → mở rộng {chatBig:0} px (< 170 bản cũ)", chatSmall <= 50f && chatBig > chatSmall && chatBig < 170f);
            Check($"Khung nhiệm vụ thu gọn {questSmall:0} px → bấm Xem {questBig:0} px", questSmall <= 52f && questBig > questSmall);
            chat.Toggle();
            qBtn.onClick.Invoke();
            yield return null;

            // Khung boss: giả gói EVENT_STATE (server mới không ghi khu; server cũ còn ", khu 1" → client vẫn bỏ)
            int oldType = GameData.EventType, oldState = GameData.EventState; float oldEnd = GameData.EventEndTime; string oldText = GameData.EventText;
            GameData.EventType = 1; GameData.EventState = 2; GameData.EventEndTime = Time.time + 600;
            GameData.EventText = "Boss thế giới đang xuất hiện: Cửu Vĩ Ốc (Đồi Hoa Cúc), Thạch Ma Vương (Thung Lũng Đá, khu 1)!";
            yield return new WaitForSecondsRealtime(0.4f);
            var ev = GameObject.Find("EventPanel");
            var evText = ev != null ? ev.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            string t = evText != null ? evText.text : "";
            yield return Shot("Hud_boss");
            Check("Khung boss: mỗi boss 1 dòng \"[TG] Tên - Map\", không ghi khu, khung hẹp ≤ 290 (cũ 380)",
                t.Contains("[TG]</color> <b><color=#ffe08a>Cửu Vĩ Ốc</color></b> <color=#9fd8ff>- Đồi Hoa Cúc")
                && t.Contains("Thạch Ma Vương</color></b> <color=#9fd8ff>- Thung Lũng Đá</color>") && !t.Contains("khu")
                && ev != null && ((RectTransform)ev.transform).rect.width <= 291f);
            GameData.EventType = oldType; GameData.EventState = oldState; GameData.EventEndTime = oldEnd; GameData.EventText = oldText;
        }

        /// <summary>
        /// Mọi nút đang hiện trên màn hình (không tính cửa sổ đang mở): bắn tia click vào GIỮA nút, xem cái trên cùng có phải chính
        /// nút đó (hoặc con của nó) không. Không phải → có UI trong suốt nằm đè chặn click → in tên + đường dẫn cái chặn.
        /// </summary>
        private void CheckHudClicks()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) { Check("Có EventSystem để kiểm click HUD", false); return; }
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            int total = 0, blocked = 0;
            foreach (var sel in Object.FindObjectsOfType<UnityEngine.UI.Selectable>())
            {
                if (!sel.isActiveAndEnabled || !sel.interactable) continue;
                if (sel.GetComponentInParent<UI.Kit.GameWindow>() != null) continue;   // nút trong cửa sổ
                var rt = (RectTransform)sel.transform;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                var canvas = sel.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, (c[0] + c[2]) / 2f);
                if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                total++;
                hits.Clear();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = sp }, hits);
                var top = hits.Count > 0 ? hits[0].gameObject : null;
                if (top != null && top.transform.IsChildOf(sel.transform)) continue;
                // chính nút nhận click nếu cái trên cùng không bắt sự kiện click nhưng nút nằm ngay dưới (ExecuteEvents bubble lên cha)
                var handler = top != null ? UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(top) : null;
                if (handler != null && handler.transform.IsChildOf(sel.transform)) continue;
                blocked++;
                Debug.Log($"[HudHit] BỊ CHẶN: {PathOf(sel.transform)} @({sp.x:0},{sp.y:0}) ← trên cùng: {(top != null ? PathOf(top.transform) : "(không có gì)")}");
            }
            Check($"Bấm được mọi nút HUD ({total - blocked}/{total}, nút bị UI khác che: {blocked})", blocked == 0);
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }
    }
}
#endif
