using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Script.Core;
using Spine.Unity;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// CÔNG CỤ DỌN DẸP & KIỂM TRA PROJECT (chỉ chạy trong Editor).
///
/// Menu:
///   Tools/Naruto/1. Sắp xếp Addressables  — đưa asset vào 4 group gọn gàng, đặt address ngắn, bỏ entry rác.
///   Tools/Naruto/2. Kiểm tra Project      — liệt kê mọi chỗ BẠN CẦN ĐIỀN (ô Inspector trống, thiếu key, mất script).
///
/// Chạy không cần mở Editor (Editor phải đóng):
///   unity run "D:\Game\Naruto" --editor-version 2022.3.62f3 -- -executeMethod NarutoProjectTools.BatchCleanup -logFile cleanup.log
///
/// Muốn thêm asset mới vào Addressables theo đúng quy ước → thêm 1 dòng vào bảng PLAN bên dưới rồi chạy lại menu 1.
/// </summary>
public static class NarutoProjectTools
{
    // =====================================================================
    // 1. QUY HOẠCH ADDRESSABLES — 1 dòng = 1 asset: (đường dẫn, group, address, label)
    // =====================================================================
    private const string GroupCore = "Core";             // prefab gốc + database: luôn cần khi vào game
    private const string GroupCharacters = "Characters"; // Spine nhân vật + dữ liệu hệ phái
    private const string GroupMaps = "Maps";             // prefab map (Map_<id>)
    private const string GroupAtlases = "Atlases";       // sprite atlas (UI, quái, NPC, skill)

    private struct Plan
    {
        public string path, group, address, label;
        public Plan(string p, string g, string a, string l = null) { path = p; group = g; address = a; label = l; }
    }

    private static readonly Plan[] PLAN =
    {
        // ---- Core ----
        new Plan("Assets/Prefabs/basePlayer.prefab",  GroupCore, AddressKeys.BasePlayer),
        new Plan("Assets/Prefabs/Mob.prefab",         GroupCore, "Mob"),
        new Plan("Assets/Prefabs/NpcPrefabs.prefab",  GroupCore, "Npc"),
        new Plan("Assets/SO/MobDatabase.asset",       GroupCore, "DB/MobDatabase"),
        new Plan("Assets/SO/NpcDatabase.asset",       GroupCore, "DB/NpcDatabase"),

        // ---- Characters: hệ 1 Naruto (Kiếm) · 2 Sakura (Tiêu) · 3 Sasuke (Hoả) ----
        new Plan("Assets/Character/Naruto/Naruto_SkeletonData.asset", GroupCharacters, AddressKeys.CharacterSkeleton(1)),
        new Plan("Assets/Character/Sakura/Sakura_SkeletonData.asset", GroupCharacters, AddressKeys.CharacterSkeleton(2)),
        new Plan("Assets/Character/Sasuke/Sasuke_SkeletonData.asset", GroupCharacters, AddressKeys.CharacterSkeleton(3)),
        new Plan("Assets/Character/Dausi.asset",  GroupCharacters, "Class_1", AddressKeys.ClassDataLabel),
        new Plan("Assets/Character/Hotro.asset",  GroupCharacters, "Class_2", AddressKeys.ClassDataLabel),
        new Plan("Assets/Character/Satthu.asset", GroupCharacters, "Class_3", AddressKeys.ClassDataLabel),
        new Plan("Assets/Prefabs/NarutoUI.prefab", GroupCharacters, "CharUI_1"),
        new Plan(SakuraUiPath,                     GroupCharacters, "CharUI_2"),
        new Plan("Assets/Prefabs/SasukeUI.prefab", GroupCharacters, "CharUI_3"),

        // ---- Maps (key phải là Map_<id> vì MapManager tải theo mapId server gửi) ----
        // GĐ8: prefab dựng tự động bằng Tools/Naruto/6 (MapBuilder) từ thiết kế tools/map_design.py (repo server)
        new Plan("Assets/Prefabs/Maps/Map_1.prefab", GroupMaps, AddressKeys.Map(1)),
        new Plan("Assets/Prefabs/Maps/Map_2.prefab", GroupMaps, AddressKeys.Map(2)),
        new Plan("Assets/Prefabs/Maps/Map_3.prefab", GroupMaps, AddressKeys.Map(3)),
        new Plan("Assets/Prefabs/Maps/Map_4.prefab", GroupMaps, AddressKeys.Map(4)),
        new Plan("Assets/Prefabs/Maps/Map_5.prefab", GroupMaps, AddressKeys.Map(5)),
        new Plan("Assets/Prefabs/Maps/Map_6.prefab", GroupMaps, AddressKeys.Map(6)),
        new Plan("Assets/Prefabs/Maps/Map_7.prefab", GroupMaps, AddressKeys.Map(7)),
        new Plan("Assets/Prefabs/Maps/Map_8.prefab", GroupMaps, AddressKeys.Map(8)),
        new Plan("Assets/Prefabs/Maps/Map_9.prefab", GroupMaps, AddressKeys.Map(9)),
        new Plan("Assets/Prefabs/Maps/Map_10.prefab", GroupMaps, AddressKeys.Map(10)),
        new Plan("Assets/Prefabs/Maps/Map_11.prefab", GroupMaps, AddressKeys.Map(11)),
        new Plan("Assets/Prefabs/Maps/Map_12.prefab", GroupMaps, AddressKeys.Map(12)),
        new Plan("Assets/Prefabs/Maps/Map_13.prefab", GroupMaps, AddressKeys.Map(13)),
        new Plan("Assets/Prefabs/Maps/Map_14.prefab", GroupMaps, AddressKeys.Map(14)),

        // ---- Atlases ----
        new Plan("Assets/Atlas/HUD.spriteatlasv2",               GroupAtlases, "Atlas/HUD"),
        new Plan("Assets/Atlas/NPC.spriteatlasv2",               GroupAtlases, "Atlas/NPC"),
        new Plan("Assets/Sprite/Mob/mob_1.spriteatlasv2",        GroupAtlases, "Atlas/Mob_1"),
        new Plan("Assets/Sprite/UI/bag/SkillNa.spriteatlasv2",   GroupAtlases, "Atlas/Skill"),
    };

    /// <summary>
    /// Entry rác sẽ bị GỠ khỏi Addressables (file KHÔNG bị xoá):
    ///  - map_X.json: chỉ server đọc, client không tải → nằm trong bundle là thừa.
    ///  - EmptyMapTemplate / PortalPrefab: chỉ dùng khi dựng map trong Editor (portal đã nằm sẵn trong prefab map).
    ///  - SkillDatabase: scene đã kéo thẳng vào SkillBarManager → để Addressable nữa là bị nhân đôi.
    ///  - Cả group "Image": từng ảnh lẻ đã nằm trong atlas → bị đóng gói 2 lần.
    /// </summary>
    private static readonly string[] REMOVE =
    {
        "Assets/MapData/map_1.json",
        "Assets/MapData/map_2.json",
        "Assets/Prefabs/EmptyMapTemplate.prefab",
        "Assets/Prefabs/PortalPrefab.prefab",
        "Assets/SO/SkillDatabase.asset",
    };
    private static readonly string[] REMOVE_GROUPS = { "Image", "RemoteData" };

    private const string SakuraUiPath = "Assets/Prefabs/SakuraUI.prefab";
    private const string MainScenePath = "Assets/Scenes/SampleScene.unity";

    // =====================================================================
    // MENU
    // =====================================================================

    [MenuItem("Tools/Naruto/1. Sắp xếp Addressables", priority = 1)]
    public static void OrganizeAddressablesMenu()
    {
        CreateSakuraUiPrefab();
        AssignClassSpinePrefabs();
        OrganizeAddressables();
        EditorUtility.DisplayDialog("Addressables", "Đã sắp xếp xong. Xem Console để biết chi tiết.", "OK");
    }

    [MenuItem("Tools/Naruto/2. Kiểm tra Project", priority = 2)]
    public static void ValidateMenu()
    {
        int problems = Validate();
        EditorUtility.DisplayDialog("Kiểm tra Project",
            problems == 0 ? "Không có vấn đề nào 🎉" : $"Có {problems} chỗ cần xử lý — xem Console (lọc chữ [CẦN ĐIỀN]).", "OK");
    }

    [MenuItem("Tools/Naruto/3. Dọn component mất script trong scene chính", priority = 3)]
    public static void CleanSceneMenu() => CleanMainScene();

    /// <summary>Gọi từ dòng lệnh (-executeMethod). Chạy tất cả bước dọn + kiểm tra.</summary>
    public static void BatchCleanup()
    {
        CreateSakuraUiPrefab();
        AssignClassSpinePrefabs();
        OrganizeAddressables();
        CleanMainScene();
        Validate();
    }

    /// <summary>Chỉ kiểm tra (không sửa gì) — dùng từ dòng lệnh.</summary>
    public static void BatchValidate() => Validate();

    /// <summary>
    /// Build lại nội dung Addressables. Cần khi Play Mode Script = "Use Existing Build"
    /// (Window → Asset Management → Addressables → Groups → Play Mode Script) hoặc trước khi build game.
    /// Với "Use Asset Database" thì không cần build.
    /// </summary>
    [MenuItem("Tools/Naruto/4. Build Addressables", priority = 4)]
    public static void BuildAddressables()
    {
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error))
            Debug.LogError("[Tools] Build Addressables LỖI: " + result.Error);
        else
            Debug.Log($"[Tools] Build Addressables xong: {result.FileRegistry.GetFilePaths().Count()} file, {result.Duration:F1}s");
    }

    // =====================================================================
    // BƯỚC: tạo SakuraUI.prefab (bản sao NarutoUI, đổi dữ liệu Spine sang Sakura)
    // =====================================================================
    private static void CreateSakuraUiPrefab()
    {
        const string src = "Assets/Prefabs/NarutoUI.prefab";
        if (!File.Exists(SakuraUiPath))
        {
            if (!AssetDatabase.CopyAsset(src, SakuraUiPath))
            {
                Debug.LogError($"[Tools] Không copy được {src} → {SakuraUiPath}");
                return;
            }
        }

        var sakura = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>("Assets/Character/Sakura/Sakura_SkeletonData.asset");
        var root = PrefabUtility.LoadPrefabContents(SakuraUiPath);
        try
        {
            root.name = "SakuraUI";
            var sg = root.GetComponentInChildren<SkeletonGraphic>(true);
            if (sg != null && sakura != null)
            {
                sg.skeletonDataAsset = sakura;
                sg.initialSkinName = "Normal";
                sg.startingAnimation = "Idle";
            }
            PrefabUtility.SaveAsPrefabAsset(root, SakuraUiPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[Tools] SakuraUI.prefab sẵn sàng (SkeletonGraphic → Sakura_SkeletonData).");
    }

    /// <summary>Hệ 2 (Hotro) đang trỏ nhầm spine Naruto → trỏ sang SakuraUI.</summary>
    private static void AssignClassSpinePrefabs()
    {
        var hotro = AssetDatabase.LoadAssetAtPath<NinjaClassData>("Assets/Character/Hotro.asset");
        string guid = AssetDatabase.AssetPathToGUID(SakuraUiPath);
        if (hotro == null || string.IsNullOrEmpty(guid)) return;
        if (hotro.spinePrefab == null || hotro.spinePrefab.AssetGUID != guid)
        {
            hotro.spinePrefab = new AssetReference(guid);
            EditorUtility.SetDirty(hotro);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tools] Hotro (hệ 2) → spinePrefab = SakuraUI.");
        }
    }

    // =====================================================================
    // BƯỚC: sắp xếp Addressables theo PLAN
    // =====================================================================
    private static void OrganizeAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Tools] Chưa có AddressableAssetSettings.");
            return;
        }

        // Group mặc định cũ "Default Local Group" → đổi tên thành Core (giữ nguyên cấu hình build local)
        var core = settings.FindGroup(GroupCore);
        if (core == null)
        {
            core = settings.DefaultGroup;
            core.Name = GroupCore;
        }
        settings.DefaultGroup = core;
        settings.AddLabel(AddressKeys.ClassDataLabel);

        var log = new StringBuilder("[Tools] Sắp xếp Addressables:\n");
        var planned = new HashSet<string>();

        foreach (var p in PLAN)
        {
            string guid = AssetDatabase.AssetPathToGUID(p.path);
            if (string.IsNullOrEmpty(guid) || !File.Exists(p.path))
            {
                log.AppendLine($"  ! THIẾU FILE {p.path}");
                continue;
            }
            planned.Add(guid);
            var group = GetOrCreateGroup(settings, p.group, core);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(p.address, false);
            if (!string.IsNullOrEmpty(p.label)) entry.SetLabel(p.label, true, true, false);
            log.AppendLine($"  {p.group,-11} {p.address,-16} ← {p.path}");
        }

        foreach (var path in REMOVE)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && settings.FindAssetEntry(guid) != null)
            {
                settings.RemoveAssetEntry(guid, false);
                log.AppendLine($"  - gỡ {path}");
            }
        }

        foreach (var name in REMOVE_GROUPS)
        {
            var g = settings.FindGroup(name);
            if (g == null) continue;
            foreach (var e in g.entries.ToList()) settings.RemoveAssetEntry(e.guid, false);
            settings.RemoveGroup(g);
            log.AppendLine($"  - xoá group {name}");
        }

        // Báo những entry còn lại KHÔNG có trong PLAN (bạn tự quyết giữ hay bỏ)
        foreach (var g in settings.groups.Where(g => g != null))
            foreach (var e in g.entries)
                if (!planned.Contains(e.guid) && !g.ReadOnly)
                    log.AppendLine($"  ? ngoài quy hoạch: [{g.Name}] {e.address} ({e.AssetPath}) — thêm vào PLAN hoặc gỡ tay");

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name, AddressableAssetGroup template)
    {
        var g = settings.FindGroup(name);
        if (g != null) return g;
        // Copy schema của group Core → cùng cấu hình build/load LOCAL
        // (truyền BẢN SAO danh sách — truyền thẳng template.Schemas sẽ lỗi "Collection was modified")
        return settings.CreateGroup(name, false, false, false, new List<AddressableAssetGroupSchema>(template.Schemas));
    }

    // =====================================================================
    // BƯỚC: dọn scene chính
    // =====================================================================
    /// <summary>Script đã bị thay thế, cần gỡ khỏi scene trước khi xoá file .cs (tránh "Missing Script").</summary>
    private static readonly string[] OBSOLETE_COMPONENTS = { "PlayerDataManager" };

    private static void CleanMainScene()
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        int removed = 0, missing = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                foreach (var mb in go.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && OBSOLETE_COMPONENTS.Contains(mb.GetType().Name))
                    {
                        Debug.Log($"[Tools] Gỡ {mb.GetType().Name} khỏi '{go.name}'");
                        UnityEngine.Object.DestroyImmediate(mb);
                        removed++;
                    }
                }
            }
        }
        if (removed + missing > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"[Tools] Dọn scene: gỡ {removed} component cũ, {missing} component mất script.");
    }

    // =====================================================================
    // KIỂM TRA — báo mọi chỗ cần bạn xử lý
    // =====================================================================
    public static int Validate()
    {
        var report = new List<string>();
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        // ---- 1. Key Addressables mà code gọi bằng chuỗi ----
        var addresses = new HashSet<string>(settings.groups.Where(g => g != null).SelectMany(g => g.entries).Select(e => e.address));
        string[] requiredKeys =
        {
            AddressKeys.BasePlayer, AddressKeys.Map(1), AddressKeys.Map(2),
            AddressKeys.CharacterSkeleton(1), AddressKeys.CharacterSkeleton(2), AddressKeys.CharacterSkeleton(3),
        };
        foreach (var k in requiredKeys)
            if (!addresses.Contains(k))
                report.Add($"[CẦN ĐIỀN] Addressables thiếu key \"{k}\" (code gọi bằng AddressKeys) → chạy Tools/Naruto/1 hoặc đánh dấu Addressable + đặt address.");

        var classEntries = settings.groups.Where(g => g != null).SelectMany(g => g.entries)
            .Where(e => e.labels.Contains(AddressKeys.ClassDataLabel)).ToList();
        if (classEntries.Count == 0)
            report.Add("[CẦN ĐIỀN] Không có asset nào mang label 'ClassData' → màn tạo nhân vật sẽ trống.");
        foreach (var e in classEntries)
        {
            var cls = AssetDatabase.LoadAssetAtPath<NinjaClassData>(e.AssetPath);
            if (cls == null) continue;
            if (cls.spinePrefab == null || string.IsNullOrEmpty(cls.spinePrefab.AssetGUID))
                report.Add($"[CẦN ĐIỀN] {e.AssetPath}: ô 'Spine Prefab' trống (kéo prefab UI Spine của hệ {cls.classId} vào).");
            if (cls.classId < 1 || cls.classId > 3)
                report.Add($"[CHÚ Ý] {e.AssetPath}: classId={cls.classId} — server chỉ có hệ 1,2,3.");
        }

        foreach (var g in settings.groups.Where(g => g != null && !g.ReadOnly)) // bỏ "Built In Data" (entry ảo của Unity)
            foreach (var e in g.entries)
                if (string.IsNullOrEmpty(e.AssetPath) || (!File.Exists(e.AssetPath) && !Directory.Exists(e.AssetPath)))
                    report.Add($"[LỖI] Addressables [{g.Name}] '{e.address}' trỏ tới file không tồn tại (guid {e.guid}).");

        // ---- 2. Ô Inspector trống trong scene chính + các prefab Addressable ----
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            CheckGameObjectTree(root, $"Scene {Path.GetFileNameWithoutExtension(MainScenePath)}", report);

        foreach (var e in settings.groups.Where(g => g != null).SelectMany(g => g.entries))
        {
            if (!e.AssetPath.EndsWith(".prefab")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.AssetPath);
            if (prefab != null) CheckGameObjectTree(prefab, $"Prefab {Path.GetFileName(e.AssetPath)}", report);
        }
        // (AuthCanvas/SystemCanvas nằm sẵn trong scene → đã được kiểm ở bước scene, kể cả tham chiếu tới object trong scene)

        // ---- In kết quả ----
        if (report.Count == 0)
            Debug.Log("[Kiểm tra] ✅ Không có vấn đề nào.");
        else
            Debug.LogWarning($"[Kiểm tra] {report.Count} vấn đề:\n" + string.Join("\n", report));
        return report.Count;
    }

    /// <summary>
    /// Duyệt mọi component của game (Assembly-CSharp) trong cây GameObject, báo:
    ///  - component mất script (Missing Script)
    ///  - ô kéo-thả (Object / AssetReference) đang trống mà không đánh dấu [Optional]
    /// </summary>
    private static void CheckGameObjectTree(GameObject root, string where, List<string> report)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0)
                report.Add($"[LỖI] {where} → '{PathOf(go)}' có {missing} component MẤT SCRIPT (chạy Tools/Naruto/3).");

            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Assembly.GetName().Name != "Assembly-CSharp") continue; // bỏ qua component của Unity/Spine/TMP

                foreach (var f in SerializedFields(type))
                {
                    if (f.GetCustomAttribute<OptionalAttribute>() != null) continue;
                    object value = f.GetValue(mb);
                    bool empty = false;

                    if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                        empty = (value as UnityEngine.Object) == null;          // so sánh kiểu Unity (bắt cả "fake null")
                    else if (typeof(AssetReference).IsAssignableFrom(f.FieldType))
                        empty = value == null || string.IsNullOrEmpty(((AssetReference)value).AssetGUID);
                    else if (f.FieldType.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType.GetElementType()))
                    {
                        var arr = value as Array;
                        empty = arr == null || arr.Length == 0 || arr.Cast<UnityEngine.Object>().Any(o => o == null);
                    }

                    if (empty)
                        report.Add($"[CẦN ĐIỀN] {where} → '{PathOf(go)}' → {type.Name}.{f.Name} đang trống");
                }
            }
        }
    }

    /// <summary>Các field Unity hiển thị trong Inspector: public (không [NonSerialized]/[HideInInspector]) hoặc [SerializeField].</summary>
    private static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsNotSerialized || f.GetCustomAttribute<HideInInspector>() != null) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                yield return f;
            }
        }
    }

    private static string PathOf(GameObject go)
    {
        var parts = new List<string>();
        for (var t = go.transform; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
