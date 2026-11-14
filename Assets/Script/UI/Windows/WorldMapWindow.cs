using System.Collections.Generic;
using System.Linq;
using Assets.Script.Data;
using Assets.Script.Map;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// BẢN ĐỒ (phím B / bấm minimap) — 2 tab:
    ///   Thế giới    sơ đồ mọi map (server gửi WORLD_MAP: tên, cấp quái, cổng nối) — map đang đứng sáng viền vàng;
    ///               bấm 1 map → xem cấp quái, cách mấy map → "Tự đi tới đây" (Combat.AutoPath.BeginMap, server chỉ từng cổng).
    ///   Khu vực này map hiện tại vẽ từ tilemap (MapImage) + chấm NPC / quái / cổng / bạn.
    ///
    /// XẾP SƠ ĐỒ (tự động, thêm map mới không phải sửa code):
    ///   1. "Xương sống" = đường dài nhất đi từ làng đầu tiên (BFS tìm map xa nhất rồi lần ngược) → 1 hàng ngang dưới cùng.
    ///   2. Map còn lại (trường, nhánh rẽ) gắn vào map xương sống gần nhất, xếp thành hàng phía trên, cách nhau đủ rộng.
    ///   3. Vẽ đường nối giữa 2 map có cổng sang nhau, rồi vẽ ô map đè lên.
    /// Prefab cũ (chỉ có Frame + Legend) vẫn dùng được: thanh tab và sơ đồ thế giới tạo bằng code trong Bind().
    /// </summary>
    public class WorldMapWindow : GameWindow
    {
        private const float MaxW = 1100f, MaxScale = 16f, TabBar = 40f;
        private const float WorldW = 1080f, WorldH = 250f;   // vùng vẽ sơ đồ thế giới

        [SerializeField] private RawImage _map;
        private MapMarkers _markers; // lớp thường (không lưu prefab được) → tạo trong Bind
        private int _version = -1;
        private float _scale, _next;

        private static int _tab;               // 0 = Thế giới, 1 = Khu vực này (nhớ lần mở trước)
        private Button _tabWorld, _tabArea;
        private RectTransform _areaFrame, _legend, _worldRoot, _graph;
        private TextMeshProUGUI _info;
        private Button _go;
        private int _selected = -1;

        /// <summary>Số map đang vẽ trên tab Thế giới (AutoTestRunner kiểm).</summary>
        public int WorldNodeCount { get; private set; }
        public bool ShowingWorld => _tab == 0;

        protected override void Configure()
        {
            Title = "Bản đồ";
            HotKey = Key.B;
            Size = new Vector2(MaxW + 20, 330);
        }

        protected override void Build()
        {
            var body = CreateBody();

            var frame = UIKit.Panel("Frame", body, new Color(0.05f, 0.08f, 0.14f, 0.9f));
            frame.rectTransform.Fill(0, 0, 0, 30);
            _map = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _map.transform.SetParent(frame.transform, false);
            _map.raycastTarget = false;

            string Dot(Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>■</color>";
            var legend = UIKit.Text("Legend", body,
                $"{Dot(MapMarkers.Me)} Bạn    {Dot(MapMarkers.Npc)} NPC    {Dot(MapMarkers.Mob)} Quái    {Dot(MapMarkers.Portal)} Cổng    {Dot(MapMarkers.Other)} Người chơi" +
                "    <color=#9aa>Bục cam: nhảy xuyên từ dưới lên, bấm xuống để thả.</color>", 14, TextAlignmentOptions.MidlineLeft);
            legend.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 26), new Vector2(0.5f, 0));
        }

        protected override void Bind()
        {
            // Chấm NPC / quái / cổng / người chơi vẽ đè lên ảnh map (tạo lúc chạy)
            _markers = new MapMarkers(_map.rectTransform, true);
            _areaFrame = (RectTransform)_map.transform.parent;
            _areaFrame.Fill(0, 0, TabBar, 30);                       // chừa chỗ thanh tab phía trên
            _legend = Body.Find("Legend") as RectTransform;

            // ---- Thanh tab ----
            _tabWorld = UIKit.Button("TabWorld", Body, "Thế giới", () => SetTab(0), 15);
            ((RectTransform)_tabWorld.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(130, 32), new Vector2(0, 1));
            _tabArea = UIKit.Button("TabArea", Body, "Khu vực này", () => SetTab(1), 15);
            ((RectTransform)_tabArea.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(136, 0), new Vector2(130, 32), new Vector2(0, 1));
            UIStyle.Chip(_tabWorld, 15);
            UIStyle.Chip(_tabArea, 15);

            // ---- Tab Thế giới: vùng sơ đồ + dòng thông tin map đang chọn + nút Tự đi ----
            var bg = UIKit.Rect("World", Body).Fill(0, 0, TabBar, 0);
            UIStyle.SetRound(bg.gameObject.AddComponent<Image>(), new Color(0.05f, 0.08f, 0.14f, 0.92f), 1.5f);
            _worldRoot = bg;
            _graph = UIKit.Rect("Graph", bg);
            _graph.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(WorldW, WorldH), new Vector2(0.5f, 1));
            _info = UIKit.Text("Info", bg, "", 16, TextAlignmentOptions.MidlineLeft, Color.white);
            _info.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(-90, 8), new Vector2(-200, 40), new Vector2(0.5f, 0));
            _go = UIKit.Button("Go", bg, "Tự đi tới đây", GoSelected, 16);
            ((RectTransform)_go.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 10), new Vector2(160, 36), new Vector2(1, 0));
            _go.image.color = UIKit.ButtonHot;

            GameData.OnChanged += k =>
            {
                if (!IsOpen) return;
                if (k == DataKind.WorldMap) DrawWorld();
                SetTitle("Bản đồ — " + GameData.MapName);
            };
        }

        protected override void Refresh()
        {
            SetTitle(string.IsNullOrEmpty(GameData.MapName) ? "Bản đồ" : "Bản đồ — " + GameData.MapName);
            _version = -1;
            SetTab(_tab);
        }

        private void SetTab(int tab)
        {
            _tab = tab;
            bool world = tab == 0;
            _worldRoot.gameObject.SetActive(world);
            _areaFrame.gameObject.SetActive(!world);
            if (_legend != null) _legend.gameObject.SetActive(!world);
            _tabWorld.image.color = world ? UIStyle.Gold : UIStyle.ChipBg;
            _tabArea.image.color = world ? UIStyle.ChipBg : UIStyle.Gold;
            SetTabLabel(_tabWorld, world);
            SetTabLabel(_tabArea, !world);
            if (world)
            {
                ((RectTransform)transform).sizeDelta = new Vector2(WorldW + 40, WorldH + TabBar + 120);
                GameActions.WorldMap();   // map đang đứng có thể đã đổi → xin lại mỗi lần mở
                DrawWorld();
            }
            else _version = -1;           // tab Khu vực: Update đặt lại cỡ theo ảnh map
        }

        private static void SetTabLabel(Button b, bool on)
        {
            var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t == null) return;
            t.color = on ? new Color(0.25f, 0.13f, 0.08f) : Color.white;
            t.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            if (on) t.fontSharedMaterial = t.font.material; else UIStyle.Readable(t, 0.18f);
        }

        // ------------------------------------------------------------------ tab Thế giới
        private readonly Dictionary<int, Vector2> _pos = new Dictionary<int, Vector2>();

        /// <summary>Vẽ lại sơ đồ thế giới từ GameData.WorldMap (xếp vị trí → đường nối → ô map).</summary>
        private void DrawWorld()
        {
            UIKit.Clear(_graph);
            var nodes = GameData.WorldMap;
            WorldNodeCount = nodes.Count;
            if (nodes.Count == 0) { ShowInfo(null); _info.text = "<color=#9aa>Đang tải bản đồ thế giới...</color>"; return; }
            Layout(nodes);

            // đường nối (vẽ trước → nằm dưới ô map)
            var drawn = new HashSet<long>();
            foreach (var n in nodes)
                foreach (int t in n.links)
                {
                    if (!_pos.ContainsKey(t)) continue;
                    long key = n.id < t ? ((long)n.id << 32) | (uint)t : ((long)t << 32) | (uint)n.id;
                    if (drawn.Add(key)) Line(_pos[n.id], _pos[t]);
                }
            foreach (var n in nodes) Node(n);
            if (_selected < 0 || !_pos.ContainsKey(_selected)) _selected = GameData.WorldMapHere;
            ShowInfo(nodes.FirstOrDefault(n => n.id == _selected));
        }

        /// <summary>
        /// Tính vị trí mỗi map (gốc = góc dưới trái vùng vẽ). Xương sống = đường BFS dài nhất từ làng đầu tiên → hàng dưới;
        /// map khác treo lên map xương sống gần nhất, mỗi bậc xa hơn 1 hàng cao hơn.
        /// </summary>
        private void Layout(List<WorldMapNode> nodes)
        {
            _pos.Clear();
            var byId = nodes.ToDictionary(n => n.id);
            var start = nodes.Where(n => n.safe).OrderBy(n => n.id).FirstOrDefault() ?? nodes[0];

            // BFS từ làng đầu: khoảng cách + "đến từ đâu"
            var dist = new Dictionary<int, int> { [start.id] = 0 };
            var from = new Dictionary<int, int> { [start.id] = start.id };
            var q = new Queue<int>();
            q.Enqueue(start.id);
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                foreach (int t in byId[cur].links)
                {
                    if (!byId.ContainsKey(t) || dist.ContainsKey(t)) continue;
                    dist[t] = dist[cur] + 1; from[t] = cur; q.Enqueue(t);
                }
            }
            int far = dist.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
            var spine = new List<int>();
            for (int at = far; ; at = from[at]) { spine.Insert(0, at); if (at == start.id) break; }

            float step = spine.Count > 1 ? Mathf.Min(170f, (WorldW - 150f) / (spine.Count - 1)) : 0f;
            float x0 = (WorldW - step * (spine.Count - 1)) / 2f, baseY = 70f, rowH = 95f;
            for (int i = 0; i < spine.Count; i++) _pos[spine[i]] = new Vector2(x0 + i * step, baseY);

            // map ngoài xương sống: bậc (số bước tới xương sống) → hàng; vị trí mong muốn quanh map neo, rồi đẩy cho khỏi chồng
            var rows = new SortedDictionary<int, List<(int id, float want)>>();
            var groups = new Dictionary<int, List<int>>();
            foreach (var n in nodes.OrderBy(n => n.id))
            {
                if (_pos.ContainsKey(n.id)) continue;
                int at = n.id, depth = 0;
                while (dist.ContainsKey(at) && !_pos.ContainsKey(at) && from[at] != at) { at = from[at]; depth++; }
                if (!_pos.ContainsKey(at)) { at = spine[0]; depth = 1; }   // không nối với làng đầu (lạ) → treo vào đầu
                if (!groups.TryGetValue(at * 100 + depth, out var g)) groups[at * 100 + depth] = g = new List<int>();
                g.Add(n.id);
                if (!rows.ContainsKey(depth)) rows[depth] = new List<(int, float)>();
            }
            const float bStep = 132f;
            foreach (var kv in groups)
            {
                int anchor = kv.Key / 100, depth = kv.Key % 100;
                for (int j = 0; j < kv.Value.Count; j++)
                    rows[depth].Add((kv.Value[j], _pos[anchor].x + (j - (kv.Value.Count - 1) / 2f) * bStep));
            }
            foreach (var kv in rows)
            {
                var row = kv.Value.OrderBy(r => r.want).ToList();
                float minX = 70f;
                for (int i = 0; i < row.Count; i++)
                {
                    float x = Mathf.Max(row[i].want, minX);
                    _pos[row[i].id] = new Vector2(x, baseY + kv.Key * rowH + 40f);
                    minX = x + bStep;
                }
                // tràn mép phải → dồn cả hàng sang trái
                float over = minX - bStep - (WorldW - 70f);
                if (over > 0) foreach (var r in row) _pos[r.id] -= new Vector2(over, 0);
            }
        }

        private void Line(Vector2 a, Vector2 b)
        {
            var rt = UIKit.Rect("Line", _graph);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.82f, 0.4f, 0.55f);
            img.raycastTarget = false;
            Vector2 d = b - a;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, 4f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        /// <summary>1 ô map: tên + cấp quái (hoặc "Làng · an toàn"); map đang đứng viền vàng dày + chữ "Bạn ở đây".</summary>
        private void Node(WorldMapNode n)
        {
            bool here = n.id == GameData.WorldMapHere, sel = n.id == _selected;
            bool small = _pos[n.id].y > 100f;   // hàng nhánh (trường...) nhỏ hơn
            var size = small ? new Vector2(116, 48) : new Vector2(124, 58);
            string sub = n.safe ? (small ? "Trường · an toàn" : "Làng · an toàn") : n.lvMin > 0 ? $"Quái cấp {n.lvMin}–{n.lvMax}" : "";
            var b = UIKit.Button("M" + n.id, _graph, $"<b>{n.name}</b>\n<size=12><color=#cfd8e3>{sub}</color></size>", () => { _selected = n.id; DrawWorld(); }, 15);
            ((RectTransform)b.transform).Place(Vector2.zero, Vector2.zero, _pos[n.id], size, new Vector2(0.5f, 0.5f));
            UIStyle.Chip(b, 15);
            b.image.color = n.safe ? new Color(0.13f, 0.34f, 0.24f, 0.95f) : new Color(0.16f, 0.17f, 0.28f, 0.95f);
            var ring = b.transform.Find("Ring").GetComponent<Image>();
            ring.color = here ? UIStyle.Gold : sel ? Color.white : new Color(1f, 0.82f, 0.4f, 0.45f);
            if (here || sel) ring.pixelsPerUnitMultiplier = 0.8f;   // viền dày hơn
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.enableWordWrapping = false;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;   // tên dài (Đầm Lầy Sương Mù) tự nhỏ lại cho vừa ô
                label.fontSizeMin = 10;
                label.fontSizeMax = 15;
                label.rectTransform.Fill(4, 4, 2, 2);
            }
            if (here)
            {
                var tag = UIKit.Text("Here", b.transform, "Bạn ở đây", 13, TextAlignmentOptions.Center, UIStyle.Gold);
                tag.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 2), new Vector2(130, 18), new Vector2(0.5f, 0));
                tag.raycastTarget = false;
                UIStyle.Readable(tag);
            }
        }

        /// <summary>Dòng dưới sơ đồ: map đang chọn — cấp quái, cách mấy map; nút "Tự đi tới đây".</summary>
        private void ShowInfo(WorldMapNode n)
        {
            if (n == null) { _info.text = "<color=#9aa>Bấm 1 map để xem — rồi \"Tự đi tới đây\".</color>"; _go.gameObject.SetActive(false); return; }
            int hops = Hops(GameData.WorldMapHere, n.id);
            string lv = n.safe ? "làng / trường an toàn (không đánh nhau)" : n.lvMin > 0 ? $"quái cấp {n.lvMin}–{n.lvMax}" : "không có quái";
            string far = n.id == GameData.WorldMapHere ? "<color=#ffd56b>bạn đang ở đây</color>" : hops > 0 ? $"cách {hops} map" : "<color=#f88>không đi bộ tới được từ đây</color>";
            _info.text = $"<b>{n.name}</b> — {lv} · {far}";
            UIStyle.Readable(_info, 0.15f);
            _go.gameObject.SetActive(n.id != GameData.WorldMapHere && hops > 0);
        }

        /// <summary>Số cổng phải đi từ map a tới map b (BFS trên GameData.WorldMap); không tới được → -1.</summary>
        private static int Hops(int a, int b)
        {
            if (a == b) return 0;
            var links = GameData.WorldMap.ToDictionary(n => n.id, n => n.links);
            if (!links.ContainsKey(a)) return -1;
            var dist = new Dictionary<int, int> { [a] = 0 };
            var q = new Queue<int>();
            q.Enqueue(a);
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                if (cur == b) return dist[cur];
                if (!links.TryGetValue(cur, out var next)) continue;
                foreach (int t in next) if (!dist.ContainsKey(t)) { dist[t] = dist[cur] + 1; q.Enqueue(t); }
            }
            return -1;
        }

        private void GoSelected()
        {
            if (_selected < 0 || _selected == GameData.WorldMapHere) return;
            Hide();
            Combat.AutoPath.BeginMap(_selected);
        }

        /// <summary>Bấm map {id} rồi "Tự đi tới đây" (AutoTestRunner dùng — giống người chơi bấm).</summary>
        public bool ClickGo(int id)
        {
            var node = _graph != null ? _graph.Find("M" + id) : null;
            if (node == null) return false;
            node.GetComponent<Button>().onClick.Invoke();
            if (!_go.gameObject.activeSelf) return false;
            _go.onClick.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ tab Khu vực này
        private void Update()
        {
            if (_tab == 0 || MapImage.Texture == null) return;
            if (_version != MapImage.Version)
            {
                _version = MapImage.Version;
                _map.texture = MapImage.Texture;
                _scale = Mathf.Min(MaxScale, MaxW / MapImage.Width);
                var size = new Vector2(MapImage.Width * _scale, MapImage.Height * _scale);
                _map.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
                // Cửa sổ vừa khít ảnh map: + thanh tab + chữ chú thích + tiêu đề
                ((RectTransform)transform).sizeDelta = new Vector2(Mathf.Max(640, size.x + 40), size.y + 110 + TabBar);
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            _markers.Redraw(_scale);
        }
    }
}
