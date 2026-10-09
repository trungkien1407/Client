using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Script.Models;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NHẬP HÌNH DEMO NỘI BỘ (tài nguyên rip đã giải mã, thư mục TaiNguyen) vào Assets/_Demo/Resources — gitignore, KHÔNG commit:
///   1. Kho ảnh   07_VatPham_IconNho/imgN.png → _Demo/Resources/ImageBank/imgN.png    (icon vật phẩm / chiêu, khung hiệu ứng)
///   2. Hình quái 02_Quai/data_N/img_K.png + anim.json → _Demo/Resources/MobArt/N/ + MobAnim/MobAnim_N.asset
/// Code tải theo tên qua AssetSource: Addressables trước (hình chính thức), không có mới lấy _Demo → thay hình thật
/// = đưa ảnh cùng tên vào Addressables (address "imgN", "MobAnim_N"), không sửa code.
/// Không mở Editor: -executeMethod DemoArtTool.BatchImport -demoArt &lt;thư mục TaiNguyen&gt;
/// </summary>
public static class DemoArtTool
{
    public const string Root = "Assets/_Demo/Resources";
    public const string BankDir = Root + "/ImageBank";
    public const string MobArtDir = Root + "/MobArt";
    public const string MobAnimDir = Root + "/MobAnim";
    private const string DirKey = "Naruto.DemoArtDir";

    [MenuItem("Tools/Naruto/Demo art/1. Nhập kho ảnh + hình quái từ TaiNguyen", priority = 40)]
    public static void MenuImport()
    {
        string dir = SourceDir(true);
        if (dir == null) return;
        var (icons, mobs) = ImportAll(dir);
        EditorUtility.DisplayDialog("Demo art", $"Kho ảnh: {icons} ảnh\nHình quái: {mobs} bộ\n→ {Root} (không commit)", "OK");
    }

    [MenuItem("Tools/Naruto/Demo art/2. Đổi thư mục TaiNguyen", priority = 41)]
    public static void MenuPick() => PickDir();

    public static void BatchImport()
    {
        var (icons, mobs) = ImportAll(SourceDir(false));
        Debug.Log($"[DemoArt] kho ảnh {icons}, hình quái {mobs}");
    }

    public static (int icons, int mobs) ImportAll(string src)
    {
        int icons = CopyBank(Path.Combine(src, "07_VatPham_IconNho"));
        var mobDirs = CopyMobArt(Path.Combine(src, "02_Quai"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        int mobs = 0;
        foreach (var (art, anim) in mobDirs) if (BuildMobAnim(art, anim)) mobs++;
        AssetDatabase.SaveAssets();
        return (icons, mobs);
    }

    // ==================== KHO ẢNH ====================

    private static readonly Regex BankName = new Regex(@"^img\d+\.png$", RegexOptions.IgnoreCase);

    private static int CopyBank(string dir)
    {
        if (!Directory.Exists(dir)) { Debug.LogWarning("[DemoArt] thiếu " + dir); return 0; }
        Directory.CreateDirectory(BankDir);
        int n = 0;
        foreach (var f in Directory.GetFiles(dir, "img*.png"))
        {
            if (!BankName.IsMatch(Path.GetFileName(f))) continue;
            string dst = Path.Combine(BankDir, Path.GetFileName(f));
            if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(f).Length) File.Copy(f, dst, true);
            n++;
        }
        return n;
    }

    // ==================== HÌNH QUÁI ====================

    private static List<(int art, JObject anim)> CopyMobArt(string dir)
    {
        var list = new List<(int, JObject)>();
        if (!Directory.Exists(dir)) { Debug.LogWarning("[DemoArt] thiếu " + dir); return list; }
        foreach (var d in Directory.GetDirectories(dir, "data_*"))
        {
            if (!int.TryParse(Path.GetFileName(d).Substring(5), out int art)) continue;
            string json = Path.Combine(d, "anim.json");
            if (!File.Exists(json)) continue;
            string dst = $"{MobArtDir}/{art}";
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(d, "img_*.png"))
            {
                string to = Path.Combine(dst, Path.GetFileName(f));
                if (!File.Exists(to) || new FileInfo(to).Length != new FileInfo(f).Length) File.Copy(f, to, true);
            }
            list.Add((art, JObject.Parse(File.ReadAllText(json))));
        }
        return list;
    }

    /// <summary>anim.json (decode_monsterdata.py) → MobAnimSO: ảnh mảnh img_K + 5 nhóm khung + khung va chạm.</summary>
    private static bool BuildMobAnim(int art, JObject anim)
    {
        int count = (int?)anim["images"] ?? 0;
        var images = new Sprite[count];
        for (int k = 0; k < count; k++) images[k] = AssetDatabase.LoadAssetAtPath<Sprite>($"{MobArtDir}/{art}/img_{k}.png");
        if (count == 0 || images.All(s => s == null)) return false;

        Directory.CreateDirectory(MobAnimDir);
        string path = $"{MobAnimDir}/{MobAnimSO.Address(art)}.asset";
        var so = AssetDatabase.LoadAssetAtPath<MobAnimSO>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<MobAnimSO>();
        so.hitW = (int?)anim["width"] ?? 24;
        so.hitH = (int?)anim["height"] ?? 32;
        so.images = images;
        var groups = (JArray)anim["groups"] ?? new JArray();
        so.states = new PartClip[MobAnimSO.StateCount];
        for (int g = 0; g < MobAnimSO.StateCount; g++)
        {
            var frames = g < groups.Count ? (JArray)groups[g]["frames"] : null;
            so.states[g] = new PartClip
            {
                frames = frames == null ? Array.Empty<PartFrame>() : frames.Select(f => new PartFrame
                {
                    parts = ((JArray)f["parts"]).Select(p => new FramePart((int)p["img"], (int)p["dx"], (int)p["dy"])).ToArray(),
                    fx = (short)((int?)f["fx"]?[0] ?? -1),
                    fxX = (short)((int?)f["fx"]?[1] ?? 0),
                    fxY = (short)((int?)f["fx"]?[2] ?? 0),
                }).ToArray()
            };
        }
        if (isNew) AssetDatabase.CreateAsset(so, path);
        else EditorUtility.SetDirty(so);
        return true;
    }

    // ==================== THƯ MỤC NGUỒN ====================

    /// <summary>Thư mục TaiNguyen: tham số -demoArt, rồi lựa chọn đã lưu, rồi đoán D:/Download/AssetRipper_win_x64/TaiNguyen.</summary>
    private static string SourceDir(bool interactive)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-demoArt");
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        string dir = EditorPrefs.GetString(DirKey, "");
        if (Directory.Exists(dir)) return dir;
        const string guess = "D:/Download/AssetRipper_win_x64/TaiNguyen";
        if (Directory.Exists(guess)) { EditorPrefs.SetString(DirKey, guess); return guess; }
        return interactive ? PickDir() : throw new DirectoryNotFoundException("Không thấy TaiNguyen — dùng -demoArt <thư mục>");
    }

    private static string PickDir()
    {
        string dir = EditorUtility.OpenFolderPanel("Chọn thư mục TaiNguyen (tài nguyên đã giải mã)", EditorPrefs.GetString(DirKey, ""), "");
        if (string.IsNullOrEmpty(dir)) return null;
        EditorPrefs.SetString(DirKey, dir);
        return dir;
    }
}

/// <summary>
/// Cài đặt nhập ảnh trong Assets/_Demo (và thư mục hình chính thức cùng quy ước nếu đặt tên Art/ImageBank, Art/MobArt):
///   kho ảnh: Sprite, 96 px / đơn vị, pivot giữa · mảnh quái: Sprite, 96 px / đơn vị, pivot góc trên-trái.
/// </summary>
public class DemoArtPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool bank = p.Contains("/ImageBank/"), mob = p.Contains("/MobArt/");
        if (!bank && !mob) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ArtUnits.PixelsPerUnit;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)(mob ? SpriteAlignment.TopLeft : SpriteAlignment.Center);
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }
}
