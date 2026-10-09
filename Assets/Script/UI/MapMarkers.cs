using System.Collections.Generic;
using Assets.Script.Entities;
using Assets.Script.Map;
using Assets.Script.Manager;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// Vẽ chấm trên ảnh map (minimap + bản đồ lớn): bạn (vàng), người khác (trắng), NPC (xanh lá), quái (đỏ), cổng (xanh lơ).
    /// Chấm là con của 1 lớp phủ đúng kích thước ảnh map; vị trí = toạ độ world × scale (px mỗi ô).
    /// </summary>
    public class MapMarkers
    {
        public static readonly Color Me = new Color(1f, 0.9f, 0.2f);
        public static readonly Color Other = Color.white;
        public static readonly Color Npc = new Color(0.4f, 1f, 0.4f);
        public static readonly Color Mob = new Color(1f, 0.35f, 0.3f);
        public static readonly Color Portal = new Color(0.4f, 1f, 1f);

        private readonly RectTransform _layer;
        private readonly bool _labels;
        private readonly List<Image> _dots = new List<Image>();
        private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
        private int _dot, _text;
        private float _scale;

        public MapMarkers(RectTransform layer, bool labels) { _layer = layer; _labels = labels; }

        /// <summary>Vẽ lại toàn bộ chấm. Trả về vị trí của bạn (world) để minimap cuộn theo.</summary>
        public Vector2 Redraw(float scale)
        {
            _scale = scale; _dot = 0; _text = 0;
            var me = Vector2.zero;

            foreach (var p in PortalMarkers.Current) Put(p.pos + new Vector2(0, 1f), Portal, 7, p.target);
            foreach (var m in Object.FindObjectsOfType<MobController>())
                if (!m.IsDead) Put(m.transform.position, Mob, 4, null);
            foreach (var n in Object.FindObjectsOfType<NpcEntity>()) Put(n.transform.position, Npc, 6, n.GetTargetName());
            foreach (var r in Object.FindObjectsOfType<RemotePlayer>()) Put(r.transform.position, Other, 5, null);
            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local != null) { me = local.transform.position; Put(me, Me, 7, "Bạn"); }

            for (int i = _dot; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
            for (int i = _text; i < _texts.Count; i++) _texts[i].gameObject.SetActive(false);
            return me;
        }

        private void Put(Vector2 world, Color color, float size, string label)
        {
            var pos = new Vector2(world.x * _scale, (world.y + 0.8f) * _scale);   // chấm ở ngang thân, không ở chân
            if (_dot == _dots.Count)
            {
                var img = UIKit.Panel("Dot", _layer, color);
                img.raycastTarget = false;
                _dots.Add(img);
            }
            var d = _dots[_dot++];
            d.gameObject.SetActive(true);
            d.color = color;
            d.rectTransform.Place(Vector2.zero, Vector2.zero, pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
            d.transform.SetAsLastSibling();

            if (!_labels || string.IsNullOrEmpty(label)) return;
            if (_text == _texts.Count)
            {
                var t = UIKit.Text("Label", _layer, "", 13, TextAlignmentOptions.Bottom);
                t.raycastTarget = false;
                t.outlineWidth = 0.25f;
                t.outlineColor = Color.black;
                _texts.Add(t);
            }
            var tx = _texts[_text++];
            tx.gameObject.SetActive(true);
            tx.text = label;
            tx.color = color;
            // Sát mép map thì canh chữ vào trong để không bị cắt
            float w = _layer.rect.width, px = pos.x < 80 ? 0f : pos.x > w - 80 ? 1f : 0.5f;
            tx.alignment = px == 0f ? TextAlignmentOptions.BottomLeft : px == 1f ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Bottom;
            tx.rectTransform.Place(Vector2.zero, Vector2.zero, pos + new Vector2(0, size), new Vector2(160, 20), new Vector2(px, 0));
        }
    }
}
