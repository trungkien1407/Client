using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VocVach.StackFit
{
    /// <summary>
    /// CỬA SỔ GHÉP ẢNH CHỒNG (Tools → VocVach → Ghép ảnh thân + chân).
    ///   · Trái: danh sách bộ hình (dấu ✎ = đang dùng số chỉnh tay, dấu * = đã kéo nhưng chưa lưu).
    ///   · Phải: xem trước phóng to — ảnh dưới đứng yên, KÉO CHUỘT ảnh trên để dời; phím ←↑→↓ dời 1 điểm ảnh (giữ Shift = 5).
    ///     Bóng xanh mờ = vị trí tự động (để so). Vạch đỏ ngang = mặt đất (đáy ảnh dưới), vạch dọc = giữa.
    ///   · Dưới: ô số X / Y, [Lưu] (ghi cấu hình lâu dài qua nguồn dữ liệu), [Hoàn tác], [Về số tự động], [Lưu tất cả].
    /// Dữ liệu đọc / ghi qua StackFitProvider của dự án (package không biết game).
    /// </summary>
    public class StackFitWindow : EditorWindow
    {
        private const float ListW = 250f;
        private List<StackFitProvider> _providers;
        private int _providerIdx;
        private List<StackFitItem> _items = new List<StackFitItem>();
        private readonly Dictionary<string, Vector2> _pending = new Dictionary<string, Vector2>();   // đã kéo, chưa lưu
        private readonly Dictionary<string, Vector2?> _autoCache = new Dictionary<string, Vector2?>();
        private int _sel = -1;
        private string _search = "";
        private bool _manualOnly, _showGhost = true, _darkBg = true;
        private float _zoom = 3f;
        private Vector2 _listScroll;
        private bool _dragging;
        private Vector2 _dragAccum;

        [MenuItem("Tools/VocVach/Ghép ảnh thân + chân", priority = 200)]
        public static void Open() => GetWindow<StackFitWindow>("Ghép ảnh").Show();

        private StackFitProvider Provider => _providers != null && _providerIdx < _providers.Count ? _providers[_providerIdx] : null;
        private StackFitItem Selected => _sel >= 0 && _sel < _items.Count ? _items[_sel] : null;

        private void OnEnable()
        {
            minSize = new Vector2(720, 460);
            // Tìm mọi lớp nguồn dữ liệu dự án đã viết (lớp con StackFitProvider, có hàm dựng không tham số)
            _providers = TypeCache.GetTypesDerivedFrom<StackFitProvider>()
                .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (StackFitProvider)Activator.CreateInstance(t)).ToList();
            Reload();
        }

        private void Reload()
        {
            _pending.Clear();
            _autoCache.Clear();
            _items = Provider != null ? Provider.Load() ?? new List<StackFitItem>() : new List<StackFitItem>();
            if (_sel >= _items.Count) _sel = _items.Count > 0 ? 0 : -1;
            if (_sel < 0 && _items.Count > 0) _sel = 0;
            Repaint();
        }

        private Vector2 Current(StackFitItem it) => _pending.TryGetValue(it.Id, out var v) ? v : it.Offset;

        private Vector2? Auto(StackFitItem it)
        {
            if (!_autoCache.TryGetValue(it.Id, out var v)) { v = Provider?.AutoOffset(it); _autoCache[it.Id] = v; }
            return v;
        }

        private void OnGUI()
        {
            if (_providers == null || _providers.Count == 0)
            {
                EditorGUILayout.HelpBox("Chưa có nguồn dữ liệu. Viết 1 lớp con của VocVach.StackFit.StackFitProvider trong thư mục Editor của dự án (xem README của package).", MessageType.Info);
                return;
            }
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                using (new EditorGUILayout.VerticalScope())
                {
                    var it = Selected;
                    if (it == null) { EditorGUILayout.HelpBox("Chọn 1 bộ hình ở danh sách bên trái.", MessageType.None); return; }
                    var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawPreview(area, it);
                    DrawControls(it);
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int p = EditorGUILayout.Popup(_providerIdx, _providers.Select(x => x.Title).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(180));
                if (p != _providerIdx) { _providerIdx = p; _sel = -1; Reload(); }
                GUILayout.Label("Tìm", GUILayout.Width(26));
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(120));
                _manualOnly = GUILayout.Toggle(_manualOnly, "Chỉ bộ chỉnh tay", EditorStyles.toolbarButton);
                _showGhost = GUILayout.Toggle(_showGhost, "Bóng tự động", EditorStyles.toolbarButton);
                _darkBg = GUILayout.Toggle(_darkBg, "Nền tối", EditorStyles.toolbarButton);
                GUILayout.Label("Phóng", GUILayout.Width(40));
                _zoom = GUILayout.HorizontalSlider(_zoom, 1f, 8f, GUILayout.Width(90));
                GUILayout.Label($"×{_zoom:0.0}", GUILayout.Width(34));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_pending.Count == 0))
                    if (GUILayout.Button($"Lưu tất cả ({_pending.Count})", EditorStyles.toolbarButton)) SaveAll();
                if (GUILayout.Button("Tải lại", EditorStyles.toolbarButton))
                {
                    if (_pending.Count == 0 || EditorUtility.DisplayDialog("Tải lại", $"Bỏ {_pending.Count} chỗ đã kéo mà chưa lưu?", "Bỏ", "Thôi")) Reload();
                }
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListW)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    if (_manualOnly && !it.Manual && !_pending.ContainsKey(it.Id)) continue;
                    if (!string.IsNullOrEmpty(_search) && (it.Label ?? it.Id).IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var o = Current(it);
                    string mark = (_pending.ContainsKey(it.Id) ? "* " : "") + (it.Manual ? "✎ " : "");
                    string text = $"{mark}{it.Label ?? it.Id}   ({o.x:0}, {o.y:0})";
                    var style = new GUIStyle(EditorStyles.label) { fontStyle = it.Manual ? FontStyle.Bold : FontStyle.Normal };
                    if (_pending.ContainsKey(it.Id)) style.normal.textColor = new Color(1f, 0.75f, 0.3f);
                    var r = GUILayoutUtility.GetRect(new GUIContent(text), style, GUILayout.Height(20));
                    if (i == _sel) EditorGUI.DrawRect(r, new Color(0.24f, 0.45f, 0.8f, 0.5f));
                    if (GUI.Button(r, text, style)) { _sel = i; GUI.FocusControl(null); }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.HelpBox("✎ = đang dùng số chỉnh tay (đã lưu)\n* = đã kéo, chưa lưu", MessageType.None);
            }
        }

        private void DrawPreview(Rect area, StackFitItem it)
        {
            EditorGUI.DrawRect(area, _darkBg ? new Color(0.16f, 0.17f, 0.2f) : new Color(0.85f, 0.87f, 0.9f));
            // gốc = giữa-đáy ảnh dưới, đặt ở giữa vùng, cách đáy 18%
            var origin = new Vector2(area.center.x, area.yMax - area.height * 0.18f);
            EditorGUI.DrawRect(new Rect(area.x, origin.y, area.width, 1), new Color(1f, 0.3f, 0.3f, 0.8f));      // mặt đất
            EditorGUI.DrawRect(new Rect(origin.x, area.y, 1, area.height), new Color(1f, 1f, 1f, 0.15f));          // giữa
            var off = Current(it);
            GUI.BeginClip(area);
            var o = origin - area.position;
            if (!Provider.TopDrawnOver) DrawSprite(it.Top, o, off, Color.white);
            if (it.Bottom != null) DrawSprite(it.Bottom, o, Vector2.zero, Color.white);
            var auto = Auto(it);
            if (_showGhost && auto.HasValue && (auto.Value - off).sqrMagnitude > 0.25f) DrawSprite(it.Top, o, auto.Value, new Color(0.4f, 0.8f, 1f, 0.35f));
            if (Provider.TopDrawnOver) DrawSprite(it.Top, o, off, Color.white);
            GUI.EndClip();
            GUI.Label(new Rect(area.x + 6, area.y + 4, area.width - 12, 36),
                $"{it.Label}   lệch ({off.x:0}, {off.y:0}) px" + (auto.HasValue ? $"   · tự động ({auto.Value.x:0}, {auto.Value.y:0})" : "") +
                "\nKéo chuột để dời ảnh trên · ←↑→↓ = 1 px (Shift = 5) · Ctrl+S = Lưu", EditorStyles.whiteMiniLabel);
            HandleInput(area, it);
        }

        /// <summary>Vẽ 1 sprite: điểm pivot của sprite đặt tại gốc + lệch (điểm ảnh × phóng), y lên trên.</summary>
        private void DrawSprite(Sprite s, Vector2 origin, Vector2 offsetPx, Color tint)
        {
            if (s == null || s.texture == null) return;
            Rect tr = s.textureRect;
            var tex = s.texture;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            Vector2 pivot = s.pivot;   // điểm ảnh tính từ góc dưới-trái của sprite
            float x = origin.x + (offsetPx.x - pivot.x) * _zoom;
            float yTop = origin.y - (offsetPx.y - pivot.y + tr.height) * _zoom;
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(new Rect(x, yTop, tr.width * _zoom, tr.height * _zoom), tex, uv, true);
            GUI.color = old;
        }

        private void HandleInput(Rect area, StackFitItem it)
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Keyboard, area);
            switch (e.type)
            {
                case EventType.MouseDown when area.Contains(e.mousePosition) && e.button == 0:
                    _dragging = true; _dragAccum = Vector2.zero;
                    GUIUtility.hotControl = id; GUIUtility.keyboardControl = id;
                    e.Use();
                    break;
                case EventType.MouseDrag when _dragging:
                    _dragAccum += new Vector2(e.delta.x, -e.delta.y) / _zoom;
                    // dời theo điểm ảnh nguyên (giữ phần lẻ để kéo chậm vẫn nhích được)
                    var step = new Vector2(Mathf.Round(_dragAccum.x), Mathf.Round(_dragAccum.y));
                    if (step != Vector2.zero) { SetPending(it, Current(it) + step); _dragAccum -= step; }
                    e.Use();
                    break;
                case EventType.MouseUp when _dragging:
                    _dragging = false; GUIUtility.hotControl = 0; e.Use();
                    break;
                case EventType.KeyDown:
                    if (GUIUtility.keyboardControl != 0 && GUIUtility.keyboardControl != id) break;   // đang gõ ô số
                    int n = e.shift ? 5 : 1;
                    Vector2 d = e.keyCode switch
                    {
                        KeyCode.LeftArrow => new Vector2(-n, 0), KeyCode.RightArrow => new Vector2(n, 0),
                        KeyCode.UpArrow => new Vector2(0, n), KeyCode.DownArrow => new Vector2(0, -n), _ => Vector2.zero
                    };
                    if (d != Vector2.zero) { SetPending(it, Current(it) + d); e.Use(); }
                    else if (e.keyCode == KeyCode.S && (e.control || e.command)) { SaveOne(it); e.Use(); }
                    break;
            }
        }

        private void SetPending(StackFitItem it, Vector2 v)
        {
            v = new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));
            if (v == it.Offset) _pending.Remove(it.Id); else _pending[it.Id] = v;
            Repaint();
        }

        private void DrawControls(StackFitItem it)
        {
            var cur = Current(it);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                int x = EditorGUILayout.IntField("X (phải +)", (int)cur.x);
                int y = EditorGUILayout.IntField("Y (lên +)", (int)cur.y);
                if (EditorGUI.EndChangeCheck()) SetPending(it, new Vector2(x, y));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_pending.ContainsKey(it.Id)))
                {
                    if (GUILayout.Button("Lưu (chỉnh tay)", GUILayout.Height(26))) SaveOne(it);
                    if (GUILayout.Button("Hoàn tác", GUILayout.Height(26))) { _pending.Remove(it.Id); Repaint(); }
                }
                using (new EditorGUI.DisabledScope(!it.Manual))
                    if (GUILayout.Button("Về số tự động", GUILayout.Height(26)) &&
                        EditorUtility.DisplayDialog("Về số tự động", $"Bỏ số chỉnh tay của {it.Label}, dùng lại số tự động?", "Bỏ", "Thôi"))
                    {
                        Provider.ResetToAuto(it);
                        _pending.Remove(it.Id);
                        _autoCache.Remove(it.Id);
                        Repaint();
                    }
            }
            EditorGUILayout.LabelField(it.Manual ? "Đang dùng số CHỈNH TAY (lưu trong cấu hình dự án)." : "Đang dùng số TỰ ĐỘNG.", EditorStyles.miniLabel);
        }

        private void SaveOne(StackFitItem it)
        {
            if (!_pending.TryGetValue(it.Id, out var v)) return;
            Provider.Save(it, v);
            _pending.Remove(it.Id);
            Repaint();
        }

        private void SaveAll()
        {
            foreach (var it in _items.Where(i => _pending.ContainsKey(i.Id)).ToList()) Provider.Save(it, _pending[it.Id]);
            _pending.Clear();
            Repaint();
        }

        private void OnDestroy()
        {
            if (_pending.Count > 0 && EditorUtility.DisplayDialog("Ghép ảnh", $"Còn {_pending.Count} bộ đã kéo mà chưa lưu. Lưu luôn?", "Lưu", "Bỏ"))
                SaveAll();
        }
    }
}
