#if AUTOTEST || UNITY_EDITOR   // code tự test: chỉ có trong Editor + bản build dev (BuildTool.BuildWindows bật cờ AUTOTEST); bản phát hành không có
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Script.Data;
using Assets.Script.Entities;
using Assets.Script.Manager;
using UnityEngine;

namespace Assets.Script.Core
{
    // Autotest "-npconly": ghép THÂN + CHÂN của mọi bộ hình NPC ngay trong game (đúng cách game vẽ) → 1 tấm NpcArtSheet.png để soi chỗ nối.
    public partial class AutoTestRunner
    {
        /// <summary>
        /// Đứng ở Làng Lá, lấy 1 NPC, lần lượt gắn từng bộ hình (NpcEntity.ApplyArt — y như server gửi), cắt khung quanh NPC trên màn hình
        /// và dán vào 1 tấm ảnh lưới 8 cột (thứ tự bộ hình ghi trong log "NPCSHEET"). Ẩn người chơi + NPC khác cho khỏi che.
        /// Chỉnh vị trí thân: Editor DemoArtTool.NpcFitOverride → Tools → Naruto → Demo art → 6 → build → chạy lại.
        /// </summary>
        private IEnumerator NpcArtShots()
        {
            GameActions.Chat(1, "/tele 1");
            yield return WaitUntil(() => GameData.MapName == "Làng Lá", 8);
            yield return new WaitForSecondsRealtime(1.5f);
            var me = NetworkPlayerManager.Instance.localPlayer;
            var npcs = FindObjectsByType<NpcEntity>(FindObjectsSortMode.None);
            var npc = npcs.OrderBy(n => Mathf.Abs(n.transform.position.x - me.transform.position.x)).FirstOrDefault();
            Check("Có NPC để ghép hình", npc != null);
            if (npc == null) yield break;
            GameActions.Chat(1, "/go " + (npc.transform.position.x + 4f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " "
                                + (npc.transform.position.y + 0.2f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            yield return new WaitForSecondsRealtime(1.2f);
            foreach (var r in me.GetComponentsInChildren<Renderer>()) r.enabled = false;   // ẩn người chơi
            foreach (var n in npcs) if (n != npc) n.gameObject.SetActive(false);         // chỉ giữ 1 NPC

            // bộ hình có trong kho (demo: Resources/NpcArt/NpcArt_{art})
            var arts = new List<int>();
            for (int a = 0; a < 200; a++) if (Resources.Load<Models.NpcArtSO>("NpcArt/" + Models.NpcArtSO.Address(a)) != null) arts.Add(a);
            Check($"Có bộ hình NPC trong kho ({arts.Count} bộ)", arts.Count > 0);
            if (arts.Count == 0) yield break;

            const int cols = 8, cw = 220, ch = 280;
            int rows = (arts.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * cw, rows * ch, TextureFormat.RGB24, false);
            var cam = Camera.main;
            for (int i = 0; i < arts.Count; i++)
            {
                npc.ApplyArt(arts[i], 1f);
                yield return WaitUntil(() => npc.headRenderer.sprite != null && npc.AppliedArt == arts[i], 3);
                yield return new WaitForSecondsRealtime(0.25f);
                yield return new WaitForEndOfFrame();
                // khung cắt: chân NPC ở giữa đáy (chừa 20 px), cao ch, rộng cw — toạ độ màn hình (gốc dưới trái)
                Vector3 feet = cam.WorldToScreenPoint(npc.transform.position);
                float x0 = Mathf.Clamp(feet.x - cw / 2f, 0, Screen.width - cw), y0 = Mathf.Clamp(feet.y - 20f, 0, Screen.height - ch);
                var crop = new Texture2D(cw, ch, TextureFormat.RGB24, false);
                crop.ReadPixels(new Rect(x0, y0, cw, ch), 0, 0);
                crop.Apply();
                int c = i % cols, r = i / cols;
                sheet.SetPixels(c * cw, (rows - 1 - r) * ch, cw, ch, crop.GetPixels());
                Destroy(crop);
            }
            sheet.Apply();
            string dir = Arg("-shots", null);
            if (!string.IsNullOrEmpty(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "NpcArtSheet.png"), sheet.EncodeToPNG());
            }
            Debug.Log("[AutoTest] NPCSHEET thứ tự (8 / hàng): " + string.Join(", ", arts));
            Check($"Đã ghép {arts.Count} bộ hình NPC vào NpcArtSheet.png", true);
            Destroy(sheet);
            foreach (var r in me.GetComponentsInChildren<Renderer>()) r.enabled = true;
        }
    }
}
#endif
