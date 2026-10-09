using System;
using System.Collections.Generic;
using Assets.Script.Models;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// VẼ 1 KHUNG GHÉP MẢNH: mỗi mảnh = 1 SpriteRenderer con (tạo thêm khi cần, thừa thì ẩn).
    /// Dùng chung cho quái (MobAnimSO) và hiệu ứng (EffectPlayer). Lật trái/phải: đổi dấu x + flipX từng mảnh.
    /// </summary>
    public class PartRenderer : MonoBehaviour
    {
        private readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>();
        private Color _color = Color.white;
        private string _layer;
        private int _order;

        public void SetSorting(string layer, int order) { _layer = layer; _order = order; foreach (var r in _parts) Apply(r, _parts.IndexOf(r)); }

        public void SetColor(Color c) { _color = c; foreach (var r in _parts) r.color = c; }

        /// <summary>Vẽ {frame}; {sprite} đổi số ảnh của mảnh → Sprite (null = bỏ mảnh đó).</summary>
        public void Show(PartFrame frame, Func<int, Sprite> sprite, bool flip)
        {
            int n = frame?.parts?.Length ?? 0;
            for (int i = 0; i < n; i++)
            {
                var p = frame.parts[i];
                var r = Part(i);
                r.sprite = sprite(p.img);
                r.flipX = flip;
                float x = ArtUnits.ToUnits(p.dx);
                r.transform.localPosition = new Vector3(flip ? -x : x, -ArtUnits.ToUnits(p.dy), 0f);
                r.enabled = r.sprite != null;
            }
            for (int i = n; i < _parts.Count; i++) _parts[i].enabled = false;
        }

        public void Hide() { foreach (var r in _parts) r.enabled = false; }

        private SpriteRenderer Part(int i)
        {
            while (_parts.Count <= i)
            {
                var go = new GameObject("Part" + _parts.Count);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                _parts.Add(r);
                Apply(r, _parts.Count - 1);
            }
            return _parts[i];
        }

        private void Apply(SpriteRenderer r, int i)
        {
            if (!string.IsNullOrEmpty(_layer)) r.sortingLayerName = _layer;
            r.sortingOrder = _order + i;   // mảnh sau đè mảnh trước (đúng thứ tự trong dữ liệu)
            r.color = _color;
        }
    }
}
