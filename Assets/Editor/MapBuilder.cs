using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// DỰNG MAP TỪ THIẾT KẾ (GĐ8) — Menu: Tools/Naruto/6. Dựng map từ thiết kế (MapLayouts)
///
/// Thiết kế map nằm ở repo server: tools/map_design.py. Chạy script đó sẽ ghi
///   - data/maps/map_N.json          (server: va chạm, NPC, quái, cổng)
///   - Assets/MapLayouts/map_N.json  (file này đọc: tile mặt đất / nước / trang trí, màu trời, khung camera)
/// Menu này dựng Assets/Prefabs/Maps/Map_N.prefab cho từng file rồi gắn Addressables "Map_N" (group Maps).
/// Khung prefab (Grid, Ground có va chạm, Water, CameraBounds) lấy từ Assets/Prefabs/MapPrefabs_2.prefab.
///
/// Sửa map: sửa tools/map_design.py → chạy lại script → chạy lại menu này → build Addressables (menu 4) / build game.
/// KHÔNG sửa tay prefab trong Assets/Prefabs/Maps (lần dựng sau sẽ bị ghi đè). Chi tiết: docs/NOI_DUNG.md (repo server).
/// </summary>
public static class MapBuilder
{
    private const string LayoutDir = "Assets/MapLayouts";
    private const string OutDir = "Assets/Prefabs/Maps";
    private const string SkyDir = "Assets/MapLayouts/Sky";
    private const string Template = "Assets/Prefabs/MapPrefabs_2.prefab";
    private const string MapsGroup = "Maps";

    [MenuItem("Tools/Naruto/6. Dựng map từ thiết kế (MapLayouts)", priority = 6)]
    public static void BuildAll()
    {
        if (!Directory.Exists(LayoutDir)) { Debug.LogError($"[MapBuilder] Không có {LayoutDir} — chạy python tools/map_design.py ở repo server trước."); return; }
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(SkyDir);
        var tileCache = new Dictionary<string, TileBase>();
        int count = 0;
        foreach (var file in Directory.GetFiles(LayoutDir, "map_*.json").OrderBy(f => f))
        {
            BuildOne(file.Replace('\\', '/'), tileCache);
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MapBuilder] Đã dựng {count} map vào {OutDir} + gắn Addressables.");
    }

    /// <summary>Dòng lệnh: dựng map + cấu hình hình ảnh quái / NPC (ContentSetupTool).</summary>
    public static void Batch()
    {
        BuildAll();
        ContentSetupTool.Apply();
    }

    private static void BuildOne(string path, Dictionary<string, TileBase> tileCache)
    {
        var json = JObject.Parse(File.ReadAllText(path));
        int id = (int)json["mapId"];
        string outPath = $"{OutDir}/Map_{id}.prefab";
        string sky = MakeSky(id, json["sky"], json["sky2"]);

        var root = PrefabUtility.LoadPrefabContents(Template);
        try
        {
            root.name = $"Map_{id}";
            // Mốc xuất dữ liệu của MapExporter cũ — không dùng nữa (server đọc data/maps/map_N.json)
            foreach (var n in new[] { "Portal", "NPC" })
            {
                var t = root.transform.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var ground = root.transform.Find("Ground").GetComponent<Tilemap>();
            var water = root.transform.Find("Water").GetComponent<Tilemap>();
            var groundRenderer = ground.GetComponent<TilemapRenderer>();
            var decor = GetOrCreateTilemap(root.transform, "Decor", groundRenderer, -1); // sau mặt đất, trước nền trời

            Fill(ground, json["ground"], tileCache);
            Fill(water, json["water"], tileCache);
            Fill(decor, json["decor"], tileCache);

            // Khung camera (Cinemachine Confiner + minimap đọc collider này — GameMaster.SetupCameraBounds)
            var b = json["cameraBounds"];
            float x0 = (float)b[0], y0 = (float)b[1], x1 = (float)b[2], y1 = (float)b[3];
            var cb = root.transform.Find("CameraBounds");
            cb.localPosition = Vector3.zero;
            var poly = cb.GetComponent<PolygonCollider2D>();
            poly.pathCount = 1;
            poly.SetPath(0, new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) });

            // Nền trời (dải màu dọc) phủ rộng hơn khung camera
            var bgT = root.transform.Find("Background");
            if (bgT == null) { bgT = new GameObject("Background").transform; bgT.SetParent(root.transform, false); }
            // [UNITY] Không dùng "??" với component: GetComponent trả về đối tượng "null giả" mà ?? không nhận ra
            var sr = bgT.GetComponent<SpriteRenderer>();
            if (sr == null) sr = bgT.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sky);
            sr.sortingLayerID = groundRenderer.sortingLayerID;
            sr.sortingOrder = groundRenderer.sortingOrder - 100;
            float w = (x1 - x0) + 40f, h = (y1 - y0) + 20f;
            var size = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one;
            bgT.localPosition = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
            bgT.localScale = new Vector3(w / size.x, h / size.y, 1f);

            PrefabUtility.SaveAsPrefabAsset(root, outPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        RegisterAddressable(outPath, $"Map_{id}");
    }

    private static Tilemap GetOrCreateTilemap(Transform root, string name, TilemapRenderer like, int orderOffset)
    {
        var t = root.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sharedMaterial = like.sharedMaterial;
            r.sortingLayerID = like.sortingLayerID;
            r.sortingOrder = like.sortingOrder + orderOffset;
            t = go.transform;
        }
        return t.GetComponent<Tilemap>();
    }

    /// <summary>Đặt tile theo danh sách [{x, y, t}] — t = "12" (tilemap_12) hoặc "Water0".</summary>
    private static void Fill(Tilemap tm, JToken list, Dictionary<string, TileBase> cache)
    {
        tm.ClearAllTiles();
        if (list == null) return;
        var pos = new List<Vector3Int>();
        var tiles = new List<TileBase>();
        foreach (var e in list)
        {
            string name = (string)e["t"];
            if (!cache.TryGetValue(name, out var tile))
            {
                string file = name.StartsWith("Water") ? name : "tilemap_" + name;
                tile = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Tilemap/Tile/{file}.asset");
                if (tile == null) Debug.LogWarning($"[MapBuilder] Thiếu tile Assets/Tilemap/Tile/{file}.asset");
                cache[name] = tile;
            }
            if (tile == null) continue;
            pos.Add(new Vector3Int((int)e["x"], (int)e["y"], 0));
            tiles.Add(tile);
        }
        tm.SetTiles(pos.ToArray(), tiles.ToArray());
        tm.CompressBounds();
    }

    /// <summary>Tạo ảnh nền trời 4x64 (dưới sáng → trên đậm), lưu thành sprite.</summary>
    private static string MakeSky(int id, JToken top, JToken bottom)
    {
        string path = $"{SkyDir}/sky_{id}.png";
        Color c1 = ToColor(bottom), c2 = ToColor(top);
        var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
        {
            var c = Color.Lerp(c1, c2, y / 63f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = 64;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return path;
    }

    private static Color ToColor(JToken c) => c == null ? Color.white : new Color((float)c[0], (float)c[1], (float)c[2], 1f);

    private static void RegisterAddressable(string assetPath, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup(MapsGroup) ?? settings.CreateGroup(MapsGroup, false, false, true, null);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        // Gỡ entry cũ trùng address (VD MapPrefabs_1/2 trước GĐ8)
        foreach (var g in settings.groups.Where(g => g != null))
            foreach (var e in g.entries.ToList())
                if (e.address == address && e.guid != guid) g.RemoveAssetEntry(e);
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }
}
