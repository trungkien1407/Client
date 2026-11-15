using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SỐ CHỈNH TAY vị trí thân NPC (2026-11-15) — lưu LÂU DÀI trong file JSON nằm trong git:
///   Assets/Editor/NpcFit/npc_fit_overrides.json
/// Mỗi dòng: số bộ hình (art) → (x, y) điểm ảnh, cùng nghĩa NpcArtSO.bodyOffsetPx (giữa-đáy ảnh thân so với giữa-đáy ảnh chân;
/// x sang phải, y lên trên). Bộ nào KHÔNG có trong file thì dùng số đo tự động (DemoArtTool.FitNpc).
///
/// Ai dùng: DemoArtTool khi nhập kho hình (menu Demo art → 1 / 6) và cửa sổ kéo thả (Tools → VocVach → Ghép ảnh thân + chân).
/// Máy khác: pull mã nguồn → liên kết kho tài nguyên → Demo art → 1 (nhập) là số chỉnh tay tự áp lại, không phải kéo lại.
/// (Trước đây là bảng viết cứng NpcFitOverride trong DemoArtTool.cs — 3 dòng cũ đã chuyển vào file này.)
/// </summary>
public static class NpcFitOverrides
{
    public const string FilePath = "Assets/Editor/NpcFit/npc_fit_overrides.json";

    public struct Entry { public Vector2 offset; public string note; }

    private static Dictionary<int, Entry> _cache;

    /// <summary>Đọc file (có bộ nhớ tạm; file đổi ngoài Unity thì gọi Reload).</summary>
    public static Dictionary<int, Entry> All
    {
        get
        {
            if (_cache == null) Reload();
            return _cache;
        }
    }

    public static void Reload()
    {
        _cache = new Dictionary<int, Entry>();
        if (!File.Exists(FilePath)) return;
        try
        {
            var root = JObject.Parse(File.ReadAllText(FilePath));
            foreach (var o in (JArray)root["overrides"] ?? new JArray())
            {
                int art = (int)o["art"];
                _cache[art] = new Entry
                {
                    offset = new Vector2((float)o["x"], (float)o["y"]),
                    note = (string)o["note"] ?? ""
                };
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NpcFit] Đọc {FilePath} lỗi: {e.Message} — tạm dùng số tự động cho mọi bộ.");
        }
    }

    public static bool TryGet(int art, out Vector2 offset)
    {
        if (All.TryGetValue(art, out var e)) { offset = e.offset; return true; }
        offset = default;
        return false;
    }

    /// <summary>Ghi / sửa 1 dòng rồi lưu file (giữ ghi chú cũ nếu không truyền ghi chú mới).</summary>
    public static void Set(int art, Vector2 offset, string note = null)
    {
        string keep = All.TryGetValue(art, out var old) ? old.note : "";
        All[art] = new Entry { offset = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y)), note = note ?? keep };
        Write();
    }

    public static void Remove(int art)
    {
        if (All.Remove(art)) Write();
    }

    /// <summary>Ghi file JSON, xếp theo số bộ hình (dễ xem diff trong git).</summary>
    private static void Write()
    {
        var arr = new JArray();
        var keys = new List<int>(_cache.Keys);
        keys.Sort();
        foreach (int art in keys)
        {
            var e = _cache[art];
            var o = new JObject { ["art"] = art, ["x"] = (int)e.offset.x, ["y"] = (int)e.offset.y };
            if (!string.IsNullOrEmpty(e.note)) o["note"] = e.note;
            arr.Add(o);
        }
        var root = new JObject
        {
            ["_ghi_chu"] = "Số chỉnh tay vị trí thân NPC (điểm ảnh, như NpcArtSO.bodyOffsetPx). Sửa bằng Tools → VocVach → Ghép ảnh thân + chân. " +
                           "Bộ không có ở đây = số tự động. Đọc bởi DemoArtTool khi nhập kho hình.",
            ["overrides"] = arr
        };
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, root.ToString(Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.ImportAsset(FilePath);
    }
}
