using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Script.Database;
using Assets.Script.Models;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VocVach.StackFit;

/// <summary>
/// NGUỒN DỮ LIỆU "NPC (thân + chân)" cho cửa sổ kéo thả của package com.vocvach.sprite-stack-fit (Tools → VocVach → Ghép ảnh thân + chân).
///   · Danh sách = mọi NpcArtSO trong Assets/_Demo/Resources/NpcArt (kho hình đã nhập bằng Demo art → 1).
///   · Số đang dùng = NpcArtSO.bodyOffsetPx (mặc định = số hiện tại); ✎ = bộ có trong npc_fit_overrides.json.
///   · Lưu = ghi npc_fit_overrides.json (trong git, LÂU DÀI) + sửa luôn NpcArtSO (bấm Play thấy ngay).
///   · Về số tự động = xoá dòng khỏi JSON + tính lại bằng DemoArtTool.AutoFitNpc.
///   · Tên NPC cạnh số bộ: đọc data/art_demo.json của repo server (npcs: templateId → [art, scale]) + tên trong NpcDatabase — có thì hiện.
/// </summary>
public class NpcStackFitProvider : StackFitProvider
{
    public override string Title => "NPC (thân + chân)";

    public override List<StackFitItem> Load()
    {
        NpcFitOverrides.Reload();
        var names = NpcNamesByArt();
        var list = new List<StackFitItem>();
        if (!AssetDatabase.IsValidFolder(DemoArtTool.NpcArtDir))
        {
            Debug.LogWarning($"[NpcFit] Chưa có kho hình NPC ({DemoArtTool.NpcArtDir}) — chạy Tools → Naruto → Demo art → 1 trước.");
            return list;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:NpcArtSO", new[] { DemoArtTool.NpcArtDir }))
        {
            var so = AssetDatabase.LoadAssetAtPath<NpcArtSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (so == null || so.body == null || !int.TryParse(so.name.Replace("NpcArt_", ""), out int art)) continue;
            names.TryGetValue(art, out var who);
            list.Add(new StackFitItem
            {
                Id = art.ToString(),
                Label = $"Bộ {art}" + (string.IsNullOrEmpty(who) ? "" : $" — {who}") + (so.legs == null ? " (1 ảnh)" : ""),
                Top = so.body,
                Bottom = so.legs,
                Offset = so.bodyOffsetPx,
                Manual = NpcFitOverrides.TryGet(art, out _),
                Tag = so,
            });
        }
        return list.OrderBy(i => int.Parse(i.Id)).ToList();
    }

    public override Vector2? AutoOffset(StackFitItem item) => item.Bottom == null ? (Vector2?)null : DemoArtTool.AutoFitNpc(int.Parse(item.Id));

    public override void Save(StackFitItem item, Vector2 offset)
    {
        int art = int.Parse(item.Id);
        NpcFitOverrides.Set(art, offset);
        Apply(item, offset);
        item.Manual = true;
        Debug.Log($"[NpcFit] Bộ {art}: lưu chỉnh tay ({offset.x:0}, {offset.y:0}) → {NpcFitOverrides.FilePath}");
    }

    public override void ResetToAuto(StackFitItem item)
    {
        int art = int.Parse(item.Id);
        NpcFitOverrides.Remove(art);
        Apply(item, AutoOffset(item) ?? Vector2.zero);
        item.Manual = false;
        Debug.Log($"[NpcFit] Bộ {art}: về số tự động ({item.Offset.x:0}, {item.Offset.y:0})");
    }

    /// <summary>Sửa asset đang dùng để Play thấy ngay (asset nằm trong _Demo — không vào git; JSON mới là bản lâu dài).</summary>
    private static void Apply(StackFitItem item, Vector2 offset)
    {
        item.Offset = offset;
        if (item.Tag is NpcArtSO so)
        {
            so.bodyOffsetPx = offset;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssetIfDirty(so);
        }
    }

    /// <summary>
    /// Tự kiểm (dòng lệnh, Editor phải đóng): Unity -batchmode -quit -projectPath ... -executeMethod NpcStackFitProvider.SelfTest
    /// Đọc danh sách → lưu thử 1 bộ lệch +1 px → kiểm JSON + asset → trả lại số cũ (JSON giữ nguyên như trước khi chạy).
    /// </summary>
    public static void SelfTest()
    {
        var p = new NpcStackFitProvider();
        var items = p.Load();
        int fail = 0;
        void Check(string what, bool ok) { Debug.Log($"[NpcFitTest] {(ok ? "PASS" : "FAIL")} {what}"); if (!ok) fail++; }
        Check($"Đọc {items.Count} bộ hình NPC", items.Count > 0);
        Check("Bộ 9 / 12 / 23 đánh dấu chỉnh tay (đọc từ JSON)", items.Where(i => i.Id == "9" || i.Id == "12" || i.Id == "23").All(i => i.Manual));
        var it = items.FirstOrDefault(i => i.Bottom != null && !i.Manual);
        if (it != null)
        {
            var before = it.Offset;
            Check($"Bộ {it.Id}: có số tự động để so", p.AutoOffset(it).HasValue);
            p.Save(it, before + new Vector2(1, 0));
            NpcFitOverrides.Reload();
            Check($"Bộ {it.Id}: lưu → có trong JSON + asset đổi", NpcFitOverrides.TryGet(int.Parse(it.Id), out var v) && v == before + new Vector2(1, 0)
                && ((NpcArtSO)it.Tag).bodyOffsetPx == before + new Vector2(1, 0));
            p.ResetToAuto(it);
            Check($"Bộ {it.Id}: về số tự động → bỏ khỏi JSON, asset = số tự động", !NpcFitOverrides.TryGet(int.Parse(it.Id), out _) && !it.Manual);
            if (((NpcArtSO)it.Tag).bodyOffsetPx != before) Apply(it, before);   // trả asset về như cũ
        }
        Debug.Log($"[NpcFitTest] KẾT QUẢ: {(fail == 0 ? "OK" : fail + " FAIL")}");
        if (Application.isBatchMode) EditorApplication.Exit(fail);
    }

    /// <summary>art → "Hokage, Thợ Rèn…" (nhiều NPC có thể dùng chung 1 bộ). Không thấy file server → rỗng.</summary>
    private static Dictionary<int, string> NpcNamesByArt()
    {
        var map = new Dictionary<int, string>();
        try
        {
            string mapsDir = EditorPrefs.GetString("Naruto.MapDataDir", "");   // thư mục data/maps đã chọn ở Tools → Naruto → Map
            if (string.IsNullOrEmpty(mapsDir)) mapsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Server/Server/data/maps"));
            string file = Path.Combine(Path.GetDirectoryName(mapsDir.TrimEnd('/', '\\')) ?? "", "art_demo.json");
            if (!File.Exists(file)) return map;
            var npcs = (JObject)JObject.Parse(File.ReadAllText(file))["npcs"];
            if (npcs == null) return map;
            var db = AssetDatabase.FindAssets("t:NpcDatabaseSO").Select(g => AssetDatabase.LoadAssetAtPath<NpcDatabaseSO>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x != null);
            foreach (var kv in npcs)
            {
                if (!int.TryParse(kv.Key, out int tpl) || !(kv.Value is JArray a) || a.Count == 0) continue;
                int art = (int)a[0];
                string name = db != null ? db.GetNpcConfig(tpl)?.defaultName : null;
                if (string.IsNullOrEmpty(name)) name = "NPC " + tpl;
                map[art] = map.TryGetValue(art, out var s) ? s + ", " + name : name;
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[NpcFit] Không đọc được tên NPC: " + e.Message); }
        return map;
    }
}
