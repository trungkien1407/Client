#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System.Collections;
using Assets.Script.Combat;
using Assets.Script.Data;
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
                int idx = GameData.Inventory.FindIndex(x => x.tpl == wpn);
                if (idx >= 0) GameActions.Equip(idx);
                yield return WaitUntil(() => GameData.Equipment.ContainsKey(1), 5);
                Check("Mặc lại vũ khí → đánh được", !SkillCaster.Unarmed);
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
#endif
