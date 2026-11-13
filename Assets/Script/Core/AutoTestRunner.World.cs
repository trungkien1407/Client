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
    // Autotest — thế giới: map, bục 1 chiều, minimap, nước, hình trên thân, ô Nón, thú cưng, bình thuốc (mục 5b–5j) + đường ống hình.
    public partial class AutoTestRunner
    {
        /// <summary>Mục 5b–5j: map / cổng, bục, nước, hiệu ứng trên thân, trang bị, thú cưng, bình thuốc.</summary>
        private IEnumerator TestWorld()
        {
            // 5b. GĐ8 — thế giới mới (14 map prefab): tên map (MAP_INFO), chữ chỉ đường ở cổng,
            //     dịch chuyển qua vài map để chụp ảnh kiểm tra bằng mắt.
            Check($"Nhận MAP_INFO → biết tên map đang đứng ({GameData.MapName})", !string.IsNullOrEmpty(GameData.MapName));
            Check("Có chữ chỉ đường ở cổng (PortalMarker)", GameObject.Find("PortalMarker") != null);
            foreach (var (mapId, mapName) in new[] { (7, "Thung Lũng Đá"), (13, "Y Thuật Đường"), (8, "Đầm Lầy Sương Mù"), (11, "Núi Tuyết") })
            {
                GameActions.Chat(1, "/tele " + mapId);
                yield return WaitUntil(() => GameData.MapName == mapName, 8);
                yield return new WaitForSecondsRealtime(1.5f); // chờ quái / NPC hiện + chữ tên map
                Check($"Sang map {mapId} ({mapName}) — nhận đúng tên map", GameData.MapName == mapName);
                yield return Shot("Map_" + mapId);
                if (mapId == 7) yield return CheckArtPipeline();
            }
            // 5g. GĐ10 — bục 1 chiều (Đồi Hoa Cúc: bục hàng 6, cột 30..36, đất dưới cao 4), minimap, bản đồ lớn, anim chiêu
            GameActions.Chat(1, "/tele 2 33.5 4.1");
            yield return WaitUntil(() => GameData.MapName == "Đồi Hoa Cúc", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            var me = NetworkPlayerManager.Instance.localPlayer;
            Check($"Map có lớp Platform (bục 1 chiều) có PlatformEffector2D",
                MapManager.Instance.currentMapInstance.transform.Find("Platform")?.GetComponent<PlatformEffector2D>() != null);
            me.GetComponent<Rigidbody2D>().velocity = new Vector2(0, 15f);      // nhảy thẳng lên từ dưới bục
            yield return new WaitForSecondsRealtime(2f);
            Check($"Nhảy xuyên bục từ dưới lên và đứng trên bục (y = {me.transform.position.y:0.00}, cần ~7)", Mathf.Abs(me.transform.position.y - 7f) < 0.3f);
            // Đứng yên lâu → Rigidbody2D ngủ; trước đây bấm xuống lúc này không rơi (bỏ va chạm nhưng thân vẫn treo)
            me.GetComponent<Rigidbody2D>().Sleep();
            yield return new WaitForSecondsRealtime(0.2f);
            Check("Bấm xuống trên bục (đang đứng yên lâu) → rơi xuyên", me.TryDropThrough());
            yield return new WaitForSecondsRealtime(1.5f);
            Check($"Rơi về mặt đất dưới bục (y = {me.transform.position.y:0.00}, cần ~4)", Mathf.Abs(me.transform.position.y - 4f) < 0.3f);
            Check($"Minimap vẽ từ tilemap ({MapImage.Width}x{MapImage.Height})", MapImage.Texture != null && MapImage.Width == 76 && GameObject.Find("Minimap") != null);
            var wm = GameWindow.Open<WorldMapWindow>();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("WorldMap");
            wm.Hide();
            var vis = me.GetComponent<PlayerVisualController>();
            int bigSkill = vis.ClassType == 2 ? 7 : vis.ClassType == 3 ? 8 : 6;   // chiêu cấp 5 của hệ
            string want = PlayerVisualController.SkillAnimFor(vis.ClassType, bigSkill);
            vis.PlaySkill(bigSkill);
            Check($"Chiêu {bigSkill} → anim chiêu trong Spine ({vis.mainSkeleton.AnimationName}, cần {want}) — hiệu ứng vẽ sẵn, không phát thêm fxCast",
                want != null && vis.mainSkeleton.AnimationName == want && vis.HasSkillAnim(bigSkill));
            Check($"Chiêu có mốc trúng đòn sau lúc bấm ({vis.HitTime - Time.time:0.00} giây)", vis.HitTime > Time.time);
            // Đánh theo nhịp anim: đang diễn chiêu lớn mà bấm đòn cơ bản → không cắt ngang anim
            SkillCaster.TryCast(1);
            Check($"Đang diễn chiêu → bấm đánh không cắt ngang ({vis.mainSkeleton.AnimationName})", vis.mainSkeleton.AnimationName == want && vis.IsActionLocked);
            yield return new WaitUntil(() => !vis.IsActionLocked);
            yield return new WaitForSecondsRealtime(0.1f);
            vis.PlaySkill(1);
            string a1 = vis.mainSkeleton.AnimationName;
            yield return new WaitForSecondsRealtime(0.65f);
            vis.PlaySkill(1);
            if (vis.ClassType != 2)
                Check($"Đòn cơ bản đấm – đá luân phiên ({a1} → {vis.mainSkeleton.AnimationName})", a1 != vis.mainSkeleton.AnimationName);
            Check("Đòn cơ bản không phát thêm hiệu ứng ra chiêu (đường chém xanh) — đấm / đá đã có hiệu ứng trong Spine",
                vis.HasBuiltInCastFx(1) && vis.HasBuiltInCastFx(2) && vis.HasBuiltInCastFx(3));

            // Cả 3 hệ: mọi chiêu (trừ bị động) đều tìm được anim có thật trong bộ Spine; chiêu lạ → anim dự phòng theo loại
            int myClass = vis.ClassType;
            var spineScales = new List<string>();
            for (int cls = 1; cls <= 3; cls++)
            {
                var load = PlayerSpawner.LoadSkeleton(cls);
                yield return new WaitUntil(() => load.IsCompleted);
                vis.SetupVisual(load.Result, null, cls);
                vis.HideBody();   // chỉ kiểm dữ liệu — không để người xem thấy nhân vật mình lóe thành Sakura / Sasuke
                if (load.Result != null) spineScales.Add(load.Result.scale.ToString("0.####"));
                var missing = new List<string>();
                foreach (var s in GameData.Skills.Values)
                {
                    if ((s.classId != 0 && s.classId != cls) || s.type == 2) continue;
                    var m = vis.ResolveMotion(s.id);
                    if (m.anim == null || vis.mainSkeleton.Skeleton.Data.FindAnimation(m.anim) == null) missing.Add(s.name);
                }
                Check($"Hệ {cls}: mọi chiêu có anim ({(missing.Count == 0 ? "đủ" : "thiếu " + string.Join(", ", missing))})", missing.Count == 0);
                string basic = vis.ResolveMotion(cls).anim;
                Check($"Hệ {cls}: đòn cơ bản → {basic}", cls == 2 ? basic == AnimNames.Throw : basic == AnimNames.Punch || basic == AnimNames.Kick);
                Check($"Hệ {cls}: chiêu chưa có trong bảng → anim dự phòng ({vis.ResolveMotion(9999).anim})", vis.ResolveMotion(9999).anim != null);
                // GĐ24: vật bay / vụ nổ khai trong bảng có thật trong bộ <Tên>FX (thiếu bộ FX demo → bỏ qua, chiêu vẫn chạy)
                yield return WaitUntil(() => Fx.CharFx.Loaded(vis.FxSet), 5);
                if (Fx.CharFx.Loaded(vis.FxSet))
                {
                    var badFx = new List<string>();
                    foreach (var s in GameData.Skills.Values)
                    {
                        if (s.classId != cls) continue;
                        var m = vis.ResolveMotion(s.id);
                        if (m.fly != null && !Fx.CharFx.Has(vis.FxSet, m.fly)) badFx.Add(s.name + ":" + m.fly);
                        if (m.impact != null && !Fx.CharFx.Has(vis.FxSet, m.impact)) badFx.Add(s.name + ":" + m.impact);
                    }
                    Check($"Hệ {cls}: hiệu ứng Spine của chiêu có trong {vis.FxSet} ({(badFx.Count == 0 ? "đủ" : "thiếu " + string.Join(", ", badFx))})", badFx.Count == 0);
                }
                else Debug.Log($"[AutoTest] INFO Hệ {cls}: chưa có bộ {vis.FxSet} (demo art) — bỏ qua kiểm vật bay");
            }
            // 3 nhân vật cùng 1 khung xương → cùng tỉ lệ nhập (SkeletonDataAsset.scale). Trước 2026-10-29 Sakura để mặc định 0,01
            // (2 bộ kia 0,004) → Sakura to gấp 2,5 lần
            Check($"3 bộ Spine nhân vật cùng tỉ lệ ({string.Join(" / ", spineScales)})", spineScales.Count == 3 && spineScales.TrueForAll(x => x == spineScales[0]));
            {
                var back = PlayerSpawner.LoadSkeleton(myClass);
                yield return new WaitUntil(() => back.IsCompleted);
                vis.SetupVisual(back.Result, null, myClass);
            }
            yield return Shot("Platform");
            yield return CheckHighSkill(vis);

            // 5h. Nước (Đầm Lầy Sương Mù, hồ 2: ô nước x 41..45, mặt nước y = 4, đáy y = 3): chạy → đi trên mặt nước,
            //     đứng yên → chìm, nhảy + chạy → lên bờ. Trước 2026-10-22 nước không có collider / tag → rơi qua như không khí.
            GameActions.Chat(1, "/tele 8 38.5 4.1");
            yield return WaitUntil(() => GameData.MapName == "Đầm Lầy Sương Mù", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            GameActions.Chat(1, "/killall");                       // quái chủ động gần hồ không quấy
            yield return new WaitForSecondsRealtime(0.5f);
            me = NetworkPlayerManager.Instance.localPlayer;
            var waterObj = MapManager.Instance.currentMapInstance.transform.Find("Water");
            Check("Lớp Water có TilemapCollider2D (Is Trigger) + tag Water",
                waterObj != null && waterObj.CompareTag("Water") && waterObj.GetComponent<TilemapCollider2D>() is TilemapCollider2D wc && wc.isTrigger);
            me.AutoMoveX = 1f;
            yield return WaitUntil(() => me.transform.position.x > 43f, 4);
            float ySurface = me.transform.position.y;
            yield return new WaitForSecondsRealtime(0.3f);   // hình trượt xuống ngang sóng
            float sink = me.GetComponent<PlayerVisualController>().WaterSink;
            yield return Shot("Water");
            Check($"Đi trên nước: hình hạ {sink:0.00} ô cho chân chạm ngọn sóng (Map/WaterLine {Map.WaterLine.Sink:0.00})", sink > Map.WaterLine.Sink - 0.03f);
            Check($"Chạy vào nước → đi trên mặt nước (x = {me.transform.position.x:0.0}, y = {ySurface:0.00}, cần ~4)", me.transform.position.x > 43f && Mathf.Abs(ySurface - 4f) < 0.25f);
            me.AutoMoveX = 0f;
            yield return new WaitForSecondsRealtime(2f);
            Check($"Đứng yên trên mặt nước → chìm xuống đáy hồ (y = {me.transform.position.y:0.00}, cần ~3)", Mathf.Abs(me.transform.position.y - 3f) < 0.3f);
            // Đang chìm: nhảy 1 lần (đo đỉnh) rồi nhảy + bấm thêm lần nữa giữa lúc bay lên → đỉnh không cao hơn (trước 2026-10-29 nhảy đôi được)
            float wy0 = me.transform.position.y, peak1 = wy0, peak2 = wy0;
            me.Jump();
            for (float t = 0; t < 1.6f; t += Time.unscaledDeltaTime) { peak1 = Mathf.Max(peak1, me.transform.position.y); yield return null; }
            yield return new WaitForSecondsRealtime(1f);   // rơi lại xuống, chìm
            me.Jump();
            yield return new WaitForSecondsRealtime(0.12f);
            me.Jump();                                      // lần 2 giữa không trung — phải bị bỏ qua
            for (float t = 0; t < 1.6f; t += Time.unscaledDeltaTime) { peak2 = Mathf.Max(peak2, me.transform.position.y); yield return null; }
            yield return new WaitForSecondsRealtime(1f);
            Check($"Đang chìm nhảy lên không nhảy đôi được (đỉnh 1 lần {peak1 - wy0:0.00} ô, bấm 2 lần {peak2 - wy0:0.00} ô)", peak1 > wy0 + 0.5f && peak2 <= peak1 + 0.2f);
            me.AutoMoveX = 1f;
            int dust0 = Fx.FootDust.Spawned;
            me.Jump();
            yield return WaitUntil(() => me.transform.position.x > 47f && me.IsGrounded, 5);
            me.AutoMoveX = 0f;
            yield return null;
            if (AssetSource.Cached<Sprite>("dust_0") == null) Debug.Log("[AutoTest] INFO chưa có hình bụi chân (FootDust/dust_0) — bỏ qua ca bụi chân");
            else Check($"Nhảy rồi tiếp đất → tung bụi chân ({Fx.FootDust.Spawned - dust0} đám)", Fx.FootDust.Spawned > dust0);
            Check($"Nhảy + chạy → lên bờ bên kia (x = {me.transform.position.x:0.0}, y = {me.transform.position.y:0.00})", me.transform.position.x > 47f && Mathf.Abs(me.transform.position.y - 4f) < 0.3f);

            // Ra chiêu (anim Spine có vẽ sẵn hiệu ứng) rồi đổi map ngay → sang map mới KHÔNG diễn tiếp chiêu cũ
            // (lỗi cũ: nhân vật bị ẩn lúc tải map, anim dừng giữa chừng → lốc xoáy đỏ Trảm Phong "đi theo" sang map mới)
            var pvc5 = me.GetComponent<PlayerVisualController>();
            bool castAnim = pvc5 != null && pvc5.PlayActionOnce("Skill10");
            GameActions.Chat(1, "/tele 1");
            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);
            yield return new WaitForSecondsRealtime(0.3f);
            if (AudioManager.Has(GameAudio.BgmVillage))
                Check($"Về Làng Lá (không có quái) → nhạc làng (đang phát: {AudioManager.CurrentMusic})", AudioManager.CurrentMusic == GameAudio.BgmVillage);
            var track5 = pvc5 != null ? pvc5.mainSkeleton.AnimationState.GetCurrent(0) : null;
            Check($"Ra chiêu rồi đổi map → map mới không diễn tiếp chiêu cũ (anim: {track5?.Animation?.Name})",
                castAnim && track5 != null && track5.Animation.Name != "Skill10");

            // 5i. Hình trên thân (Fx.BodyAura, vẽ bằng code): /effect tự dính choáng / bỏng → sao trên đầu, tàn lửa.
            //     Mặc vũ khí +15 → server tính hào quang bậc 3 (PLAYER_PVP_INFO) → vòng đỏ dưới chân + viền sáng ô đồ. Xong trả vũ khí cũ.
            yield return new WaitForSecondsRealtime(1f);
            me = NetworkPlayerManager.Instance.localPlayer;
            GameActions.Chat(1, "/effect 1 3");
            yield return WaitUntil(() => me.TryGetComponent(out BodyAura a1) && a1.Has(StatusEffect.Stun), 5);
            me.TryGetComponent(out BodyAura aura5i);
            Check("/effect 1 → sao choáng xoay trên đầu (BodyAura)", aura5i != null && aura5i.Has(StatusEffect.Stun));
            GameActions.Chat(1, "/effect 3 3");
            yield return WaitUntil(() => aura5i != null && aura5i.Has(StatusEffect.Burn), 5);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("StatusAura");
            Check("/effect 3 → bỏng: thân nhấp nháy cam + tàn lửa", aura5i != null && aura5i.Has(StatusEffect.Burn));

            int wpn = -1, wpnReq = int.MaxValue;   // vũ khí cấp thấp nhất hệ mình mặc được
            foreach (var it in GameData.Items.Values)
                if (it.slot == 1 && (it.classRequire == 0 || it.classRequire == GameData.ClassType) && it.levelRequire <= GameData.Me.level && it.levelRequire < wpnReq) { wpn = it.id; wpnReq = it.levelRequire; }
            GameData.Equipment.TryGetValue(1, out int oldWpn);
            GameActions.Chat(1, $"/item {wpn} 1 15");
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == wpn && x.level == 15), 5);
            GameActions.Equip(GameData.Inventory.FindIndex(x => x.tpl == wpn && x.level == 15));
            yield return WaitUntil(() => GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv) && pv.glow == 3, 5);
            Check($"Mặc {GameData.ItemName(wpn)} +15 → server báo hào quang bậc 3", GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv3) && pv3.glow == 3);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Vòng hào quang đỏ dưới chân hiện", aura5i != null && aura5i.GlowTier == 3 && aura5i.RingVisible);
            yield return Shot("Glow");
            var invGlow = GameWindow.Open<InventoryWindow>();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Ô vũ khí +15 trong Hành trang có viền sáng (ItemGlow)", invGlow.GetComponentsInChildren<ItemGlow>(true).Length > 0);
            yield return Shot("WeaponGlow");
            invGlow.Hide();
            int oldIdx = oldWpn > 0 ? GameData.Inventory.FindIndex(x => x.tpl == oldWpn) : -1;
            if (oldIdx >= 0) GameActions.Equip(oldIdx); else GameActions.Unequip(1);
            // Server gửi PLAYER_PVP_INFO trước EQUIPMENT → chờ ô vũ khí đổi xong rồi mới so bậc
            yield return WaitUntil(() => oldWpn > 0 ? GameData.Equipment.TryGetValue(1, out int w1) && w1 == oldWpn : !GameData.Equipment.ContainsKey(1), 5);
            yield return new WaitForSecondsRealtime(0.3f);
            GameData.EquipLevel.TryGetValue(1, out int oldLv);
            Check("Trả vũ khí cũ → hào quang về đúng bậc của nó", GameData.Pvp.TryGetValue(LocalPlayerState.Id, out var pv1) && pv1.glow == GameData.GlowTier(oldLv));

            // 5i2. GĐ23 — ô Nón (slot 10): mặc Nón Vải (id 123, cấp 5) → server trả EQUIPMENT có ô 10; Hành trang vẽ đủ 10 ô
            GameActions.Chat(1, "/item 123 1");
            yield return WaitUntil(() => GameData.Inventory.Exists(x => x.tpl == 123), 5);
            GameActions.Equip(GameData.Inventory.FindIndex(x => x.tpl == 123));
            yield return WaitUntil(() => GameData.Equipment.TryGetValue(10, out int h0) && h0 == 123, 5);
            Check("Mặc Nón Vải → ô trang bị 10 (Nón)", GameData.Equipment.TryGetValue(10, out int hat) && hat == 123);
            var invHat = GameWindow.Open<InventoryWindow>();
            yield return new WaitForSecondsRealtime(0.5f);
            int eqCells = 0;
            foreach (var bt in invHat.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (bt.name.StartsWith("Slot")) eqCells++;
            Check($"Hành trang vẽ đủ 10 ô trang bị (đếm được {eqCells})", eqCells == GameData.EquipSlotCount);
            yield return Shot("EquipHat");
            invHat.Hide();
            GameActions.Unequip(10);
            yield return WaitUntil(() => !GameData.Equipment.ContainsKey(10), 5);

            // 5i3. GĐ27 — thú cưng: GM /pet → đi theo nhân vật (Spine, dáng theo cấp), cửa sổ P, cho ăn, cất / phóng sinh
            yield return CheckPets();

            // 5j. Bình thuốc (GĐ18): Đan Hồi Chakra (type 3) + Đan Hồi Phục (type 2) có hồi chiêu RIÊNG → dùng liền nhau đều ăn;
            //     phím Q / icon "HP" tự chọn bình (trước đây chỉ Bình Nhỏ id 1); icon HP / MP trên HUD là nút thật.
            GameActions.Chat(1, "/item 24 2"); GameActions.Chat(1, "/item 3 2"); GameActions.Chat(1, "/item 5 1");   // Bình Máu Lớn (cấp 10)
            yield return WaitUntil(() => GameData.CountItem(24) >= 2 && GameData.CountItem(3) >= 2 && GameData.CountItem(5) >= 1, 5);
            int dan24 = GameData.CountItem(24), dan3 = GameData.CountItem(3);
            GameActions.UseItem(24);
            yield return WaitUntil(() => GameData.CountItem(24) == dan24 - 1, 5);
            GameActions.UseItem(3);
            yield return WaitUntil(() => GameData.CountItem(3) == dan3 - 1, 5);
            Check($"Đan Hồi Chakra rồi Đan Hồi Phục liền nhau → cả 2 được dùng (đan chakra {dan24}→{GameData.CountItem(24)}, đan máu {dan3}→{GameData.CountItem(3)})",
                GameData.CountItem(24) == dan24 - 1 && GameData.CountItem(3) == dan3 - 1);
            GameActions.Chat(1, "/hp");
            yield return new WaitForSecondsRealtime(0.5f);
            int pickFull = QuickPotion.Pick(true);
            Check($"Máu đầy → phím Q chọn bình nhỏ nhất ({GameData.ItemName(pickFull)})", pickFull == ItemIds.HpPotion);
            LocalPlayerState.Hp = 100;   // giả máu còn ít (chỉ phía client) → phải chọn bình to nhất hiện có
            int pickLow = QuickPotion.Pick(true);
            LocalPlayerState.Hp = LocalPlayerState.MaxHp;
            Check($"Máu thiếu nhiều → chọn bình to ({GameData.ItemName(pickLow)})", GameData.Items.TryGetValue(pickLow, out var pt) && pt.hpRestore >= 300);
            GameObject hpIcon = null;
            var hudCv = GameObject.Find("HUDCanvas");
            if (hudCv != null) foreach (var b in hudCv.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (b.name == "HP") hpIcon = b.gameObject;
            Check("Icon bình HP trên HUD là nút bấm được (có số lượng)", hpIcon != null && hpIcon.GetComponent<UnityEngine.UI.Button>() != null && hpIcon.GetComponentInChildren<TMPro.TextMeshProUGUI>() != null);
        }

        /// <summary>
        /// Chụp màn hình (chỉ khi chạy có tham số "-shots &lt;thư mục&gt;", và KHÔNG dùng -nographics) để xem bố cục UI.
        /// </summary>
        /// <summary>
        /// GĐ12 — hình theo dữ liệu server: quái có monster_template.art dùng bộ hình MobAnim, hiệu ứng chiêu (GAME_DATA_EFFECTS + kho ảnh)
        /// diễn được. Chỉ kiểm khi server có cấu hình hình (data/art_demo.json hoặc hình riêng) — repo sạch thì bỏ qua.
        /// </summary>
        private IEnumerator CheckArtPipeline()
        {
            bool anyArt = false;
            foreach (var t in GameData.Mobs.Values) if (t.art >= 0) { anyArt = true; break; }
            if (anyArt)
            {
                int withArt = 0;
                foreach (var m in UnityEngine.Object.FindObjectsByType<Assets.Script.Entities.MobController>(FindObjectsSortMode.None))
                    if (m.AppliedArt >= 0) withArt++;
                Check($"Quái dùng bộ hình theo server (MobAnim): {withArt} con", withArt > 0);
            }
            // Quái thường hồi sinh thành Thủ lĩnh (ngẫu nhiên khi hồi sinh; GM /tinhanh để thử) → tên đỏ "[Thủ lĩnh]", CÙNG kích cỡ quái thường
            GameActions.Chat(1, "/tinhanh 2");
            Assets.Script.Entities.MobController leader = null;
            yield return WaitUntil(() =>
            {
                foreach (var m in UnityEngine.Object.FindObjectsByType<Assets.Script.Entities.MobController>(FindObjectsSortMode.None))
                    if (m.Rank == 2 && !m.IsDead) { leader = m; return true; }
                return false;
            }, 5);
            Assets.Script.Entities.MobController normal = null;   // cùng loại quái, hạng thường → so kích cỡ
            if (leader != null)
                foreach (var m in UnityEngine.Object.FindObjectsByType<Assets.Script.Entities.MobController>(FindObjectsSortMode.None))
                    if (m != leader && m.Rank == 0 && m.templateId == leader.templateId) { normal = m; break; }
            Check($"GM /tinhanh 2 → thấy quái Thủ lĩnh (hạng 2), cùng kích cỡ quái thường ({(leader != null ? leader.transform.localScale.x : 0):0.00} / {(normal != null ? normal.transform.localScale.x : 0):0.00})",
                leader != null && (normal == null || Mathf.Abs(leader.transform.localScale.x - normal.transform.localScale.x) < 0.01f));
            yield return Shot("LeaderMob");

            if (GameData.Effects.Count == 0) yield break;
            var me = NetworkPlayerManager.Instance.localPlayer.transform.position;
            int shown = 0;
            foreach (var s in GameData.Skills.Values)
            {
                if (s.fxHit >= 0) Fx.EffectPlayer.Play(s.fxHit, me + new Vector3(1.5f + shown * 1.6f, 0f, 0f));
                if (s.fxCast >= 0) Fx.EffectPlayer.Play(s.fxCast, me + new Vector3(1.5f + shown * 1.6f, 0f, 0f));
                if (s.fxHit >= 0 || s.fxCast >= 0) shown++;
                if (shown >= 6) break;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Check($"Hiệu ứng chiêu theo dữ liệu: {GameData.Effects.Count} mẫu, diễn thử {shown}", shown > 0 && GameObject.Find("Effects") != null);
            yield return Shot("SkillFx");
        }
    }
}
