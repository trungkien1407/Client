using System.Collections.Generic;
using Assets.Script.Database;
using Assets.Script.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// ĐỒNG BỘ CẤU HÌNH HÌNH ẢNH với nội dung server (db/patch_gd1_content.sql).
/// Menu: Tools/Naruto/5. Cấu hình hình ảnh quái & NPC (GĐ1)
///
/// Vì client mới có 2 bộ hình quái ("0_*", "3_*") và 1 hình NPC, nhiều loại dùng chung hình.
/// [CẦN ĐIỀN khi có art mới] vẽ sprite "<key>_0.._3" vào atlas quái rồi sửa bảng MOBS bên dưới
/// (hoặc sửa trực tiếp Assets/SO/MobDatabase.asset trong Inspector) — chạy lại menu không ghi đè dòng đã có spriteKey riêng.
/// </summary>
public static class ContentSetupTool
{
    // templateId (khớp monster_template.id server), tên, spriteKey, scale
    private static readonly (short id, string name, string key, float scale)[] MOBS =
    {
        (1, "Mộc Nhân", "0", 1f),
        (2, "Ốc Sên", "3", 1f),
        (3, "Cóc Lục", "3", 1.15f),
        (4, "Ốc Sên Tinh Anh", "3", 1.35f),
        (5, "Cóc Chúa", "3", 1.8f),
    };

    // templateId (npc_template.id), tên mặc định, sprite đầu, sprite chân
    private static readonly (int id, string name, string head, string leg)[] NPCS =
    {
        (1, "Hokage", "TsunadeBody", "tsunadeLeg"),
        (2, "Thợ Rèn", "TsunadeBody", "tsunadeLeg"),
    };

    [MenuItem("Tools/Naruto/5. Cấu hình hình ảnh quái & NPC (GĐ1)", priority = 5)]
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
