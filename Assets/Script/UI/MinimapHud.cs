using Assets.Script.Map;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// MINIMAP góc phải trên (prefab Resources/UI/Hud/MinimapHud): ảnh map (MapImage) cuộn theo nhân vật + chấm NPC / quái /
    /// cổng / người chơi. Bấm vào hoặc phím B → bản đồ lớn (WorldMapWindow). GameHud tạo.
    /// </summary>
    public class MinimapHud : HudPanel
    {
        public const float W = 160f, H = 112f;

        [SerializeField] private Button _button;
        [SerializeField] private RawImage _map;
        private MapMarkers _markers;
        private int _version = -1;
        private float _scale, _next;

        protected override void Build()
        {
            gameObject.name = "Minimap";   // AutoTestRunner tìm theo tên
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.75f);
            ((RectTransform)transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(W, H), new Vector2(1, 1));
            _button = gameObject.AddComponent<Button>();

            var view = UIKit.Rect("View", transform).Fill(2, 2, 2, 2);
            view.gameObject.AddComponent<RectMask2D>();
            _map = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _map.transform.SetParent(view, false);
            _map.gameObject.layer = 5;
            _map.raycastTarget = false;

            var hint = UIKit.Text("Hint", transform, "B", 12, TextAlignmentOptions.TopRight, new Color(1, 1, 1, 0.6f));
            hint.rectTransform.Fill(0, 4, 2, 0);
            hint.raycastTarget = false;
        }

        protected override void Bind()
        {
            gameObject.name = "Minimap";
            _button.onClick.AddListener(() => GameWindow.Get<WorldMapWindow>().Toggle());
            _markers = new MapMarkers(_map.rectTransform, false);
        }

        private void Update()
        {
            if (MapImage.Texture == null) { _map.enabled = false; return; }
            if (_version != MapImage.Version)
            {
                _version = MapImage.Version;
                _map.enabled = true;
                _map.texture = MapImage.Texture;
                _scale = (H - 4) / MapImage.Height;
                _map.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero,
                    new Vector2(MapImage.Width * _scale, MapImage.Height * _scale), Vector2.zero);
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.15f;

            var me = _markers.Redraw(_scale);
            // Cuộn ngang để bạn ở giữa, không lộ ngoài mép map
            float viewW = W - 4, mapW = MapImage.Width * _scale;
            float x = mapW <= viewW ? (viewW - mapW) / 2f : Mathf.Clamp(viewW / 2f - me.x * _scale, viewW - mapW, 0f);
            _map.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
