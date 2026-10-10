using System.Collections.Generic;
using Assets.Script.Database;
using Assets.Script.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// ĐỒNG BỘ CẤU HÌNH HÌNH ẢNH với nội dung server (db/patch_gd1_content.sql).
/// Menu: Tools/Naruto/5. Cấu hình hình ảnh quái & NPC
///
/// Vì client mới có 2 bộ hình quái ("0_*", "3_*") và 1 hình NPC, nhiều loại dùng chung hình.
/// [CẦN ĐIỀN khi có art mới] vẽ sprite "<key>_0.._3" vào atlas quái rồi sửa bảng MOBS bên dưới
/// (hoặc sửa trực tiếp Assets/SO/MobDatabase.asset trong Inspector) — chạy lại menu không ghi đè dòng đã có spriteKey riêng.
/// </summary>
public static class ContentSetupTool
{
    // templateId (khớp monster_template.id server), tên, spriteKey, scale, màu nhuộm (GĐ8: phân biệt quái mượn chung hình)
    // GĐ8: dòng 8–24 sinh từ repo server: python tools/gen_content.py (in ra bảng CLIENT_MOBS) — sửa ở đó rồi dán lại.
    private static readonly (short id, string name, string key, float scale, string tint)[] MOBS =
    {
        (1, "Mộc Nhân", "0", 1f, "#FFFFFF"),
        (2, "Ốc Sên", "3", 1f, "#FFFFFF"),
        (3, "Cóc Lục", "3", 1.15f, "#C8F0A0"),
        (4, "Ốc Sên Tinh Anh", "3", 1.35f, "#FFE08A"),
        (5, "Cóc Chúa", "3", 1.8f, "#9FD07A"),
        (6, "Ốc Vương (boss phó bản)", "3", 2.0f, "#FFB0B0"),   // GĐ4 — [CẦN ĐIỀN khi có art] đổi spriteKey
        (7, "Cửu Vĩ Ốc (boss thế giới)", "3", 2.6f, "#FF9050"), // GĐ4 — [CẦN ĐIỀN khi có art] đổi spriteKey
        // ---- GĐ8: nội dung cấp 5 → 30 — [CẦN ĐIỀN khi có art] mỗi loại 1 bộ hình riêng ----
        (8, "Cóc Độc", "3", 1.2f, "#7CFC5A"),
        (9, "Sói Rừng", "3", 1.2f, "#A0A0A0"),
        (10, "Khỉ Đá", "0", 1.1f, "#C08850"),
        (11, "Sói Đầu Đàn", "3", 1.45f, "#707070"),
        (12, "Rắn Đá", "3", 1.15f, "#B0B060"),
        (13, "Dơi Hang", "3", 1.0f, "#8060C0"),
        (14, "Nhện Độc", "3", 1.45f, "#50C050"),
        (15, "Nhện Chúa", "3", 2.0f, "#308030"),
        (16, "Ếch Độc", "3", 1.2f, "#40D0B0"),
        (17, "Cá Sấu Đầm", "3", 1.3f, "#608040"),
        (18, "Bọ Cạp", "3", 1.2f, "#FF8040"),
        (19, "Thuỷ Quái", "3", 1.5f, "#4080FF"),
        (20, "Sói Tuyết", "3", 1.25f, "#A0C8FF"),
        (21, "Gấu Tuyết", "0", 1.3f, "#B8D0FF"),
        (22, "Yêu Hồ", "3", 1.5f, "#FFA020"),
        (23, "Băng Long", "3", 2.3f, "#60B0FF"),
        (24, "Xà Vương", "3", 2.2f, "#A07030"),
        // ---- GĐ9: boss thế giới 3 mốc cấp (repo server tools/gen_story.py) — [CẦN ĐIỀN khi có art] ----
        (25, "Thạch Ma Vương (boss thế giới)", "0", 2.6f, "#8A7A6A"),
        (26, "Băng Hồ Vương (boss thế giới)", "3", 2.8f, "#9FE8FF"),
    };

    // templateId (npc_template.id), tên mặc định, sprite đầu, sprite chân
    private static readonly (int id, string name, string head, string leg)[] NPCS =
    {
        (1, "Hokage", "TsunadeBody", "tsunadeLeg"),
        (2, "Thợ Rèn", "TsunadeBody", "tsunadeLeg"),
        // GĐ8 — [CẦN ĐIỀN khi có art] hình riêng cho từng NPC
        (5, "Trưởng Làng Đá", "TsunadeBody", "tsunadeLeg"),
        (6, "Trưởng Làng Tuyết", "TsunadeBody", "tsunadeLeg"),
        (7, "Hiệu Trưởng Đấu Sĩ Đường", "TsunadeBody", "tsunadeLeg"),
        (8, "Hiệu Trưởng Y Thuật Đường", "TsunadeBody", "tsunadeLeg"),
        (9, "Hiệu Trưởng Ảnh Sát Đường", "TsunadeBody", "tsunadeLeg"),
        (10, "Chủ Chợ", "TsunadeBody", "tsunadeLeg"),   // GĐ9 — chợ ở 3 làng
    };

    [MenuItem("Tools/Naruto/5. Cấu hình hình ảnh quái & NPC", priority = 5)]
    public static void Apply()
    {
        var mobDb = AssetDatabase.LoadAssetAtPath<MobDatabaseSO>("Assets/SO/MobDatabase.asset");
        var mobAtlasGuid = AssetDatabase.AssetPathToGUID("Assets/Sprite/Mob/mob_1.spriteatlasv2");
        foreach (var m in MOBS)
        {
            var e = mobDb.mobs.Find(x => x.templateId == m.id);
            if (e == null)
            {
                e = new MobVisualData { templateId = m.id, frameRate = 0.15f, mobAtlas = new AssetReferenceT<UnityEngine.U2D.SpriteAtlas>(mobAtlasGuid) };
                mobDb.mobs.Add(e);
            }
            e.mobName = m.name;
            if (string.IsNullOrEmpty(e.spriteKey)) e.spriteKey = m.key;
            if (e.scale <= 0 || Mathf.Approximately(e.scale, 1f)) e.scale = m.scale;
            // Màu nhuộm chỉ đặt khi quái còn mượn hình (spriteKey = bảng) — có art riêng rồi thì giữ màu bạn chỉnh tay
            if (e.spriteKey == m.key && ColorUtility.TryParseHtmlString(m.tint, out var tint)) e.tint = tint;
        }
        EditorUtility.SetDirty(mobDb);

        var npcDb = AssetDatabase.LoadAssetAtPath<NpcDatabaseSO>("Assets/SO/NpcDatabase.asset");
        foreach (var n in NPCS)
        {
            var e = npcDb.npcs.Find(x => x.templateId == n.id);
            if (e == null)
            {
                e = new NpcDatabaseSO.NpcConfig { templateId = n.id, headSpriteName = n.head, legSpriteName = n.leg };
                npcDb.npcs.Add(e);
            }
            e.defaultName = n.name;
        }
        EditorUtility.SetDirty(npcDb);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Tools] Đã cấu hình {MOBS.Length} quái, {NPCS.Length} NPC.");
    }

    /// <summary>Dòng lệnh: cấu hình nội dung + kiểm tra project.</summary>
    public static void Batch()
    {
        Apply();
        NarutoProjectTools.BatchValidate();
    }
}
