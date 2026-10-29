using System.Collections.Generic;
using System.IO;
using System.Text;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VocVach.SpineTools
{
    /// <summary>
    /// CHỤP BẢNG ANIM SPINE: mỗi bộ xương (SkeletonDataAsset) → thư mục Builds/AnimSheets/{tên}/ gồm
    ///   {anim}.png      8 khung hình đều nhau của 1 anim (xem anim là chém, ném, buff... và khung nào trúng đòn)
    ///   _ALL.png        mọi anim xếp thành hàng, cùng thứ tự với _ALL.txt (tên + độ dài)
    /// Menu: Tools/VocVach/Spine/Chụp bảng anim (tất cả | đang chọn). Batch: -executeMethod VocVach.SpineTools.AnimSheet.RunAll
    /// Skin: "Normal" nếu có, không thì skin mặc định.
    /// </summary>
    public static class AnimSheet
    {
        public const int Frames = 8, Cell = 320, RowCell = 160;
        public static string OutRoot => Path.GetFullPath("Builds/AnimSheets");

        [MenuItem("Tools/VocVach/Spine/Chụp bảng anim (tất cả)")]
        public static void RunAll()
        {
            var list = new List<string>();
            // Quét mọi ScriptableObject trong Assets rồi tự lọc kiểu SkeletonDataAsset (không phụ thuộc bộ lọc "t:" theo tên kiểu)
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(AssetDatabase.GetAssetPath(a));
            }
            Debug.Log($"[AnimSheet] Tìm thấy {list.Count} bộ Spine");
            Capture(list);
        }

        [MenuItem("Tools/VocVach/Spine/Chụp bảng anim (đang chọn)")]
        public static void RunSelected()
        {
            var list = new List<string>();
            foreach (var o in Selection.objects) if (o is SkeletonDataAsset s) list.Add(AssetDatabase.GetAssetPath(s));
            if (list.Count == 0) { Debug.LogWarning("[AnimSheet] Chọn ít nhất 1 SkeletonDataAsset trong Project."); return; }
            Capture(list);
        }

        /// <summary>Chụp các bộ xương theo ĐƯỜNG DẪN — NewScene dọn tài nguyên không dùng, giữ tham chiếu thì bị huỷ trước khi chụp.</summary>
        public static void Capture(List<string> assetPaths)
        {
            string prevScene = EditorSceneManager.GetActiveScene().path;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rt = new RenderTexture(Cell, Cell, 24);
            Camera cam = null;
            try
            {
                cam = new GameObject("Cam").AddComponent<Camera>();
                cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.35f, 0.42f, 0.38f);
                cam.targetTexture = rt;
                foreach (var path in assetPaths)
                {
                    var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                    if (data != null) CaptureOne(data, cam, rt);
                }
                Debug.Log("[AnimSheet] Xong: " + OutRoot);
            }
            finally
            {
                RenderTexture.active = null;
                if (cam != null) cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                if (!string.IsNullOrEmpty(prevScene)) EditorSceneManager.OpenScene(prevScene);
            }
        }

        private static void CaptureOne(SkeletonDataAsset data, Camera cam, RenderTexture rt)
        {
            var sa = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
            sa.Initialize(true);   // ngoài Play mode (batch) không tự khởi tạo
            if (sa.Skeleton == null) { Debug.LogWarning("[AnimSheet] không đọc được " + data.name); Object.DestroyImmediate(sa.gameObject); return; }
            if (sa.Skeleton.Data.FindSkin("Normal") != null) { sa.initialSkinName = "Normal"; sa.Initialize(true); }
            string name = data.name.Replace("_SkeletonData", "");
            string dir = Path.Combine(OutRoot, name);
            Directory.CreateDirectory(dir);
            var mr = sa.GetComponent<MeshRenderer>();
            var rows = new List<Texture2D>();
            var index = new StringBuilder();

            foreach (var anim in sa.Skeleton.Data.Animations)
            {
                // Lượt 1 đo khung bao cả anim (đặt camera vừa khít), lượt 2 chụp
                var b = new Bounds(sa.transform.position + Vector3.up, Vector3.one);
                var shots = new List<Texture2D>();
                for (int pass = 0; pass < 2; pass++)
                {
                    sa.Skeleton.SetToSetupPose();
                    sa.AnimationState.ClearTracks();
                    sa.AnimationState.SetAnimation(0, anim, false);
                    float step = anim.Duration / Frames;
                    for (int f = 0; f < Frames; f++)
                    {
                        sa.Update(f == 0 ? 0f : step);
                        sa.LateUpdate();
                        if (pass == 0) { b.Encapsulate(mr.bounds); continue; }
                        cam.orthographicSize = Mathf.Max(b.size.x, b.size.y) * 0.55f;
                        cam.transform.position = new Vector3(b.center.x, b.center.y, -10);
                        cam.Render();
                        RenderTexture.active = rt;
                        var t = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                        t.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); t.Apply();
                        shots.Add(t);
                    }
                }
                var sheet = new Texture2D(Cell * Frames, Cell, TextureFormat.RGB24, false);
                var row = new Texture2D(RowCell * Frames, RowCell, TextureFormat.RGB24, false);
                for (int i = 0; i < shots.Count; i++)
                {
                    sheet.SetPixels(i * Cell, 0, Cell, Cell, shots[i].GetPixels());
                    row.SetPixels(i * RowCell, 0, RowCell, RowCell, Half(shots[i]));
                    Object.DestroyImmediate(shots[i]);
                }
                sheet.Apply(); row.Apply();
                File.WriteAllBytes(Path.Combine(dir, anim.Name + ".png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                rows.Add(row);
                index.AppendLine($"{rows.Count,2}. {anim.Name}  {anim.Duration:0.00}s");
            }

            // _ALL.png: hàng 1 ở TRÊN cùng (Texture2D đếm y từ dưới lên)
            var all = new Texture2D(RowCell * Frames, RowCell * Mathf.Max(1, rows.Count), TextureFormat.RGB24, false);
            for (int i = 0; i < rows.Count; i++)
            {
                all.SetPixels(0, (rows.Count - 1 - i) * RowCell, RowCell * Frames, RowCell, rows[i].GetPixels());
                Object.DestroyImmediate(rows[i]);
            }
            all.Apply();
            File.WriteAllBytes(Path.Combine(dir, "_ALL.png"), all.EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, "_ALL.txt"), index.ToString());
            Object.DestroyImmediate(all);
            Object.DestroyImmediate(sa.gameObject);
            Debug.Log($"[AnimSheet] {name}: {rows.Count} anim → {dir}");
        }

        /// <summary>
        /// CHỤP SKIN: mỗi bộ xương → Builds/AnimSheets/{tên}/_SKINS.png — mỗi hàng 1 skin (bỏ "default" và skin vũ khí WEAPON_*),
        /// 8 khung của anim Idle (hoặc anim đầu tiên). Xem skin biến thân / trang phục trông thế nào. Batch: -executeMethod VocVach.SpineTools.AnimSheet.RunSkins
        /// </summary>
        [MenuItem("Tools/VocVach/Spine/Chụp bảng skin (tất cả)")]
        public static void RunSkins()
        {
            string prevScene = EditorSceneManager.GetActiveScene().path;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) paths.Add(AssetDatabase.GetAssetPath(a));
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rt = new RenderTexture(Cell, Cell, 24);
            Camera cam = null;
            try
            {
                cam = new GameObject("Cam").AddComponent<Camera>();
                cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.35f, 0.42f, 0.38f);
                cam.targetTexture = rt;
                foreach (var path in paths)
                {
                    var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                    if (data != null) CaptureSkins(data, cam, rt);
                }
            }
            finally
            {
                RenderTexture.active = null;
                if (cam != null) cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                if (!string.IsNullOrEmpty(prevScene)) EditorSceneManager.OpenScene(prevScene);
            }
        }

        private static void CaptureSkins(SkeletonDataAsset data, Camera cam, RenderTexture rt)
        {
            var sa = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
            sa.Initialize(true);
            if (sa.Skeleton == null) { Object.DestroyImmediate(sa.gameObject); return; }
            var skins = new List<string>();
            foreach (var s in sa.Skeleton.Data.Skins) if (s.Name != "default" && !s.Name.StartsWith("WEAPON")) skins.Add(s.Name);
            var anim = sa.Skeleton.Data.FindAnimation("Idle") ?? (sa.Skeleton.Data.Animations.Count > 0 ? sa.Skeleton.Data.Animations.Items[0] : null);
            if (skins.Count == 0 || anim == null) { Object.DestroyImmediate(sa.gameObject); return; }
            var mr = sa.GetComponent<MeshRenderer>();
            var rows = new List<Texture2D>();
            var index = new StringBuilder();
            foreach (var skin in skins)
            {
                sa.initialSkinName = skin;
                sa.Initialize(true);
                var b = new Bounds(sa.transform.position + Vector3.up, Vector3.one);
                var row = new Texture2D(RowCell * Frames, RowCell, TextureFormat.RGB24, false);
                for (int pass = 0; pass < 2; pass++)
                {
                    sa.Skeleton.SetToSetupPose();
                    sa.AnimationState.ClearTracks();
                    sa.AnimationState.SetAnimation(0, anim, true);
                    float step = anim.Duration / Frames;
                    for (int f = 0; f < Frames; f++)
                    {
                        sa.Update(f == 0 ? 0f : step);
                        sa.LateUpdate();
                        if (pass == 0) { b.Encapsulate(mr.bounds); continue; }
                        cam.orthographicSize = Mathf.Max(b.size.x, b.size.y) * 0.55f;
                        cam.transform.position = new Vector3(b.center.x, b.center.y, -10);
                        cam.Render();
                        RenderTexture.active = rt;
                        var t = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                        t.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); t.Apply();
                        row.SetPixels(f * RowCell, 0, RowCell, RowCell, Half(t));
                        Object.DestroyImmediate(t);
                    }
                }
                row.Apply();
                rows.Add(row);
                index.AppendLine($"{rows.Count,2}. {skin}");
            }
            string name = data.name.Replace("_SkeletonData", "");
            string dir = Path.Combine(OutRoot, name);
            Directory.CreateDirectory(dir);
            var all = new Texture2D(RowCell * Frames, RowCell * rows.Count, TextureFormat.RGB24, false);
            for (int i = 0; i < rows.Count; i++)
            {
                all.SetPixels(0, (rows.Count - 1 - i) * RowCell, RowCell * Frames, RowCell, rows[i].GetPixels());
                Object.DestroyImmediate(rows[i]);
            }
            all.Apply();
            File.WriteAllBytes(Path.Combine(dir, "_SKINS.png"), all.EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, "_SKINS.txt"), index.ToString());
            Object.DestroyImmediate(all);
            Object.DestroyImmediate(sa.gameObject);
            Debug.Log($"[AnimSheet] {name}: {skins.Count} skin → {dir}/_SKINS.png");
        }

        /// <summary>Thu nhỏ 1/2 (trung bình 2×2 điểm ảnh).</summary>
        private static Color[] Half(Texture2D t)
        {
            var src = t.GetPixels();
            var dst = new Color[RowCell * RowCell];
            for (int y = 0; y < RowCell; y++)
                for (int x = 0; x < RowCell; x++)
                {
                    int i = y * 2 * Cell + x * 2;
                    dst[y * RowCell + x] = (src[i] + src[i + 1] + src[i + Cell] + src[i + Cell + 1]) * 0.25f;
                }
            return dst;
        }
    }
}
