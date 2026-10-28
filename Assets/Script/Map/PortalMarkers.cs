using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>
    /// MỐC CHỈ ĐƯỜNG Ở CỔNG — mũi tên vàng (chỉ hướng cổng) + tên map đích, từ MAP_INFO, nhấp nhô nhẹ. Minimap đọc Current.
    /// [CẦN ĐIỀN khi có art] thêm hình cổng.
    /// </summary>
    public static class PortalMarkers
    {
        /// <summary>1 cổng: vị trí chân, tên map đích, kiểu (0 mép trái, 1 mép phải, 2 cổng giữa map).</summary>
        public struct Info { public Vector2 pos; public string target; public int side; }

        private static readonly List<GameObject> _markers = new List<GameObject>();
        public static readonly List<Info> Current = new List<Info>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            Data.GameData.OnSessionReset += Clear;
            MapManager.OnMapLoaded += OnMapLoaded;
        }

        // Biên ngang của map hiện tại (CameraBounds của prefab map) — giữ chữ tên map không lòi ra ngoài
        private static bool _hasBounds;
        private static float _minX, _maxX;

        private static void OnMapLoaded(GameObject map)
        {
            _hasBounds = false;
            if (map == null) return;
            var b = map.transform.Find("CameraBounds");
            var col = b != null ? b.GetComponent<Collider2D>() : null;
            if (col == null) return;
            _hasBounds = true;
            _minX = col.bounds.min.x;
            _maxX = col.bounds.max.x;
        }

        public static void Show(List<Info> portals)
        {
            Clear();
            Current.AddRange(portals);
            foreach (var p in portals)
            {
                // Cổng mép trái / phải nằm sát mép map: dời mốc vào trong 0,9 ô để mũi tên + chữ không bị cắt ở mép màn hình
                float inward = p.side == 0 ? 0.9f : p.side == 1 ? -0.9f : 0f;
                var go = new GameObject("PortalMarker");
                go.transform.position = new Vector3(p.pos.x + inward, p.pos.y + 2.2f, 0f);

                // Mũi tên chỉ hướng cổng: trái / phải; cổng giữa map → chỉ xuống chỗ đứng
                float angle = p.side == 0 ? 180f : p.side == 1 ? 0f : -90f;
                AddArrow(go.transform, "ArrowShadow", 1.18f, new Color(0f, 0f, 0f, 0.75f), angle, 30);
                AddArrow(go.transform, "Arrow", 1f, new Color(1f, 0.86f, 0.2f), angle, 31);

                // Tên map đích: nằm về phía TRONG map (trái → chữ bên phải mũi tên; phải → bên trái), không canh giữa ở mép
                var label = new GameObject("Name");
                label.transform.SetParent(go.transform, false);
                var t = label.AddComponent<TextMeshPro>();
                t.text = p.side == 2 ? $"{p.target}\n<size=2.2>đứng vào để đi</size>" : p.target;
                t.fontSize = 2.8f;
                t.color = Color.white;
                t.outlineWidth = 0.25f;
                t.outlineColor = Color.black;
                t.sortingOrder = 32;
                t.enableWordWrapping = false;
                var rt = t.rectTransform;
                rt.sizeDelta = new Vector2(6f, 1.2f);
                if (p.side == 0) { t.alignment = TextAlignmentOptions.MidlineLeft; rt.pivot = new Vector2(0f, 0.5f); label.transform.localPosition = new Vector3(0.65f, 0f, 0f); }
                else if (p.side == 1) { t.alignment = TextAlignmentOptions.MidlineRight; rt.pivot = new Vector2(1f, 0.5f); label.transform.localPosition = new Vector3(-0.65f, 0f, 0f); }
                else { t.alignment = TextAlignmentOptions.Bottom; rt.pivot = new Vector2(0.5f, 0f); label.transform.localPosition = new Vector3(0f, 0.6f, 0f); }
                label.AddComponent<KeepInside>();   // cổng sát mép (vd "Ảnh Sát Đường" ở mép trái) → chữ dời vào trong

                go.AddComponent<Bob>();
                _markers.Add(go);
            }
        }

        /// <summary>Mũi tên (hình vẽ bằng code FxShapes.Arrow) — lớp đen to hơn phía sau làm viền.</summary>
        private static void AddArrow(Transform parent, string name, float scale, Color color, float angle, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(scale, scale * 0.9f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Fx.FxShapes.Arrow;
            sr.color = color;
            sr.sortingOrder = order;
        }

        public static void Clear()
        {
            foreach (var m in _markers) if (m != null) Object.Destroy(m);
            _markers.Clear();
            Current.Clear();
        }

        /// <summary>Dời chữ ngang cho phần chữ thật (textBounds) nằm trong biên map, cách mép 0,2 ô.</summary>
        private class KeepInside : MonoBehaviour
        {
            private TextMeshPro _t;
            private float _baseX;
            private void Start() { _t = GetComponent<TextMeshPro>(); _baseX = transform.localPosition.x; }
            private void LateUpdate()
            {
                if (!_hasBounds || _t == null) return;
                var tb = _t.textBounds;
                if (tb.size.x <= 0f) return;
                float origin = transform.parent.position.x + _baseX;
                float left = origin + tb.min.x, right = origin + tb.max.x, shift = 0f;
                if (left < _minX + 0.2f) shift = _minX + 0.2f - left;
                else if (right > _maxX - 0.2f) shift = _maxX - 0.2f - right;
                var p = transform.localPosition;
                transform.localPosition = new Vector3(_baseX + shift, p.y, p.z);
            }
        }

        /// <summary>Nhấp nhô lên xuống cho dễ thấy.</summary>
        private class Bob : MonoBehaviour
        {
            private float _y0;
            private void Start() => _y0 = transform.position.y;
            private void Update()
            {
                var p = transform.position;
                p.y = _y0 + Mathf.Sin(Time.time * 2.5f) * 0.12f;
                transform.position = p;
            }
        }
    }
}
