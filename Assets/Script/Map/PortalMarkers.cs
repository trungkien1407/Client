using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>
    /// CHỮ CHỈ ĐƯỜNG Ở CỔNG — từ MAP_INFO (vị trí cổng + tên map đích), nhấp nhô nhẹ. Minimap đọc Current.
    /// [CẦN ĐIỀN khi có art] thêm hình cổng.
    /// </summary>
    public static class PortalMarkers
    {
        /// <summary>1 cổng: vị trí chân, tên map đích, kiểu (0 mép trái, 1 mép phải, 2 cổng giữa map).</summary>
        public struct Info { public Vector2 pos; public string target; public int side; }

        private static readonly List<GameObject> _markers = new List<GameObject>();
        public static readonly List<Info> Current = new List<Info>();

        public static void Show(List<Info> portals)
        {
            Clear();
            Current.AddRange(portals);
            foreach (var p in portals)
            {
                var go = new GameObject("PortalMarker");
                go.transform.position = new Vector3(p.pos.x, p.pos.y + 2.3f, 0f);
                var t = go.AddComponent<TextMeshPro>();
                t.text = p.side == 0 ? $"<< {p.target}" : p.side == 1 ? $"{p.target} >>" : $"[ {p.target} ]\n<size=2.6>đứng vào để đi</size>";
                t.fontSize = 3.2f;
                t.alignment = TextAlignmentOptions.Center;
                t.color = new Color(0.55f, 1f, 1f);
                t.outlineWidth = 0.25f;
                t.outlineColor = Color.black;
                t.sortingOrder = 30;
                t.rectTransform.sizeDelta = new Vector2(8f, 2f);
                go.AddComponent<Bob>();
                _markers.Add(go);
            }
        }

        public static void Clear()
        {
            foreach (var m in _markers) if (m != null) Object.Destroy(m);
            _markers.Clear();
            Current.Clear();
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
