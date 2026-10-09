using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Script.Map;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// PREFAB MAP LÀ NGUỒN DỮ LIỆU (thay tools/map_design.py + tool 6). Map chỉnh tay trong Assets/Prefabs/Maps/Map_N.prefab:
///   va chạm = Tilemap Ground (đặc, 1) · Platform (bục 1 chiều, 3) · Water (nước, 2); Decor không va chạm
///   NPC / quái / cổng = điểm MapNpcSpot / MapMobSpot / MapPortalSpot (con của "Spots"); MapInfo ở gốc: id, tên, khung, chỗ xuất hiện
/// Menu Tools → Naruto → Map:
///   1. Nhập điểm từ data/maps vào prefab — làm 1 lần cho map cũ (map đã có MapInfo thì bỏ qua)
///   2. Xuất map cho server — ghi data/maps/map_N.json ở repo server (server đọc khi khởi động)
/// Không mở Editor: -executeMethod MapDataTool.BatchImport / BatchExport [-mapData &lt;thư mục data/maps&gt;]
/// </summary>
public static class MapDataTool
{
    private const string PrefabDir = "Assets/Prefabs/Maps";
    private const string DirKey = "Naruto.MapDataDir";

    [MenuItem("Tools/Naruto/Map/1. Nhập điểm từ data/maps vào prefab (1 lần)", priority = 30)]
    public static void MenuImport()
    {
        string dir = DataDir(true);
        if (dir == null) return;
        EditorUtility.DisplayDialog("Map", $"Đã nhập {ImportAll(dir)} map (map đã có MapInfo thì giữ nguyên).", "OK");
    }

    [MenuItem("Tools/Naruto/Map/2. Xuất map cho server (prefab → data/maps)", priority = 31)]
    public static void MenuExport()
    {
        string dir = DataDir(true);
        if (dir == null) return;
        var warn = new List<string>();
        int n = ExportAll(dir, warn);
        EditorUtility.DisplayDialog("Map", $"Đã xuất {n} map vào {dir}" + (warn.Count > 0 ? "\n\nCảnh báo:\n" + string.Join("\n", warn.Take(15)) : ""), "OK");
    }

    [MenuItem("Tools/Naruto/Map/3. Đổi thư mục data/maps của server", priority = 32)]
    public static void MenuPickDir() => PickDir();

    public static void BatchImport() => Debug.Log($"[MapDataTool] Nhập {ImportAll(DataDir(false))} map");

    public static void BatchExport()
    {
        var warn = new List<string>();
        int n = ExportAll(DataDir(false), warn);
        foreach (var w in warn) Debug.LogWarning("[MapDataTool] " + w);
        Debug.Log($"[MapDataTool] Xuất {n} map");
    }

    // ==========================================
    // NHẬP (data/maps → prefab), 1 lần
    // ==========================================

    public static int ImportAll(string dir, bool force = false)
    {
        int n = 0;
        foreach (var path in MapPrefabs())
        {
            int id = IdOf(path);
            string file = Path.Combine(dir, $"map_{id}.json");
            if (!File.Exists(file)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<MapInfo>() != null && !force) continue;
                var j = JObject.Parse(File.ReadAllText(file));
                var info = root.GetComponent<MapInfo>();
                if (info == null) info = root.AddComponent<MapInfo>();
                info.mapId = (int)j["mapId"];
                info.mapName = (string)j["mapName"];
                info.width = (int)j["width"];
                info.height = (int)j["height"];
                info.spawn = new Vector2((float)j["spawnX"], (float)j["spawnY"]);
                info.hasBoss = j["bossX"] != null;
                if (info.hasBoss) info.boss = new Vector2((float)j["bossX"], (float)j["bossY"]);

                var old = root.transform.Find("Spots");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var spots = new GameObject("Spots").transform;
                spots.SetParent(root.transform, false);
                foreach (var e in j["npcs"])
                    Spot<MapNpcSpot>(spots, $"NPC_{e["templateId"]}", e).templateId = (int)e["templateId"];
                foreach (var e in j["monsters"])
                {
                    var m = Spot<MapMobSpot>(spots, $"Mob_{e["templateId"]}", e);
                    m.templateId = (int)e["templateId"];
                    m.respawnTime = (int)e["respawnTime"];
                }
                foreach (var e in j["portals"])
                {
                    var p = Spot<MapPortalSpot>(spots, $"Portal_to_{e["targetMapId"]}", e);
                    p.targetMapId = (int)e["targetMapId"];
                    p.target = new Vector2((float)e["targetX"], (float)e["targetY"]);
                    p.size = new Vector2((float)e["width"], (float)e["height"]);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                n++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return n;
    }

    private static T Spot<T>(Transform parent, string name, JToken e) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((float)e["x"], (float)e["y"], 0);
        return go.AddComponent<T>();
    }

    // ==========================================
    // XUẤT (prefab → data/maps)
    // ==========================================

    public static int ExportAll(string dir, List<string> warn)
    {
        Directory.CreateDirectory(dir);
        int n = 0;
        foreach (var path in MapPrefabs())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var info = root.GetComponent<MapInfo>();
                if (info == null) { warn.Add($"{Path.GetFileName(path)}: chưa có MapInfo — chạy mục 1 trước, bỏ qua"); continue; }
                var json = Export(root.transform, info, warn);
                File.WriteAllText(Path.Combine(dir, $"map_{info.mapId}.json"), json.ToString(Formatting.None));
                n++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return n;
    }

    /// <summary>Cùng định dạng tools/map_design.py — server: MapManager đọc, world/Map dựng lưới va chạm.</summary>
    private static JObject Export(Transform root, MapInfo info, List<string> warn)
    {
        string tag = $"map {info.mapId} {info.mapName}";
        int w = info.width, h = info.height;
        var coll = new byte[w, h];
        // Thứ tự giống map_design: đặc / bục trước, nước sau (nước đè)
        Mark(root, "Ground", 1, coll, w, h, warn, tag);
        Mark(root, "Platform", 3, coll, w, h, warn, tag);
        Mark(root, "Water", 2, coll, w, h, warn, tag);
        var columns = new JArray();
        for (int x = 0; x < w; x++)
        {
            var col = new byte[h];
            for (int y = 0; y < h; y++) col[y] = coll[x, y];
            columns.Add(Convert.ToBase64String(col));   // mỗi cột 1 chuỗi base64, phần tử y = hàng y
        }

        var o = new JObject
        {
            ["mapId"] = info.mapId, ["mapName"] = info.mapName, ["width"] = w, ["height"] = h, ["originX"] = 0, ["originY"] = 0,
            ["spawnX"] = R(info.spawn.x), ["spawnY"] = R(info.spawn.y),
            ["collisionMap"] = columns,
            ["npcs"] = new JArray(root.GetComponentsInChildren<MapNpcSpot>().Select(s =>
            {
                var p = Pos(root, s.transform);
                return new JObject { ["templateId"] = s.templateId, ["x"] = R(p.x), ["y"] = R(p.y) };
            })),
            ["monsters"] = new JArray(root.GetComponentsInChildren<MapMobSpot>().Select(s =>
            {
                var p = Pos(root, s.transform);
                return new JObject { ["templateId"] = s.templateId, ["x"] = R(p.x), ["y"] = R(p.y), ["respawnTime"] = s.respawnTime };
            })),
            ["portals"] = new JArray(root.GetComponentsInChildren<MapPortalSpot>().Select(s =>
            {
                var p = Pos(root, s.transform);
                if (s.targetMapId <= 0) warn.Add($"{tag}: cổng {s.name} chưa chọn map đích");
                return new JObject
                {
                    ["x"] = R(p.x), ["y"] = R(p.y), ["width"] = R(s.size.x), ["height"] = R(s.size.y),
                    ["targetMapId"] = s.targetMapId, ["targetX"] = R(s.target.x), ["targetY"] = R(s.target.y)
                };
            })),
        };
        if (info.hasBoss) { o["bossX"] = R(info.boss.x); o["bossY"] = R(info.boss.y); }
        return o;
    }

    private static void Mark(Transform root, string layer, byte value, byte[,] coll, int w, int h, List<string> warn, string tag)
    {
        var t = root.Find(layer);
        if (t == null) return;
        var tm = t.GetComponent<Tilemap>();
        tm.CompressBounds();
        int outside = 0;
        foreach (var c in tm.cellBounds.allPositionsWithin)
        {
            if (!tm.HasTile(c)) continue;
            if (c.x < 0 || c.y < 0 || c.x >= w || c.y >= h) { outside++; continue; }
            coll[c.x, c.y] = value;
        }
        if (outside > 0) warn.Add($"{tag}: {outside} ô {layer} nằm ngoài khung {w}x{h} — không tính va chạm (sửa width/height trong MapInfo)");
    }

    private static Vector2 Pos(Transform root, Transform t) => root.InverseTransformPoint(t.position);
    private static double R(float v) => Math.Round(v, 3);

    // ==========================================

    private static IEnumerable<string> MapPrefabs() => Directory.GetFiles(PrefabDir, "Map_*.prefab")
        .Select(p => p.Replace('\\', '/')).OrderBy(IdOf);

    private static int IdOf(string path) => int.Parse(Path.GetFileNameWithoutExtension(path).Substring(4));

    /// <summary>Thư mục data/maps của repo server: tham số -mapData, rồi lựa chọn đã lưu, rồi đoán D:/Server/Server cạnh project.</summary>
    private static string DataDir(bool interactive)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-mapData");
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        string dir = EditorPrefs.GetString(DirKey, "");
        if (Directory.Exists(dir)) return dir;
        string guess = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Server/Server/data/maps"));
        if (Directory.Exists(guess)) { EditorPrefs.SetString(DirKey, guess); return guess; }
        return interactive ? PickDir() : throw new DirectoryNotFoundException("Không thấy data/maps — dùng -mapData <thư mục>");
    }

    private static string PickDir()
    {
        string dir = EditorUtility.OpenFolderPanel("Chọn thư mục data/maps của repo server", EditorPrefs.GetString(DirKey, ""), "");
        if (string.IsNullOrEmpty(dir)) return null;
        EditorPrefs.SetString(DirKey, dir);
        return dir;
    }
}
