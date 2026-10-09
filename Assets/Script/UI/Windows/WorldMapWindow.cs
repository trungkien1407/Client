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
    /// BẢN ĐỒ LỚN (phím B / bấm minimap): cả map hiện tại + tên NPC, cổng đi map nào, vị trí bạn và quái.
    /// </summary>
    public class WorldMapWindow : GameWindow
    {
        private const float MaxW = 1100f, MaxScale = 16f;

        private RawImage _map;
        private MapMarkers _markers;
        private int _version = -1;
        private float _scale, _next;

        protected override void Build()
        {
            Title = "Bản đồ";
            HotKey = Key.B;
            Size = new Vector2(MaxW + 20, 330);
            var body = CreateBody();

            var frame = UIKit.Panel("Frame", body, new Color(0.05f, 0.08f, 0.14f, 0.9f));
            frame.rectTransform.Fill(0, 0, 0, 30);
            _map = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _map.transform.SetParent(frame.transform, false);
            _map.raycastTarget = false;
            _markers = new MapMarkers(_map.rectTransform, true);

            string Dot(Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>■</color>";
            var legend = UIKit.Text("Legend", body,
                $"{Dot(MapMarkers.Me)} Bạn    {Dot(MapMarkers.Npc)} NPC    {Dot(MapMarkers.Mob)} Quái    {Dot(MapMarkers.Portal)} Cổng    {Dot(MapMarkers.Other)} Người chơi" +
                "    <color=#9aa>Bục cam: nhảy xuyên từ dưới lên, bấm xuống để thả.</color>", 14, TextAlignmentOptions.MidlineLeft);
            legend.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 26), new Vector2(0.5f, 0));

            GameData.OnChanged += k => { if (IsOpen) SetTitle("Bản đồ — " + GameData.MapName); };
        }

        protected override void Refresh()
        {
            SetTitle(string.IsNullOrEmpty(GameData.MapName) ? "Bản đồ" : "Bản đồ — " + GameData.MapName);
            _version = -1;
        }

        private void Update()
        {
            if (MapImage.Texture == null) return;
            if (_version != MapImage.Version)
            {
                _version = MapImage.Version;
                _map.texture = MapImage.Texture;
                _scale = Mathf.Min(MaxScale, MaxW / MapImage.Width);
                var size = new Vector2(MapImage.Width * _scale, MapImage.Height * _scale);
                _map.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
                // Cửa sổ vừa khít ảnh map: + chữ chú thích + tiêu đề
                ((RectTransform)transform).sizeDelta = new Vector2(Mathf.Max(640, size.x + 40), size.y + 110);
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            _markers.Redraw(_scale);
        }
    }
}
