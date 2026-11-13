using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace Assets.Script.Map.Backdrop
{
    /// <summary>
    /// NỀN NHIỀU LỚP CÓ CHIỀU SÂU (parallax) cho mọi map — dựng lúc chạy, không phải sửa từng prefab.
    ///
    /// Vì sao trông "sâu": mắt thấy vật ở xa trôi CHẬM hơn vật ở gần khi mình đi ngang (như nhìn qua cửa sổ xe).
    /// Mỗi lớp có hệ số bám camera f: 1 = dính chặt màn hình (trời, như ở vô cực) · 0 = đứng yên theo map (như mặt đất).
    ///   trời 1 · mây 0,95 (+ tự trôi) · núi xa 0,85 · sương · đồi giữa 0,7 · hàng cây gần 0,5 · rồi mới tới map (0).
    /// Thêm "phối cảnh khí quyển": lớp càng xa càng nhạt, càng ngả màu chân trời (BackdropPainter haze).
    ///
    /// Chủ đề theo map: MapInfo.backdrop (Auto = BackdropThemes.ForMap). Ảnh vẽ bằng code (BackdropPainter);
    /// có art riêng thì đặt Assets/Resources/Backdrops/&lt;Chủ đề&gt;/&lt;sky|clouds|far|mid|near&gt;.png — có file nào dùng file đó.
    /// </summary>
    public class ParallaxBackdrop : MonoBehaviour
    {
        public static ParallaxBackdrop Instance { get; private set; }

        /// <summary>1 lớp nền: f = hệ số bám camera (ngang / dọc), baseY = đáy lớp khi camera ở độ cao tham chiếu.</summary>
        public class Layer
        {
            public string name;
            public Transform tr;
            public SpriteRenderer sr;
            public float fx, fy, baseY, driftSpeed, drift, tileW;
            public bool screen;   // dính màn hình (trời, mặt trời): đặt theo camera, không parallax
            public Vector2 screenOffset;
        }

        private readonly List<Layer> _layers = new List<Layer>();
        private Transform _root;
        private Camera _cam;
        private float _mapCenter, _refY;

        /// <summary>Chủ đề đang dùng (AutoTestRunner đọc).</summary>
        public BackdropTheme Theme { get; private set; }
        public IReadOnlyList<Layer> Layers => _layers;

        // ảnh vẽ 1 lần mỗi chủ đề
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private void Awake()
        {
            Instance = this;
            MapManager.OnMapLoaded += Build;
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);   // đặt lớp SAU khi camera đã đi → không rung
        }

        private void OnDestroy()
        {
            MapManager.OnMapLoaded -= Build;
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        private void OnCameraUpdated(CinemachineBrain brain) => Place();
        private void LateUpdate() => Place();

        // Mây trôi: cộng 1 lần mỗi khung ở Update (Place chạy 2 lần / khung — sau camera và LateUpdate — nên không cộng ở đó)
        private void Update()
        {
            foreach (var l in _layers)
                if (l.driftSpeed != 0f && l.tileW > 0f) l.drift = Mathf.Repeat(l.drift + l.driftSpeed * Time.deltaTime, l.tileW);
        }

        // ---------------------------------------------------------------- dựng
        private void Build(GameObject map)
        {
            _layers.Clear();
            if (map == null) return;
            _cam = Camera.main;
            if (_cam == null) return;
            var info = map.GetComponentInChildren<MapInfo>(true);
            int mapId = info != null ? info.mapId : 1;
            float width = info != null ? info.width : 60f;
            float ground = info != null ? info.spawn.y : 4f;
            Theme = info != null && info.backdrop != BackdropTheme.Auto ? info.backdrop : BackdropThemes.ForMap(mapId);
            var p = BackdropThemes.Get(Theme);

            // Prefab map có sẵn tấm nền 1 màu "Background" (SpriteRenderer, thứ tự -100) kéo phủ cả map → che mất các lớp
            // phía sau (-200..-160). Có nền nhiều lớp thì tắt tấm đó (vẫn giữ trong prefab, đổi Auto → tắt ParallaxBackdrop là về như cũ).
            foreach (var sr in map.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.gameObject.name == "Background") sr.enabled = false;

            _root = new GameObject("[Backdrop]").transform;
            _root.SetParent(map.transform, false);   // đổi map → prefab map bị huỷ → nền huỷ theo
            _mapCenter = width / 2f;
            _refY = ground + 2.5f;                   // độ cao camera thường gặp khi đứng trên đất
            _cam.backgroundColor = p.skyTop;

            float viewH = _cam.orthographicSize * 2f, viewW = viewH * _cam.aspect;
            string key = Theme.ToString();
            int seed = (int)Theme * 101;

            bool cave = Theme == BackdropTheme.Cave, indoor = Theme == BackdropTheme.Indoor;
            // 0. Có ẢNH NỀN THẬT (Resources/Backdrops/<chủ đề>/sky|far|mid|near) → dùng ảnh, không vẽ bằng code
            if (!indoor && BuildFromArt(key, p, ground, width, viewW, viewH)) { Place(); return; }

            // 1. Trời (dính màn hình, kéo giãn phủ khung nhìn)
            var sky = Get(key, "sky", () => BackdropPainter.Sky(p), 0.5f);
            AddScreen("Sky", sky, -200, new Vector2(viewW * 1.4f, viewH * 1.4f), Vector2.zero);
            if (p.sun) AddScreen("Sun", Get(key, "sun", () => BackdropPainter.Sun(Color.Lerp(Color.white, p.skyHorizon, 0.3f)), 0.5f), -199,
                new Vector2(3.2f, 3.2f), new Vector2(viewW * 0.3f, viewH * 0.27f));

            if (indoor)
            {
                // Trong nhà: tường có cửa sổ sáng (xa) + cột gỗ, đèn lồng (gần)
                AddTiled("Wall", Get(key, "far", () => BackdropPainter.IndoorWall(1024, 448, p.far, Color.Lerp(p.skyHorizon, Color.white, 0.35f))),
                    -180, 0.85f, 0.8f, ground - 3f, width, viewW);
                AddTiled("Pillars", Get(key, "near", () => BackdropPainter.Near(NearKind.Pillars, 1024, 448, seed + 3, p.near, p.skyHorizon, 0.05f)),
                    -160, 0.55f, 0.5f, ground - 3f, width, viewW);
                return;
            }

            // 2. Mây (tự trôi chậm)
            if (p.clouds)
            {
                var cl = AddTiled("Clouds", Get(key, "clouds", () => BackdropPainter.Clouds(1024, 192, seed + 1, new Color(1, 1, 1, 0.9f))),
                    -190, 0.95f, 0.9f, ground + 4.5f, width, viewW);
                cl.driftSpeed = 0.18f;
            }
            // 3. Núi xa — nhạt nhiều (haze 0,55), nhấp nhô nhiều đỉnh
            AddTiled("Far", Get(key, "far", () => BackdropPainter.Ridge(1024, 320, seed + 2, p.far, p.skyHorizon, 0.55f, 0.35f, 0.95f, 2, 14, 0.9f, p.snowCaps, p.flatTops)),
                -180, 0.85f, 0.8f, ground - 2.5f, width, viewW);
            if (cave)
                AddTiled("Ceiling", Get(key, "ceiling", () => BackdropPainter.Ridge(1024, 256, seed + 9, p.far, p.skyHorizon, 0.35f, 0.15f, 0.6f, 3, 18, 0.7f, false, false, true)),
                    -179, 0.85f, 0.8f, ground + 5.5f, width, viewW);
            // 4. Sương giữa núi xa và đồi
            if (p.fog.a > 0.01f) AddTiled("FogFar", Get(key, "fog", () => BackdropPainter.Fog(p.fog)), -175, 0.8f, 0.75f, ground - 1.5f, width, viewW);
            // 5. Đồi giữa — đậm hơn, thoải hơn
            AddTiled("Mid", Get(key, "mid", () => BackdropPainter.Ridge(1024, 256, seed + 4, p.mid, p.skyHorizon, 0.32f, 0.3f, 0.75f, 1, 7, 1.2f, p.snowCaps && !cave, false)),
                -170, 0.7f, 0.65f, ground - 2.5f, width, viewW);
            if (p.fog.a > 0.3f) AddTiled("FogNear", Get(key, "fog", () => BackdropPainter.Fog(p.fog)), -165, 0.62f, 0.6f, ground - 2f, width, viewW);
            // 6. Hàng cây / trúc / đá gần — đậm nhất nhưng vẫn pha sương 30% để nhân vật / quái (lớp map) luôn nổi hơn nền
            AddTiled("Near", Get(key, "near", () => BackdropPainter.Near(p.nearKind, 1024, 224, seed + 5, p.near, p.skyHorizon, 0.3f)),
                -160, 0.5f, 0.45f, ground - 1.6f, width, viewW);
            Place();
        }

        /// <summary>Ảnh vẽ bằng code theo chủ đề (vẽ 1 lần, giữ lại cho lần sau).</summary>
        private static Sprite Get(string theme, string layer, System.Func<Texture2D> paint, float pivotY = 0f)
        {
            string k = theme + "/" + layer;
            if (Cache.TryGetValue(k, out var s) && s != null) return s;
            s = BackdropPainter.ToSprite(paint(), pivotY);
            Cache[k] = s;
            return s;
        }

        // ---------------------------------------------------------------- ảnh nền thật
        private static Texture2D Art(string theme, string layer) => Resources.Load<Texture2D>($"Backdrops/{theme}/{layer}");

        /// <summary>
        /// Dựng nền từ ảnh thật. Không có ảnh far / mid / near nào → false (dùng nền vẽ bằng code).
        /// Thiếu ảnh trời → trời vẽ bằng code; lớp nào thiếu ảnh thì bỏ lớp đó.
        /// </summary>
        private bool BuildFromArt(string key, BackdropPalette p, float ground, float width, float viewW, float viewH)
        {
            Texture2D sky = Art(key, "sky"), far = Art(key, "far"), mid = Art(key, "mid"), near = Art(key, "near");
            if (far == null && mid == null && near == null) return false;
            if (sky != null) AddScreen("Sky", ArtSprite(key, "sky", sky, null, 10f), -200, new Vector2(viewW * 1.3f, viewH * 1.3f), Vector2.zero);
            else
            {
                AddScreen("Sky", Get(key, "sky", () => BackdropPainter.Sky(p), 0.5f), -200, new Vector2(viewW * 1.4f, viewH * 1.4f), Vector2.zero);
                if (p.sun) AddScreen("Sun", Get(key, "sun", () => BackdropPainter.Sun(Color.Lerp(Color.white, p.skyHorizon, 0.3f)), 0.5f), -199,
                    new Vector2(3.2f, 3.2f), new Vector2(viewW * 0.3f, viewH * 0.27f));
            }
            // Lớp bóng cây 1 màu được tô lại: xa → pha màu chân trời nhiều (nhạt), gần → đậm
            if (far != null) AddArt("Far", key, "far", far, -180, 0.85f, 0.8f, Color.Lerp(p.far, p.skyHorizon, 0.55f), ground, width, viewW, viewH, 0.7f);
            if (p.fog.a > 0.01f) AddTiled("FogFar", Get(key, "fog", () => BackdropPainter.Fog(p.fog)), -175, 0.8f, 0.75f, ground - 1.5f, width, viewW);
            if (mid != null) AddArt("Mid", key, "mid", mid, -170, 0.7f, 0.65f, Color.Lerp(p.mid, p.skyHorizon, 0.35f), ground, width, viewW, viewH, 0.55f);
            if (p.fog.a > 0.3f) AddTiled("FogNear", Get(key, "fog", () => BackdropPainter.Fog(p.fog)), -165, 0.62f, 0.6f, ground - 2f, width, viewW);
            if (near != null) AddArt("Near", key, "near", near, -160, 0.5f, 0.45f, Color.Lerp(p.near, p.skyHorizon, 0.2f), ground, width, viewW, viewH, 0.45f);
            return true;
        }

        /// <summary>1 lớp ảnh: ảnh có trời sẵn ở mép trên (núi kèm trời) → kéo cao phủ quá màn hình; còn lại cao = heightFrac × khung nhìn.</summary>
        private void AddArt(string name, string key, string layer, Texture2D tex, int order, float fx, float fy, Color tint,
                            float ground, float width, float viewW, float viewH, float heightFrac)
        {
            var readable = Readable(tex);
            bool opaqueTop = TopIsOpaque(readable);
            float hUnits = opaqueTop ? viewH * 1.35f : viewH * heightFrac;
            var s = ArtSprite(key, layer, readable, tint, hUnits, name == "Near" ? 0f : 0.22f);
            AddTiled(name, s, order, fx, fy, opaqueTop ? ground - 4f : ground - 2.5f, width, viewW);
        }

        /// <summary>
        /// Ảnh → sprite cao hUnits đơn vị (pixelsPerUnit tính theo chiều cao ảnh). tint != null và ảnh là BÓNG 1 MÀU
        /// (độ sáng các điểm ít chênh, tối) → tô lại bằng màu tint, giữ viền sáng nếu có.
        /// </summary>
        private static Sprite ArtSprite(string key, string layer, Texture2D tex, Color? tint, float hUnits, float harmonize = 0f)
        {
            string k = "art/" + key + "/" + layer;
            if (Cache.TryGetValue(k, out var s) && s != null) return s;
            var src = tint.HasValue ? Readable(tex) : tex;
            if (tint.HasValue)
            {
                if (IsSilhouette(src, out float meanLum)) src = Recolor(src, tint.Value, meanLum);
                else if (harmonize > 0f) src = Harmonize(src, tint.Value, harmonize);
            }
            s = Sprite.Create(src, new Rect(0, 0, src.width, src.height), new Vector2(0.5f, 0f), src.height / Mathf.Max(1f, hUnits), 0, SpriteMeshType.FullRect);
            Cache[k] = s;
            return s;
        }

        /// <summary>Ảnh nhập không bật Read/Write → chép qua RenderTexture để đọc được điểm ảnh.</summary>
        private static Texture2D Readable(Texture2D t)
        {
            if (t.isReadable) return t;
            var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var c = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            c.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
            c.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return c;
        }

        private static bool TopIsOpaque(Texture2D t)
        {
            int y = t.height - 3, n = 0, op = 0;
            for (int x = 0; x < t.width; x += 8) { n++; if (t.GetPixel(x, y).a > 0.9f) op++; }
            return n > 0 && op > n * 0.9f;
        }

        /// <summary>
        /// Bóng 1 màu (lớp hàng cây đen / xám của game gốc): ≥ 80% điểm đục có độ sáng gần bằng nhau, tối, và ÍT MÀU (gần xám).
        /// Ảnh có màu thật (núi đỏ, núi tuyết, cây thông xanh) không phải bóng → giữ màu gốc.
        /// </summary>
        private static bool IsSilhouette(Texture2D t, out float mean)
        {
            var lums = new List<float>();
            double satSum = 0;
            for (int y = 0; y < t.height; y += 6)
                for (int x = 0; x < t.width; x += 6)
                {
                    var c = t.GetPixel(x, y);
                    if (c.a < 0.6f) continue;
                    lums.Add(c.r * 0.3f + c.g * 0.59f + c.b * 0.11f);
                    float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    satSum += mx > 0.001f ? (mx - mn) / mx : 0f;
                }
            mean = 0f;
            if (lums.Count < 50) return false;
            lums.Sort();
            float median = lums[lums.Count / 2];
            int near = 0;
            foreach (float l in lums) if (Mathf.Abs(l - median) < 0.08f) near++;
            mean = median;
            return near >= lums.Count * 0.8f && median < 0.28f && satSum / lums.Count < 0.55;   // bóng gốc rất tối (độ sáng 0,1–0,2), xanh-đen độ màu ~0,4; núi đỏ sáng ~0,4 → không phải bóng
        }

        /// <summary>Ảnh màu ở lớp xa / giữa: kéo nhẹ về tông chủ đề (k phần) cho hợp màu trời — vẫn giữ chi tiết.</summary>
        private static Texture2D Harmonize(Texture2D t, Color tint, float k)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) continue;
                Color o = Color.Lerp(c, tint, k);
                px[i] = new Color32((byte)(o.r * 255), (byte)(o.g * 255), (byte)(o.b * 255), c.a);
            }
            var r = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            r.SetPixels32(px);
            r.Apply(false, true);
            return r;
        }

        /// <summary>Tô lại bóng: màu = tint, giữ alpha; điểm sáng hơn trung bình (viền tuyết / nắng) giữ sáng.</summary>
        private static Texture2D Recolor(Texture2D t, Color tint, float meanLum)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) continue;
                float l = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
                Color o = Color.Lerp(tint, Color.white, Mathf.Clamp01((l - meanLum) * 2.2f));
                px[i] = new Color32((byte)(o.r * 255), (byte)(o.g * 255), (byte)(o.b * 255), c.a);
            }
            var r = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            r.SetPixels32(px);
            r.Apply(false, true);
            return r;
        }

        private Layer NewLayer(string name, Sprite s, int order)
        {
            // Lặp NGANG nhưng KẸP DỌC: để Repeat cả 2 chiều thì khi lọc mượt, mép trên trong suốt bị pha với hàng đáy đục
            // của ảnh → hiện 1 vạch kẻ ngang mảnh ở đỉnh mỗi lớp (đã gặp 2026-11-13)
            if (s != null && s.texture != null)
            {
                s.texture.wrapModeU = TextureWrapMode.Repeat;
                s.texture.wrapModeV = TextureWrapMode.Clamp;
            }
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;   // sau lớp Background (-100) của map, sau mọi nhân vật / quái
            var l = new Layer { name = name, tr = go.transform, sr = sr };
            _layers.Add(l);
            return l;
        }

        private void AddScreen(string name, Sprite s, int order, Vector2 size, Vector2 offset)
        {
            var l = NewLayer(name, s, order);
            l.screen = true; l.screenOffset = offset; l.fx = l.fy = 1f;
            Vector2 native = s.bounds.size;
            l.tr.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        }

        /// <summary>Lớp lặp ngang: đủ rộng để camera đi hết map vẫn không lộ mép (= khung nhìn + phần lớp trượt so với camera).</summary>
        private Layer AddTiled(string name, Sprite s, int order, float fx, float fy, float baseY, float mapWidth, float viewW, float height = 0f)
        {
            var l = NewLayer(name, s, order);
            l.fx = fx; l.fy = fy; l.baseY = baseY;
            l.tileW = s.bounds.size.x;
            float h = height > 0 ? height : s.bounds.size.y;
            l.sr.drawMode = SpriteDrawMode.Tiled;
            l.sr.tileMode = SpriteTileMode.Continuous;
            l.sr.size = new Vector2(viewW + mapWidth * (1f - fx) + l.tileW * 2f + 8f, h);
            return l;
        }

        // ---------------------------------------------------------------- mỗi khung
        /// <summary>
        /// Đặt lớp theo camera: x = tâm map + (camera.x − tâm map) × f → lớp đi theo camera f phần, tụt lại (1 − f) phần.
        /// Mây cộng thêm độ trôi theo thời gian (quấn theo bề ngang 1 ô ảnh → trôi mãi không hết).
        /// </summary>
        private void Place()
        {
            if (_cam == null || _layers.Count == 0) return;
            Vector3 c = _cam.transform.position;
            foreach (var l in _layers)
            {
                if (l.tr == null) continue;
                if (l.screen) { l.tr.position = new Vector3(c.x + l.screenOffset.x, c.y + l.screenOffset.y, 0f); continue; }
                float x = _mapCenter + (c.x - _mapCenter) * l.fx + l.drift;
                float y = l.baseY + (c.y - _refY) * l.fy;
                l.tr.position = new Vector3(x, y, 0f);
            }
        }
    }
}
