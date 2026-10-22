using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// GẮN VA CHẠM NƯỚC cho các map prefab (Assets/Prefabs/Maps/Map_*.prefab).
/// Object "Water" của map phải có TilemapCollider2D (Is Trigger) + tag "Water" thì PlayerMovement mới biết đang ở nước
/// (đi trên mặt nước / chìm). Trước 2026-10-22 các map thiếu cả hai → nước như không khí.
/// Chỉ thêm thứ còn thiếu, chạy lại nhiều lần không sao. Không đổi ô → không cần xuất lại data/maps.
///   Menu: Tools → Naruto → Map → 5. Gắn va chạm nước
///   Batch: Unity -batchmode -quit -executeMethod WaterTool.BatchFix
/// </summary>
public static class WaterTool
{
    private const string MapDir = "Assets/Prefabs/Maps";
    public const string WaterTag = "Water";

    [MenuItem("Tools/Naruto/Map/5. Gắn va chạm nước (trigger + tag Water)", priority = 34)]
    public static void Menu()
    {
        var log = new List<string>();
        int n = FixAll(log);
        EditorUtility.DisplayDialog("Gắn va chạm nước", n == 0 ? "Mọi map đã đủ." : $"Đã sửa {n} map:\n" + string.Join("\n", log), "OK");
    }

    public static void BatchFix()
    {
        var log = new List<string>();
        int n = FixAll(log);
        Debug.Log($"[WaterTool] Đã sửa {n} map\n" + string.Join("\n", log));
    }

    private static int FixAll(List<string> log)
    {
        int total = 0;
        foreach (var path in Directory.GetFiles(MapDir, "Map_*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var water = root.transform.Find("Water");
                if (water == null || !Ensure(water.gameObject, out string what)) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"{Path.GetFileNameWithoutExtension(path)}: {what}");
                total++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return total;
    }

    /// <summary>Thêm collider trigger + tag nếu thiếu. Trả true nếu có đổi.</summary>
    public static bool Ensure(GameObject water, out string what)
    {
        var done = new List<string>();
        if (water.GetComponent<Tilemap>() != null)
        {
            var c = water.GetComponent<TilemapCollider2D>();
            if (c == null) { c = water.AddComponent<TilemapCollider2D>(); done.Add("thêm TilemapCollider2D"); }
            if (!c.isTrigger) { c.isTrigger = true; done.Add("Is Trigger"); }
        }
        if (!water.CompareTag(WaterTag)) { water.tag = WaterTag; done.Add("tag Water"); }
        what = string.Join(", ", done);
        return done.Count > 0;
    }

    /// <summary>Object Water có ô nước mà thiếu trigger / tag (MapDataTool cảnh báo lúc xuất map).</summary>
    public static bool IsMisconfigured(Transform water)
    {
        if (water == null) return false;
        var tm = water.GetComponent<Tilemap>();
        if (tm == null || tm.GetUsedTilesCount() == 0) return false;
        var c = water.GetComponent<TilemapCollider2D>();
        return c == null || !c.isTrigger || !water.CompareTag(WaterTag);
    }
}
