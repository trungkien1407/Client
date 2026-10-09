using System;
using System.IO;
using System.Linq;
using Assets.Script.UI.Kit;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SINH PREFAB UI từ giao diện đang dựng bằng code — chỉ để có bản đầu, sau đó bạn chỉnh bằng kéo-thả.
///   1. Bộ mẫu: Assets/Resources/UI/Kit/{Panel, Text, Button, Input, ScrollList, Bar, Window}.prefab + White.png,
///      UITheme.asset (bảng màu), UIRoot.prefab (canvas chung).
///   2. Cửa sổ: Assets/Resources/UI/Windows/&lt;Lớp&gt;.prefab — bản biến thể của Kit/Window, các nút / chữ bên trong
///      là bản lồng của Kit/Button, Kit/Text... → sửa mẫu 1 lần, mọi nơi đổi theo.
///   3. HUD: Assets/Resources/UI/Hud/&lt;Lớp&gt;.prefab (mọi lớp HudPanel — thanh EXP + menu, minimap, khung chat, nút điện thoại)
/// MẶC ĐỊNH KHÔNG GHI ĐÈ file đã có (giữ chỉnh sửa của bạn). Chỉ xuất cửa sổ đã tách Bind() (GameWindow.SupportsPrefab).
/// Chạy không mở Editor: -executeMethod UIPrefabTool.BatchGenerate
/// </summary>
public static class UIPrefabTool
{
    private const string Res = "Assets/Resources/";

    [MenuItem("Tools/Naruto/UI/1. Tạo bộ mẫu (Kit + UITheme + UIRoot)", priority = 20)]
    public static void MenuKit() => Report(GenerateKit(false));

    [MenuItem("Tools/Naruto/UI/2. Xuất cửa sổ + HUD còn thiếu ra prefab", priority = 21)]
    public static void MenuWindows() => Report(ExportWindows(false) + ExportHud(false));

    [MenuItem("Tools/Naruto/UI/3. Xuất lại cửa sổ đang chọn (ghi đè)", priority = 22)]
    public static void MenuReexportSelected()
    {
        var names = Selection.objects.Select(o => o.name).ToArray();
        var types = WindowTypes().Concat(HudTypes()).Where(t => names.Contains(t.Name)).ToArray();
        if (types.Length == 0) { EditorUtility.DisplayDialog("UI", "Chọn prefab (hoặc script) cửa sổ cần xuất lại.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("UI", "Ghi đè " + string.Join(", ", types.Select(t => t.Name)) + "?\nMọi chỉnh sửa trong prefab đó sẽ mất.", "Ghi đè", "Huỷ")) return;
        int n = 0;
        foreach (var t in types) if (typeof(HudPanel).IsAssignableFrom(t) ? ExportHudPanel(t, true) : ExportWindow(t, true)) n++;
        Report(n);
    }

    public static void BatchGenerate()
    {
        int n = GenerateKit(false) + ExportWindows(false) + ExportHud(false);
        Debug.Log($"[UIPrefabTool] Đã tạo {n} file");
    }

    // ==========================================
    // BỘ MẪU
    // ==========================================

    public static int GenerateKit(bool overwrite)
    {
        Directory.CreateDirectory(Res + UIPrefabs.KitFolder);
        Directory.CreateDirectory(Res + UIPrefabs.WindowFolder);
        Directory.CreateDirectory(Res + UIPrefabs.HudFolder);
        int n = 0;

        string white = Res + UIPrefabs.KitFolder + "White.png";
        if (overwrite || !File.Exists(white)) { MakeWhiteSprite(white); n++; }

        string theme = Res + UIPrefabs.ThemePath + ".asset";
        if (overwrite || !File.Exists(theme)) { AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UITheme>(), theme); n++; }

        // Mẫu cơ bản: dựng thuần bằng code (không lồng mẫu vào nhau)
        UIPrefabs.UseKit = false;
        try
        {
            n += Save("Panel", overwrite, () => UIKit.BuildPanel("Panel", null).gameObject);
            n += Save("Text", overwrite, () => UIKit.BuildText("Text", null).gameObject);
            n += Save("Button", overwrite, () => UIKit.BuildButton("Button", null).gameObject);
            n += Save("Input", overwrite, () => UIKit.BuildInput("Input", null).gameObject);
            n += Save("ScrollList", overwrite, () => UIKit.BuildScrollList("ScrollList", null).gameObject);
            n += Save("Bar", overwrite, () => UIKit.BuildBar("Bar", null).gameObject);
        }
        finally { UIPrefabs.UseKit = true; }

        // Khung cửa sổ: tiêu đề / nút X là bản lồng của mẫu Panel / Text / Button
        n += WithKitLinks(() => Save("Window", overwrite, () => UIKit.BuildWindowFrame("Window", null).gameObject));

        string root = Res + UIPrefabs.RootPath + ".prefab";
        if (overwrite || !File.Exists(root))
        {
            var go = UIRoot.BuildByCode();
            PrefabUtility.SaveAsPrefabAsset(go, root);
            UnityEngine.Object.DestroyImmediate(go);
            n++;
        }
        AssetDatabase.SaveAssets();
        UIPrefabs.ClearCache();
        return n;
    }

    private static int Save(string kit, bool overwrite, Func<GameObject> build)
    {
        string path = Res + UIPrefabs.KitFolder + kit + ".prefab";
        if (!overwrite && File.Exists(path)) return 0;
        var go = build();
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        UIPrefabs.ClearCache();
        return 1;
    }

    private static void MakeWhiteSprite(string path)
    {
        var tex = new Texture2D(4, 4);
        tex.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.SaveAndReimport();
    }

    // ==========================================
    // CỬA SỔ
    // ==========================================

    public static int ExportWindows(bool overwrite)
    {
        int n = 0;
        foreach (var t in WindowTypes()) if (ExportWindow(t, overwrite)) n++;
        AssetDatabase.SaveAssets();
        UIPrefabs.ClearCache();
        return n;
    }

    private static bool ExportWindow(Type t, bool overwrite)
    {
        string path = Res + UIPrefabs.WindowFolder + t.Name + ".prefab";
        if (!overwrite && File.Exists(path)) return false;
        return WithKitLinks(() =>
        {
            var w = GameWindow.CreateByCode(t, null);
            PrefabUtility.SaveAsPrefabAsset(w.gameObject, path);
            UnityEngine.Object.DestroyImmediate(w.gameObject);
            return 1;
        }) == 1;
    }

    // ==========================================
    // HUD
    // ==========================================

    public static int ExportHud(bool overwrite)
    {
        Directory.CreateDirectory(Res + UIPrefabs.HudFolder);
        int n = 0;
        foreach (var t in HudTypes()) if (ExportHudPanel(t, overwrite)) n++;
        AssetDatabase.SaveAssets();
        UIPrefabs.ClearCache();
        return n;
    }

    private static bool ExportHudPanel(Type t, bool overwrite)
    {
        string path = Res + UIPrefabs.HudFolder + t.Name + ".prefab";
        if (!overwrite && File.Exists(path)) return false;
        return WithKitLinks(() =>
        {
            var p = HudPanel.CreateByCode(t, null);
            PrefabUtility.SaveAsPrefabAsset(p.gameObject, path);
            UnityEngine.Object.DestroyImmediate(p.gameObject);
            return 1;
        }) == 1;
    }

    private static Type[] HudTypes() => TypeCache.GetTypesDerivedFrom<HudPanel>().Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToArray();

    /// <summary>Lớp cửa sổ xuất được (đã tách Bind()).</summary>
    private static Type[] WindowTypes() => TypeCache.GetTypesDerivedFrom<GameWindow>()
        .Where(t => !t.IsAbstract && GameWindow.SupportsPrefab(t)).OrderBy(t => t.Name).ToArray();

    /// <summary>Trong lúc chạy {fn}, phần tử tạo từ mẫu là BẢN LỒNG (giữ liên kết với prefab mẫu).</summary>
    private static int WithKitLinks(Func<int> fn)
    {
        UIPrefabs.ClearCache();
        UIPrefabs.InstantiateHook = (prefab, parent) =>
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        };
        try { return fn(); }
        finally { UIPrefabs.InstantiateHook = null; }
    }

    private static void Report(int n)
    {
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("UI", n > 0 ? $"Đã tạo {n} file trong Assets/Resources/UI." : "Không có gì mới (file đã có thì giữ nguyên).", "OK");
    }
}
