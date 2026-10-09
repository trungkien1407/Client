using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Script.Map
{
    /// <summary>
    /// ẢNH THU NHỎ CỦA MAP cho minimap / bản đồ lớn — vẽ thẳng từ tilemap của map vừa tải, 1 ô = 1 điểm ảnh
    /// (không cần camera phụ). Toạ độ ảnh trùng toạ độ world: ô (x, y) → điểm ảnh (x, y).
    /// </summary>
    public static class MapImage
    {
        public static Texture2D Texture { get; private set; }
        public static int Width { get; private set; }
        public static int Height { get; private set; }
        /// <summary>Tăng mỗi lần đổi map — UI so sánh để biết cần gắn ảnh mới.</summary>
        public static int Version { get; private set; }

        private static readonly Color Earth = new Color(0.36f, 0.27f, 0.20f);
        private static readonly Color Surface = new Color(0.55f, 0.78f, 0.38f);
        private static readonly Color Platform = new Color(0.90f, 0.65f, 0.30f);
        private static readonly Color Water = new Color(0.30f, 0.60f, 0.95f, 0.9f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook() => MapManager.OnMapLoaded += Build;

        private static void Build(GameObject map)
        {
            var ground = Find(map, "Ground");
            var plat = Find(map, "Platform");
            var water = Find(map, "Water");
            if (ground == null) return;

            ground.CompressBounds();
            var b = ground.cellBounds;
            int w = Mathf.Max(1, b.xMax);
            int h = Mathf.Max(12, b.yMax + 4);   // chừa khoảng trời phía trên để thấy bục / người đang nhảy

            if (Texture != null) Object.Destroy(Texture);
            Texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = new Vector3Int(x, y, 0);
                    Color col = Color.clear;
                    if (ground.HasTile(c)) col = ground.HasTile(c + Vector3Int.up) ? Earth : Surface;
                    else if (plat != null && plat.HasTile(c)) col = Platform;
                    else if (water != null && water.HasTile(c)) col = Water;
                    px[y * w + x] = col;
                }
            Texture.SetPixels32(px);
            Texture.Apply();
            Width = w; Height = h; Version++;
        }

        private static Tilemap Find(GameObject map, string name)
        {
            var t = map.transform.Find(name);
            return t != null ? t.GetComponent<Tilemap>() : null;
        }
    }
}
