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
    // Autotest — thú cưng / thú cưỡi (GĐ27–28) + chụp 24 thú cưỡi (-mountsonly).
    public partial class AutoTestRunner
    {
        /// <summary>
        /// GĐ27 thú cưng + GĐ28 thú cưỡi / dòng tiềm năng: /pet 11 12 (Sói Băng cấp 12 → dáng 2) → PET_LIST (có dòng tiềm năng) +
        /// đuôi PLAYER_PVP_INFO (art, dáng, đang cưỡi) → PetMount vẽ thú DƯỚI CHÂN, nhân vật diễn "ride" và được nâng lên lưng thú.
        /// Chạy: thú anim run, chạy nhanh × 1,3. Phím R (ToggleLocal) xuống / lên. Ra đòn → tự xuống. Cửa sổ Thú cưng: danh sách,
        /// hình xem trước, dòng tiềm năng. Cho ăn → EXP tăng. Cất → hết hình, nhân vật đứng đất. Phóng sinh hết.
        /// Chạy kèm "-mounts" → chụp lần lượt cả 24 thú cưỡi (Mount_01..24.png) để chỉnh cỡ / chỗ ngồi (PetMount.Tune).
        /// Không có hình demo → bỏ qua phần hình.
        /// </summary>
        private IEnumerator CheckPets()
        {
            var me = NetworkPlayerManager.Instance.localPlayer;
            me.TryGetComponent(out PlayerVisualController vis);
            GameActions.PetListRequest();
            yield return WaitUntil(() => GameData.PetListReceived, 5);
            foreach (var old in GameData.Pets.ToArray()) GameActions.PetRelease(old.uid);   // lần chạy trước còn sót
            yield return WaitUntil(() => GameData.Pets.Count == 0, 5);
            // Quái đang đánh mình (ca trước còn gây hấn) → bị đánh là ngã khỏi thú, ca "đang cưỡi" hỏng oan → dọn khu trước
            GameActions.Chat(1, "/killall");
            yield return new WaitForSecondsRealtime(0.5f);
            GameActions.Chat(1, "/pet 11 12");
            yield return WaitUntil(() => GameData.MyPet != null, 5);
            var pet = GameData.MyPet;
            yield return new WaitForSecondsRealtime(0.3f);
            var autoOpened = GameWindow.Get<PetWindow>();
            Check("Có thú mới → cửa sổ Thú cưng tự mở", autoOpened.IsOpen);
            autoOpened.Hide();   // cửa sổ mở chặn di chuyển — đóng để thử cưỡi
            Check("GM /pet 11 12 → PET_LIST có Sói Băng cấp 12 đang mang", pet != null && pet.tpl == 11 && pet.level == 12 && pet.on);
            Check("Thú cấp 12 → dáng 2, có chỉ số cộng", pet != null && pet.stage == 2 && pet.bonus[2] > 0);
            Check($"Thú Thường có 1–2 dòng tiềm năng ({(pet != null ? string.Join(", ", pet.pot.Select(PetInfo.PotText)) : "")})",
                pet != null && pet.pot.Count >= 1 && pet.pot.Count <= 2 && pet.pot.All(l => l.kind >= 1 && l.kind <= 6 && l.value > 0));
            yield return WaitUntil(() => GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv) && pv.petArt == 1024 && pv.riding, 5);
            Check("Đuôi PLAYER_PVP_INFO: thú của mình (art 1024, dáng 2) + đang cưỡi",
                GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pvi) && pvi.petArt == 1024 && pvi.petStage == 2 && pvi.riding);
            Check("Đang cưỡi → chạy nhanh × 1,3 (LocalPlayerState.RideFactor)", LocalPlayerState.Riding && Mathf.Approximately(LocalPlayerState.RideFactor, PetMount.SpeedBonus));

            PetMount.Preload(1024);
            yield return new WaitForSecondsRealtime(1.5f);
            bool art = Core.AssetSource.Cached<Spine.Unity.SkeletonDataAsset>("Pet_1024") != null;
            me.TryGetComponent(out PetMount pm);
            if (!art) Debug.Log("[AutoTest] INFO chưa có hình thú demo (Resources/Pets) — bỏ qua phần hình thú cưỡi");
            else
            {
                yield return WaitUntil(() => pm != null && pm.Showing, 5);
                Check("Thú cưỡi hiện dưới chân nhân vật (PetMount, Spine)", pm != null && pm.Showing);
                Check("Thú dùng skin dáng 2 (lv2)", pm != null && pm.Showing && pm.Skeleton.Skeleton.Skin != null && pm.Skeleton.Skeleton.Skin.Name == "lv2");
                Check("Thú ngoài màn hình không dựng lưới (updateWhenInvisible = OnlyAnimationStatus)",
                    pm != null && pm.Showing && pm.Skeleton.updateWhenInvisible == Spine.Unity.UpdateMode.OnlyAnimationStatus);
                float dx0 = pm != null && pm.Showing ? Mathf.Abs(pm.Skeleton.transform.position.x - me.transform.position.x) : 99f;
                Check($"Thú đứng đúng chỗ chân chủ (lệch {dx0:F2})", dx0 < 0.05f);
                Check($"Nhân vật ngồi trên lưng thú: nâng {(vis != null ? vis.RideLift : 0):F2} ô, anim \"{vis?.mainSkeleton?.AnimationName}\"",
                    vis != null && vis.IsRiding && vis.RideLift > 0.3f && vis.mainSkeleton.AnimationName == PlayerVisualController.RideAnim);
                var ds = pm != null ? pm.DrawnSize : Vector2.zero;
                bool sameFacing = pm != null && pm.Showing && vis.mainSkeleton.skeleton != null
                    && (vis.mainSkeleton.skeleton.ScaleX > 0) == (pm.Skeleton.transform.localScale.x < 0);
                Check($"Thú cưỡi đủ to (lưng cao {pm.DrawnBack:F2}, cả hình cao {ds.x:F2}, rộng {ds.y:F2} — người cao ~1,6) + cùng hướng nhìn với nhân vật", pm.DrawnBack >= 0.8f && ds.y <= PetMount.MaxWidth * 1.2f && sameFacing);
                yield return Shot("Mount");
                // Chủ chạy sang phải 3 ô → thú anim run; nhân vật vẫn "ride"; thú vẫn dưới chân
                float startX = me.transform.position.x, t0 = Time.realtimeSinceStartup;
                me.AutoMoveX = 1f;
                string runAnim = null, riderAnim = null;
                float until = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < until && me.transform.position.x < startX + 3f)
                {
                    if (pm != null && pm.Showing && pm.Skeleton.AnimationState.GetCurrent(0)?.Animation?.Name == "run") runAnim = "run";
                    if (vis != null && me.transform.position.x > startX + 1f) riderAnim ??= vis.mainSkeleton.AnimationName;
                    yield return null;
                }
                float runTime = Time.realtimeSinceStartup - t0;
                me.AutoMoveX = 0f;
                yield return Shot("Mount_Run");
                yield return new WaitForSecondsRealtime(0.6f);
                float dx1 = pm != null && pm.Showing ? Mathf.Abs(pm.Skeleton.transform.position.x - me.transform.position.x) : 99f;
                Check($"Cưỡi chạy {me.transform.position.x - startX:F1} ô trong {runTime:F2} giây: thú anim {runAnim ?? "không"}, người \"{riderAnim}\", thú dưới chân (lệch {dx1:F2})",
                    runAnim == "run" && riderAnim == PlayerVisualController.RideAnim && dx1 < 0.05f);
            }

            // Phím R: xuống / lên
            PetMount.ToggleLocal();
            yield return WaitUntil(() => !LocalPlayerState.Riding, 3);
            yield return new WaitForSecondsRealtime(0.2f);
            Check("Phím R → xuống thú: hết cưỡi, hết hình thú, nhân vật đứng đất",
                !LocalPlayerState.Riding && (pm == null || !pm.Showing) && (vis == null || (!vis.IsRiding && vis.RideLift == 0f)));
            PetMount.ToggleLocal();
            yield return WaitUntil(() => LocalPlayerState.Riding, 3);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Phím R lần nữa → lên lại", LocalPlayerState.Riding && (!art || (pm != null && pm.Showing && vis.IsRiding)));
            // Ra đòn → xuống ngay (client đoán trước) và server cũng báo xuống
            int skill = SkillBarManager.Instance != null ? SkillBarManager.Instance.GetSelectedSkillId() : 0;
            if (skill > 0)
            {
                GameActions.UseSkill(skill, AttackTarget.Mob, 0);
                Check("Ra đòn khi đang cưỡi → xuống ngay (không chờ server)", !LocalPlayerState.Riding && (vis == null || !vis.IsRiding));
                yield return new WaitForSecondsRealtime(0.8f);
                Check("… server cũng báo đã xuống (PLAYER_PVP_INFO riding = 0)",
                    GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv2) && !pv2.riding && !LocalPlayerState.Riding);
                float tDown = Time.realtimeSinceStartup;
                yield return WaitUntil(() => LocalPlayerState.Riding, 7);
                float tUp = Time.realtimeSinceStartup - tDown;
                Check($"3 giây không đánh / bị đánh → tự lên lại thú (sau {tUp:F1} giây)", LocalPlayerState.Riding && tUp >= 2.2f);
            }

            // GĐ28 thể lực: thú mới đầy 1000; hết → xuống, không lên được; Linh Thảo +200 → tự lên lại
            var cur = GameData.MyPet;
            Check($"Thú mới: thể lực {cur?.stamina} / {cur?.staminaMax}", cur != null && cur.stamina == 1000 && cur.staminaMax == 1000);
            GameActions.Chat(1, "/petsta 0");
            yield return WaitUntil(() => !LocalPlayerState.Riding && GameData.MyPet != null && GameData.MyPet.stamina == 0, 4);
            Check("Hết thể lực → tự xuống thú", !LocalPlayerState.Riding && GameData.MyPet != null && GameData.MyPet.stamina == 0);
            GameActions.Chat(1, "/item 145 1");
            yield return WaitUntil(() => GameData.CountItem(145) >= 1, 5);
            GameActions.UseItem(145);
            yield return WaitUntil(() => GameData.MyPet != null && GameData.MyPet.stamina == 200, 5);
            Check("Dùng Linh Thảo → thể lực 200", GameData.MyPet != null && GameData.MyPet.stamina == 200);
            yield return WaitUntil(() => LocalPlayerState.Riding, 6);
            Check("Có thể lực lại → tự lên thú", LocalPlayerState.Riding);

            var pw = GameWindow.Open<PetWindow>();
            yield return new WaitForSecondsRealtime(0.8f);
            Check("Cửa sổ Thú cưng (P) liệt kê thú", pw.IsOpen && pw.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length > 2);
            Check("Cửa sổ Thú cưng hiện thể lực",
                pw.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Any(t => t.text.Contains("Thể lực")));
            Check("Cửa sổ Thú cưng hiện dòng tiềm năng",
                pw.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Any(t => t.text.Contains("Tiềm năng") && pet != null && pet.pot.Count > 0 && t.text.Contains(PetInfo.PotNames[pet.pot[0].kind])));
            if (art)
            {
                yield return WaitUntil(() => pw.GetComponentInChildren<Spine.Unity.SkeletonGraphic>() != null, 3);
                Check("Cửa sổ Thú cưng có hình động xem trước (SkeletonGraphic)", pw.GetComponentInChildren<Spine.Unity.SkeletonGraphic>() != null);
            }
            GameActions.Chat(1, "/item 143 3");
            yield return WaitUntil(() => GameData.CountItem(143) >= 3, 5);
            yield return Shot("PetWindow");
            int exp0 = pet != null ? pet.exp : 0, lv0 = pet != null ? pet.level : 0, meat0 = GameData.CountItem(143);
            GameActions.PetFeed(pet != null ? pet.uid : 0, 143, 2);
            yield return WaitUntil(() => GameData.CountItem(143) == meat0 - 2, 5);
            var fed = GameData.MyPet;
            Check("Cho ăn 2 Thịt Tươi → thú +600 EXP, túi −2",
                fed != null && GameData.CountItem(143) == meat0 - 2 && (fed.level > lv0 || fed.exp == exp0 + 600));
            pw.Hide();

            // -mounts: chụp cả 24 thú cưỡi để chỉnh cỡ / chỗ ngồi
            if (art && Array.IndexOf(Environment.GetCommandLineArgs(), "-mounts") >= 0)
                for (int tpl = 1; tpl <= 24; tpl++)
                {
                    foreach (var p in GameData.Pets.ToArray()) GameActions.PetRelease(p.uid);
                    yield return WaitUntil(() => GameData.Pets.Count == 0, 5);
                    GameActions.Chat(1, "/pet " + tpl + " 35");
                    yield return WaitUntil(() => GameData.MyPet != null, 5);
                    GameWindow.Get<PetWindow>().Hide();
                    int a = GameData.MyPet != null ? GameData.MyPet.art : 0;
                    PetMount.Preload(a);
                    yield return WaitUntil(() => pm != null && pm.Showing && pm.Art == a, 5);
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Shot($"Mount_{tpl:00}_{a}");
                }

            GameActions.PetEquip(0);
            yield return WaitUntil(() => GameData.MyPet == null, 5);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Cất thú → không còn mang, hết hình thú, nhân vật đứng đất, hết tăng tốc",
                GameData.MyPet == null && (pm == null || !pm.Showing) && (vis == null || !vis.IsRiding) && !LocalPlayerState.Riding);
            foreach (var p in GameData.Pets.ToArray()) GameActions.PetRelease(p.uid);
            yield return WaitUntil(() => GameData.Pets.Count == 0, 5);
            Check("Phóng sinh → túi thú trống", GameData.Pets.Count == 0);
        }

        /// <summary>
        /// Chụp lần lượt 24 thú cưỡi (Mount_01_443.png …) + ghi số đo từng con vào log ("[AutoTest] MOUNT ...": cao / rộng phần hình,
        /// tỉ lệ, độ nâng người) — xem ảnh rồi chỉnh PetMount.Tune (cỡ, chỗ ngồi). Ẩn NPC cho ảnh khỏi rối.
        /// </summary>
        private IEnumerator MountShots()
        {
            var me = NetworkPlayerManager.Instance.localPlayer;
            me.TryGetComponent(out PlayerVisualController vis);
            foreach (var npc in FindObjectsByType<Entities.NpcEntity>(FindObjectsSortMode.None)) npc.gameObject.SetActive(false);
            GameActions.PetListRequest();
            yield return WaitUntil(() => GameData.PetListReceived, 5);
            foreach (var p in GameData.Pets.ToArray()) GameActions.PetRelease(p.uid);
            yield return WaitUntil(() => GameData.Pets.Count == 0, 5);
            var cam = Camera.main;
            var mr = vis != null ? vis.mainSkeleton.GetComponent<MeshRenderer>() : null;
            // Ảnh nền (không thú, ẩn người) — tools/do_thu_cuoi.py so ảnh từng thú với ảnh này để tách hình thú, đo mặt lưng theo điểm ảnh
            if (mr != null) mr.enabled = false;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("MountPx_bg");
            if (mr != null) mr.enabled = true;
            for (int tpl = 1; tpl <= 24; tpl++)
            {
                foreach (var p in GameData.Pets.ToArray()) GameActions.PetRelease(p.uid);
                yield return WaitUntil(() => GameData.Pets.Count == 0, 5);
                GameActions.Chat(1, "/pet " + tpl + " 35");
                yield return WaitUntil(() => GameData.MyPet != null, 5);
                yield return null;
                GameWindow.Get<PetWindow>().Hide();
                GameActions.Chat(1, "/hp");   // đầy máu → khỏi bật số hồi máu đè lên ảnh
                int a = GameData.MyPet != null ? GameData.MyPet.art : 0;
                PetMount.Preload(a);
                me.TryGetComponent(out PetMount pm);
                yield return WaitUntil(() => pm != null && pm.Showing && pm.Art == a, 6);
                yield return new WaitForSecondsRealtime(1.2f);
                var ds = pm != null ? pm.DrawnSize : Vector2.zero;
                Debug.Log($"[AutoTest] MOUNT tpl={tpl} art={a} name={GameData.MyPet?.name} cao={ds.x:F2} rong={ds.y:F2} lung={(pm != null ? pm.DrawnBack : 0):F2} nang={(vis != null ? vis.RideLift : 0):F2}");
                Check($"Thú cưỡi {tpl} ({GameData.MyPet?.name}) hiện + người ngồi trên", pm != null && pm.Showing && vis != null && vis.IsRiding);
                yield return Shot($"Mount_{tpl:00}_{a}");
                // Ảnh đo: ẩn người, giữ thú — kèm toạ độ chân trên màn hình + số điểm ảnh / đơn vị + tỉ lệ thú đang vẽ
                if (mr != null) mr.enabled = false;
                if (pm != null && pm.Showing)
                {
                    pm.Skeleton.AnimationState.ClearTracks();
                    pm.Skeleton.Skeleton.SetToSetupPose();   // tư thế gốc (đứng yên, không nhún)
                    pm.Skeleton.Update(0f);
                }
                yield return new WaitForSecondsRealtime(0.2f);
                Vector3 feet = cam.WorldToScreenPoint(pm.Skeleton.transform.position);
                float ppu = Screen.height / (2f * cam.orthographicSize);
                Debug.Log($"[AutoTest] MOUNTPX tpl={tpl} art={a} feetX={feet.x:F1} feetY={Screen.height - feet.y:F1} ppu={ppu:F2} scale={Mathf.Abs(pm.Skeleton.transform.localScale.y):F5} screenH={Screen.height}");
                yield return Shot($"MountPx_{tpl:00}_{a}");
                var pmr = pm.Skeleton.GetComponent<MeshRenderer>();   // nền ngay tại chỗ (camera có thể xê dịch giữa các con)
                pmr.enabled = false;
                yield return Shot($"MountPxBg_{tpl:00}_{a}");
                pmr.enabled = true;
                if (mr != null) mr.enabled = true;
            }
            foreach (var p in GameData.Pets.ToArray()) GameActions.PetRelease(p.uid);
            yield return WaitUntil(() => GameData.Pets.Count == 0, 5);
        }
    }
}
