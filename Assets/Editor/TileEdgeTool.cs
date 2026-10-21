using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// SỬA MÉP CỎ / TUYẾT trong các map prefab (Assets/Prefabs/Maps/Map_*.prefab).
/// Tileset có 3 loại ô mặt đất: MÉP TRÁI (cỏ bo tròn bên trái, sau lưng là trời), GIỮA (liền 2 bên), MÉP PHẢI.
/// Vẽ tay hay đặt nhầm ô mép ở giữa đoạn đất liền → mặt đất bị "đứt" thành từng khúc, lộ trời giữa 2 ô.
/// Công cụ này xem từng ô mặt đất có hàng xóm trái / phải là ô mặt đất không rồi chọn lại đúng loại:
///   có cả 2 bên → GIỮA · chỉ có bên phải → MÉP TRÁI · chỉ có bên trái → MÉP PHẢI · không có bên nào → ô đứng riêng.
/// Chỉ đổi HÌNH (cùng là ô đặc) → va chạm / dữ liệu map của server không đổi, không cần xuất lại data/maps.
///   Menu: Tools → Naruto → Map → 4. Sửa mép cỏ / tuyết
///   Batch: Unity -batchmode -quit -executeMethod TileEdgeTool.BatchFix
/// </summary>
public static class TileEdgeTool
{
    private const string MapDir = "Assets/Prefabs/Maps";
    private const string TileDir = "Assets/Tilemap/Tile";

    /// <summary>1 họ ô mặt đất: số ô (tilemap_N) của mép trái, giữa, mép phải, ô đứng riêng (-1 = giữ nguyên), và mọi số thuộc họ.</summary>
    private sealed class Family
    {
        public int left, mid, right, single;
        public HashSet<int> members;
    }

    // Số ô lấy từ bảng tra tileset (xem docs/noi-dung/NOI_DUNG.md §2 — bảng ô tileset). 35 là mép phải thứ hai của cỏ.
    private static readonly Family[] Families =
    {
        new Family { left = 12, mid = 9, right = 18, single = 0, members = new HashSet<int> { 0, 9, 12, 18, 35 } },     // cỏ
        new Family { left = 26, mid = 39, right = 55, single = -1, members = new HashSet<int> { 26, 39, 55 } },          // tuyết
    };

    [MenuItem("Tools/Naruto/Map/4. Sửa mép cỏ - tuyết (chọn ô mép theo hàng xóm)", priority = 33)]
    public static void Menu()
    {
        var log = new List<string>();
        int n = FixAll(log);
        EditorUtility.DisplayDialog("Sửa mép cỏ / tuyết", n == 0 ? "Không có ô nào cần sửa." : $"Đã đổi {n} ô:\n" + string.Join("\n", log), "OK");
    }

    public static void BatchFix()
    {
        var log = new List<string>();
        int n = FixAll(log);
        Debug.Log($"[TileEdgeTool] Đã đổi {n} ô\n" + string.Join("\n", log));
    }

    private static int FixAll(List<string> log)
    {
        var tiles = LoadTiles();
        int total = 0;
        foreach (var path in Directory.GetFiles(MapDir, "Map_*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            int changed = 0;
            try
            {
                foreach (var tm in root.GetComponentsInChildren<Tilemap>(true)) changed += FixTilemap(tm, tiles);
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (changed > 0) log.Add($"{Path.GetFileNameWithoutExtension(path)}: {changed} ô");
            total += changed;
        }
        return total;
    }

    /// <summary>tilemap_N → Tile (đọc thư mục Tile, tên asset = tên ô).</summary>
    private static Dictionary<int, TileBase> LoadTiles()
    {
        var map = new Dictionary<int, TileBase>();
        foreach (var guid in AssetDatabase.FindAssets("t:TileBase", new[] { TileDir }))
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null && TryIndex(t, out int n)) map[n] = t;
        }
        return map;
    }

    private static bool TryIndex(TileBase t, out int n)
    {
        n = -1;
        return t != null && t.name.StartsWith("tilemap_") && int.TryParse(t.name.Substring(8), out n);
    }

    private static Family FamilyOf(TileBase t)
    {
        if (!TryIndex(t, out int n)) return null;
        foreach (var f in Families) if (f.members.Contains(n)) return f;
        return null;
    }

    /// <summary>Mọi họ mặt đất nối liền với nhau (cỏ cạnh tuyết vẫn coi là liền — không đặt mép giữa 2 họ).</summary>
    private static bool IsSurface(TileBase t) => FamilyOf(t) != null;

    private static int FixTilemap(Tilemap tm, Dictionary<int, TileBase> tiles)
    {
        int changed = 0;
        // Tính hết trước rồi mới đổi: đổi giữa chừng không ảnh hưởng hàng xóm (họ của ô không đổi nên vẫn đúng, nhưng tách cho rõ)
        var plan = new List<(Vector3Int pos, TileBase tile)>();
        foreach (var pos in tm.cellBounds.allPositionsWithin)
        {
            var cur = tm.GetTile(pos);
            var fam = FamilyOf(cur);
            if (fam == null) continue;
            bool l = IsSurface(tm.GetTile(pos + Vector3Int.left));
            bool r = IsSurface(tm.GetTile(pos + Vector3Int.right));
            int want = l && r ? fam.mid : r ? fam.left : l ? fam.right : fam.single;
            if (want < 0) continue;
            TryIndex(cur, out int have);
            if (want == have || (want == fam.right && have == 35 && fam.members.Contains(35))) continue;   // 35 = mép phải kiểu khác, giữ
            if (tiles.TryGetValue(want, out var t)) plan.Add((pos, t));
        }
        foreach (var (pos, t) in plan)
        {
            var m = tm.GetTransformMatrix(pos);
            var c = tm.GetColor(pos);
            tm.SetTile(pos, t);
            tm.SetTransformMatrix(pos, m);   // giữ lật / xoay / màu của ô cũ
            tm.SetColor(pos, c);
            changed++;
        }
        return changed;
    }
}
