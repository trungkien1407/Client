using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;
using Assets.Script.Map;
using Assets.Script.EditorTools;
using System.Collections.Generic;

public class MapExporter : EditorWindow
{
    private GameObject mapRoot;
    private int mapId = 1;
    private string mapName = "Làng Lá";

    [MenuItem("Tools/Ninja Map Exporter (Server Only)")]
    public static void ShowWindow() => GetWindow<MapExporter>("Map Exporter");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Chế độ Prefab Map: Tool này CHỈ xuất data logic (Collision, NPC, Mob, Portal) cho Server. Không xuất hình ảnh và Camera Bounds.", MessageType.Info);

        mapId = EditorGUILayout.IntField("Map ID", mapId);
        mapName = EditorGUILayout.TextField("Map Name", mapName);
        mapRoot = (GameObject)EditorGUILayout.ObjectField("Map Root (Prefab)", mapRoot, typeof(GameObject), true);

        if (GUILayout.Button("Export Map JSON (Server Data)"))
        {
            if (mapRoot == null)
                EditorUtility.DisplayDialog("Lỗi", "Kéo Map Root (hoặc Prefab) vào đã bạn ơi!", "OK");
            else
                Export();
        }
    }

    void Export()
    {
        // Vẫn cần ít nhất 1 Tilemap để tính toán kích thước tổng (Width/Height) cho Server
        Tilemap[] allTilemaps = mapRoot.GetComponentsInChildren<Tilemap>();
        if (allTilemaps.Length == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", "Map Root phải chứa ít nhất 1 Tilemap (dùng làm Collision/Ground) để tool tính toán kích thước lưới!", "OK");
            return;
        }

        BoundsInt totalBounds = GetFullBounds(allTilemaps);

        // Khởi tạo Data (Đã bỏ mapLayers và cameraBounds)
        MapData data = new MapData
        {
            mapId = mapId,
            mapName = mapName,
            width = totalBounds.size.x,
            height = totalBounds.size.y,
            originX = totalBounds.xMin,
            originY = totalBounds.yMin,
            collisionMap = new byte[totalBounds.size.x][],
            npcs = new List<NpcData>(),
            portals = new List<PortalData>(),
            monsters = new List<MobData>()
        };

        // Khởi tạo mảng collisionMap với giá trị 0 (0 = đi được)
        for (int i = 0; i < data.width; i++)
        {
            data.collisionMap[i] = new byte[data.height];
        }

        // 1. EXPORT COLLISION MAP (Va chạm đất/tường/nước)
        foreach (var tm in allTilemaps)
        {
            string layerName = tm.gameObject.name.ToLower();

            bool isSolidLayer = layerName.Contains("ground") || layerName.Contains("wall") || layerName.Contains("collision");
            bool isWaterLayer = layerName.Contains("water");

            if (!isSolidLayer && !isWaterLayer) continue;

            for (int x = totalBounds.xMin; x < totalBounds.xMax; x++)
            {
                for (int y = totalBounds.yMin; y < totalBounds.yMax; y++)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    TileBase paintedTile = tm.GetTile(cellPos);

                    if (paintedTile != null)
                    {
                        int localX = x - totalBounds.xMin;
                        int localY = y - totalBounds.yMin;

                        if (isSolidLayer)
                        {
                            data.collisionMap[localX][localY] = 1; // 1 = Đất/Tường
                        }
                        else if (isWaterLayer && data.collisionMap[localX][localY] == 0)
                        {
                            data.collisionMap[localX][localY] = 2; // 2 = Nước
                        }
                    }
                }
            }
        }

        // 2. Export NPCs
        NpcExportData[] npcObjects = mapRoot.GetComponentsInChildren<NpcExportData>();
        foreach (var npc in npcObjects)
        {
            Vector3 localPos = mapRoot.transform.InverseTransformPoint(npc.transform.position);
            data.npcs.Add(new NpcData
            {
                templateId = npc.templateId,
                x = localPos.x,
                y = localPos.y
            });
        }

        // 3. Export Monsters (Quái)
        MonsterExportData[] mobObjects = mapRoot.GetComponentsInChildren<MonsterExportData>();
        foreach (var mob in mobObjects)
        {
            Vector3 localPos = mapRoot.transform.InverseTransformPoint(mob.transform.position);
            data.monsters.Add(new MobData
            {
                templateId = mob.templateId,
                x = localPos.x,
                y = localPos.y,
                respawnTime = mob.respawnTime
            });
        }

        // 4. Export Portals (Cổng dịch chuyển)
        PortalExportData[] portalObjects = mapRoot.GetComponentsInChildren<PortalExportData>();
        foreach (var p in portalObjects)
        {
            Vector3 localPos = mapRoot.transform.InverseTransformPoint(p.transform.position);
            data.portals.Add(new PortalData
            {
                x = localPos.x,
                y = localPos.y,
                width = p.width,
                height = p.height,
                targetMapId = p.targetMapId,
                targetX = p.targetX,
                targetY = p.targetY,
                direction = (int)p.direction
            });
        }

        // Lưu File JSON
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string path = EditorUtility.SaveFilePanel("Lưu Server Map JSON", "", $"server_map_{mapId}.json", "json");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            Debug.Log($"Xuất thành công Server Map {mapId}! Có {data.npcs.Count} NPCs, {data.monsters.Count} Quái và {data.portals.Count} Cổng. (Đã lược bỏ Camera Bounds)");
        }
    }

    BoundsInt GetFullBounds(Tilemap[] tms)
    {
        BoundsInt bounds = tms[0].cellBounds;
        foreach (var tm in tms)
        {
            tm.CompressBounds();
            bounds.xMin = Mathf.Min(bounds.xMin, tm.cellBounds.xMin);
            bounds.yMin = Mathf.Min(bounds.yMin, tm.cellBounds.yMin);
            bounds.xMax = Mathf.Max(bounds.xMax, tm.cellBounds.xMax);
            bounds.yMax = Mathf.Max(bounds.yMax, tm.cellBounds.yMax);
        }
        return bounds;
    }
}