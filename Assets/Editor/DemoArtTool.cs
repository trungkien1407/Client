using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Script.Models;
using Assets.Script.UI.Kit;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NHẬP HÌNH DEMO NỘI BỘ (tài nguyên rip đã giải mã, thư mục TaiNguyen) vào Assets/_Demo/Resources — gitignore, KHÔNG commit:
///   1. Kho ảnh   07_VatPham_IconNho/imgN.png → _Demo/Resources/ImageBank/imgN.png    (icon vật phẩm / chiêu, khung hiệu ứng)
///   2. Hình quái 02_Quai/data_N/img_K.png + anim.json → _Demo/Resources/MobArt/N/ + MobAnim/MobAnim_N.asset
///   3. Skin UI   08_UI/... (khung cửa sổ, nút, ô...) → _Demo/Resources/UISkin/&lt;tên&gt;.png + viền 9-slice (UISkin dùng lúc chạy)
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
    public const string SkinDir = Root + "/UISkin";
    public const string NpcArtDir = Root + "/NpcArt";
    private const string DirKey = "Naruto.DemoArtDir";

    [MenuItem("Tools/Naruto/Demo art/1. Nhập kho ảnh + hình quái từ TaiNguyen", priority = 40)]
    public static void MenuImport()
    {
        string dir = SourceDir(true);
        if (dir == null) return;
        var (icons, mobs, ui, npcs) = ImportAll(dir);
        EditorUtility.DisplayDialog("Demo art", $"Kho ảnh: {icons} ảnh\nHình quái: {mobs} bộ\nHình NPC: {npcs} bộ\nSkin UI: {ui} hình\n→ {Root} (không commit)", "OK");
    }

    [MenuItem("Tools/Naruto/Demo art/2. Đổi thư mục TaiNguyen", priority = 41)]
    public static void MenuPick() => PickDir();

    public static void BatchImport()
    {
        var (icons, mobs, ui, npcs) = ImportAll(SourceDir(false));
        Debug.Log($"[DemoArt] kho ảnh {icons}, hình quái {mobs}, hình NPC {npcs}, skin UI {ui}");
    }

    public static (int icons, int mobs, int ui, int npcs) ImportAll(string src)
    {
        int icons = CopyBank(Path.Combine(src, "07_VatPham_IconNho"));
        var mobDirs = CopyMobArt(Path.Combine(src, "02_Quai"));
        var npcArts = CopyNpcArt(Path.Combine(src, "03_NPC"));
        var skins = CopyUiSkin(Path.Combine(src, "08_UI"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        int mobs = 0, npcs = 0;
        foreach (var (art, anim) in mobDirs) if (BuildMobAnim(art, anim)) mobs++;
        foreach (var art in npcArts) if (BuildNpcArt(art)) npcs++;
        foreach (var (path, border) in skins) SetupSkinSprite(path, border);
        AssetDatabase.SaveAssets();
        return (icons, mobs, skins.Count, npcs);
    }

    // ==================== SKIN UI ====================

    // Tên hình UISkin → ảnh trong 08_UI. Viền 9-slice: null = lấy từ _sprites.json cùng thư mục (game gốc ghi sẵn), có giá trị = đặt tay
    // (Vector4 = trái, dưới, phải, trên — điểm ảnh). KhungTrongGame/3 = bản 3× (vừa khung 1280x720 khi UISkin thu viền ×2).
    private static readonly (string key, string file, Vector4? border)[] SkinFiles =
    {
        (UISkin.Window, "KhungTrongGame/3/bagBG_2.png", null),                    // nền cửa sổ tím mận, hoa văn góc
        (UISkin.Header, "imggame/sprTabTitle1.png", new Vector4(40, 0, 40, 0)),   // thanh tiêu đề
        (UISkin.Close, "imggame/sprClosePopup.png", Vector4.zero),                // nút đóng (hình X)
        (UISkin.Button, "KhungTrongGame/3/sprCellQS0.png", null),                 // nút: kem bo góc (nhân màu theme)
        (UISkin.Input, "KhungTrongGame/3/bgdlPer0.png", null),                    // ô nhập chữ: nền tối
        (UISkin.List, "KhungTrongGame/3/popupbgInfo_0.png", null),                // khung danh sách cuộn
        (UISkin.Slot, "KhungTrongGame/3/sprCellQS0.png", null),                   // ô / hàng (nhân màu ô của theme)
        // Icon trạng thái (StatusIconsHud, fx_<mã StatusEffect>) — hình nhạt trên nền tối, game nhuộm màu hiệu ứng
        ("fx_1", "IconMenu/3.png", Vector4.zero),    // choáng: vòng xoáy
        ("fx_2", "IconMenu/5.png", Vector4.zero),    // chậm: tinh thể băng
        ("fx_3", "IconMenu/6.png", Vector4.zero),    // bỏng: lửa
        ("fx_4", "IconMenu/31.png", Vector4.zero),   // tăng sức mạnh: mũ giáp
        ("fx_5", "IconMenu/43.png", Vector4.zero),   // phân thân: người + vệt chuyển động
        // 12 nút menu HUD (HudMenuIcons, theo thứ tự GameHudView.MenuLabels) + nền tròn + icon Đồ sát
        ("menu_bg", "imggame/sprBgSkill0.png", Vector4.zero),          // nền tròn tối viền vàng
        ("menu_char", "imggame/sprIconChangeChar0.png", Vector4.zero),  // Nhân vật: đầu ninja
        ("menu_bag", "IconMenu/menuLeft1.png", Vector4.zero),           // Túi
        ("menu_skill", "IconMenu/menuLeft15.png", Vector4.zero),        // Kỹ năng: kiếm chéo
        ("menu_quest", "IconMenu/menuLeft0.png", Vector4.zero),         // Nhiệm vụ: bảng check
        ("menu_talk", "imggame/sprChat.png", Vector4.zero),             // Nói (F)
        ("menu_activity", "imggame/sprGiftIcon0.png", Vector4.zero),    // Hoạt động: quà
        ("menu_social", "imggame/sprPk1.png", Vector4.zero),            // Xã hội: 2 người
        ("menu_mail", "imggame/sprMail0.png", Vector4.zero),            // Thư
        ("menu_top", "imggame/sprRankIcon0.png", Vector4.zero),         // Xếp hạng: cúp hạng 1
        ("menu_guide", "IconMenu/menuLeft8.png", Vector4.zero),         // Cẩm nang: cuộn giấy
        ("menu_settings", "IconMenu/menuLeft5.png", Vector4.zero),      // Cài đặt: bánh răng
        ("menu_peace", "IconMenu/menuLeft11.png", Vector4.zero),        // Hoà bình: khiên khoá
        ("menu_pk", "IconMenu/menuLeft13.png", Vector4.zero),           // Đồ sát: mặt quỷ
    };

    private static List<(string path, Vector4 border)> CopyUiSkin(string uiDir)
    {
        var list = new List<(string, Vector4)>();
        if (!Directory.Exists(uiDir)) { Debug.LogWarning("[DemoArt] thiếu " + uiDir); return list; }
        Directory.CreateDirectory(SkinDir);
        foreach (var (key, file, border) in SkinFiles)
        {
            string from = Path.Combine(uiDir, file);
            if (!File.Exists(from)) { Debug.LogWarning("[DemoArt] thiếu hình skin " + from); continue; }
            string to = $"{SkinDir}/{key}.png";
            if (!File.Exists(to) || new FileInfo(to).Length != new FileInfo(from).Length) File.Copy(from, to, true);
            list.Add((to, border ?? BorderFromJson(from)));
        }
        return list;
    }

    /// <summary>Viền 9-slice của ảnh trong _sprites.json cùng thư mục (giải mã từ game gốc): "border": [trái, dưới, phải, trên].</summary>
    private static Vector4 BorderFromJson(string png)
    {
        string json = Path.Combine(Path.GetDirectoryName(png) ?? "", "_sprites.json");
        if (!File.Exists(json)) return Vector4.zero;
        var b = JObject.Parse(File.ReadAllText(json))[Path.GetFileNameWithoutExtension(png)]?["border"] as JArray;
        return b != null && b.Count == 4 ? new Vector4((float)b[0], (float)b[1], (float)b[2], (float)b[3]) : Vector4.zero;
    }

    private static void SetupSkinSprite(string path, Vector4 border)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.spriteBorder = border;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
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

    // ==================== HÌNH NPC ====================

    /// <summary>
    /// 03_NPC/npcN (hoặc npcNa)/img0.png (thân) + img1.png (chân) → _Demo/Resources/NpcArt/N/. Số bộ hình = N (bỏ chữ "a").
    /// Không có animation: mỗi NPC là 2 ảnh tĩnh. npc37 chỉ có 1 ảnh to (cây / đồ vật) → legs trống.
    /// </summary>
    private static List<int> CopyNpcArt(string dir)
    {
        var list = new List<int>();
        if (!Directory.Exists(dir)) { Debug.LogWarning("[DemoArt] thiếu " + dir); return list; }
        foreach (var d in Directory.GetDirectories(dir, "npc*"))
        {
            string num = new string(Path.GetFileName(d).Substring(3).TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(num, out int art)) continue;
            if (!File.Exists(Path.Combine(d, "img0.png"))) continue;
            string dst = $"{NpcArtDir}/{art}";
            Directory.CreateDirectory(dst);
            foreach (var name in new[] { "img0.png", "img1.png" })
            {
                string f = Path.Combine(d, name), to = Path.Combine(dst, name);
                if (File.Exists(f) && (!File.Exists(to) || new FileInfo(to).Length != new FileInfo(f).Length)) File.Copy(f, to, true);
            }
            list.Add(art);
        }
        return list;
    }

    /// <summary>Ảnh img0 / img1 → NpcArtSO "NpcArt_N" (cùng thư mục Resources/NpcArt để tải bằng "NpcArt/NpcArt_N").</summary>
    private static bool BuildNpcArt(int art)
    {
        var body = AssetDatabase.LoadAssetAtPath<Sprite>($"{NpcArtDir}/{art}/img0.png");
        if (body == null) return false;
        string path = $"{NpcArtDir}/{NpcArtSO.Address(art)}.asset";
        var so = AssetDatabase.LoadAssetAtPath<NpcArtSO>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<NpcArtSO>();
        so.body = body;
        so.legs = AssetDatabase.LoadAssetAtPath<Sprite>($"{NpcArtDir}/{art}/img1.png");
        so.bodyOffsetPx = so.legs != null ? FitBodyOnLegs($"{NpcArtDir}/{art}/img0.png", $"{NpcArtDir}/{art}/img1.png") : Vector2.zero;
        if (isNew) AssetDatabase.CreateAsset(so, path);
        else EditorUtility.SetDirty(so);
        return true;
    }

    /// <summary>
    /// Ghép thân lên chân theo ĐIỂM ẢNH (game gốc không ghi vị trí):
    ///   ngang — so "cột có màu" của dải {band} hàng dưới cùng của thân với dải {band} hàng trên cùng của chân, chọn độ dời
    ///           để chân được thân phủ nhiều nhất (hoà thì lấy trùng khít hơn, rồi dời ít hơn). Không lấy giữa min..max vì
    ///           đồ cầm tay (búa Thợ Rèn) hay thõng xuống thấp nhất → đo nhầm vào đồ;
    ///   dọc — mép có màu của thân chồng xuống mép có màu của chân {overlap} px.
    /// Trả vị trí giữa-đáy ảnh thân so với giữa-đáy ảnh chân (px, x sang phải, y lên trên).
    /// </summary>
    private static Vector2 FitBodyOnLegs(string bodyPath, string legsPath, int overlap = 4, int band = 12)
    {
        var b = ReadPng(bodyPath); var l = ReadPng(legsPath);
        if (b == null || l == null) return Vector2.zero;
        try
        {
            // Texture2D: hàng 0 = ĐÁY ảnh
            int bBottom = FirstOpaqueRow(b, fromBottom: true), lTop = FirstOpaqueRow(l, fromBottom: false);
            if (bBottom < 0 || lTop < 0) return new Vector2(0, l.height - overlap);
            var bodyCols = OpaqueColumns(b, bBottom, bBottom + band - 1, b.width / 2f);
            var legCols = OpaqueColumns(l, lTop - band + 1, lTop, l.width / 2f);
            int bestDx = 0; float bestCover = -1f, bestIou = -1f;
            for (int dx = -80; dx <= 80; dx++)
            {
                int inter = 0;
                foreach (int x in legCols) if (bodyCols.Contains(x - dx)) inter++;
                float cover = legCols.Count > 0 ? (float)inter / legCols.Count : 0f;
                float iou = (float)inter / Mathf.Max(1, bodyCols.Count + legCols.Count - inter);
                bool better = cover > bestCover + 1e-4f
                              || (Mathf.Abs(cover - bestCover) <= 1e-4f && (iou > bestIou + 1e-4f
                                  || (Mathf.Abs(iou - bestIou) <= 1e-4f && Mathf.Abs(dx) < Mathf.Abs(bestDx))));
                if (better) { bestCover = cover; bestIou = iou; bestDx = dx; }
            }
            float dy = lTop + 1 - overlap - bBottom;          // đáy có màu của thân nằm dưới đỉnh có màu của chân {overlap} px
            return new Vector2(bestDx, Mathf.Round(dy));
        }
        finally { UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(l); }
    }

    private static Texture2D ReadPng(string assetPath)
    {
        if (!File.Exists(assetPath)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return t.LoadImage(File.ReadAllBytes(assetPath)) ? t : null;
    }

    /// <summary>Hàng có điểm ảnh đục đầu tiên tính từ đáy (fromBottom) hoặc từ đỉnh; -1 = ảnh trống.</summary>
    private static int FirstOpaqueRow(Texture2D t, bool fromBottom)
    {
        var px = t.GetPixels32();
        for (int i = 0; i < t.height; i++)
        {
            int y = fromBottom ? i : t.height - 1 - i;
            for (int x = 0; x < t.width; x++) if (px[y * t.width + x].a > 128) return y;
        }
        return -1;
    }

    /// <summary>Các cột có điểm ảnh đục trong hàng y0..y1, tính theo toạ độ lấy giữa ảnh làm 0 (làm tròn).</summary>
    private static HashSet<int> OpaqueColumns(Texture2D t, int y0, int y1, float center)
    {
        var px = t.GetPixels32();
        var set = new HashSet<int>();
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(t.height - 1, y1); y++)
            for (int x = 0; x < t.width; x++)
                if (px[y * t.width + x].a > 128) set.Add(Mathf.RoundToInt(x - center));
        return set;
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
        bool bank = p.Contains("/ImageBank/"), mob = p.Contains("/MobArt/"), npc = p.Contains("/NpcArt/");
        if (!bank && !mob && !npc) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ArtUnits.PixelsPerUnit;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)(mob ? SpriteAlignment.TopLeft : npc ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);   // NPC: chân đứng ở gốc
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }
}
