using Assets.Script.UI.Kit;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// MŨI TÊN NHỎ chỉ xuống (2026-11-15) trên đầu đồ rơi / NPC: tam giác vàng có viền tối, nhún lên xuống nhẹ.
    /// Hình vẽ bằng code (UIIcons.Play xoay 90° → chỉ xuống), không cần file ảnh. Đặt bằng Attach(); ẩn / hiện bằng gameObject.SetActive.
    /// </summary>
    public class PointerArrow : MonoBehaviour
    {
        private const float Amp = 0.08f, Speed = 4f;
        private Vector3 _base;
        private float _phase;

        /// <summary>Gắn mũi tên vào {parent} tại {localPos} (đơn vị thế giới), cạnh {size}. Trả về chính mũi tên.</summary>
        public static PointerArrow Attach(Transform parent, Vector3 localPos, float size, int sortingOrder, Color? color = null)
        {
            var go = new GameObject("PointerArrow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var a = go.AddComponent<PointerArrow>();
            a._base = localPos;
            a._phase = Random.value * 6.28f;
            float k = size / 0.64f;   // ảnh icon 64 px / 100 px mỗi đơn vị = 0,64 đơn vị
            // viền tối phía sau (to hơn 1 chút) → nhìn rõ trên nền sáng
            Make(go.transform, "Edge", k * 1.35f, new Color(0.15f, 0.08f, 0.02f, 0.9f), sortingOrder);
            Make(go.transform, "Fill", k, color ?? new Color(1f, 0.82f, 0.25f), sortingOrder + 1);
            return a;
        }

        private static void Make(Transform parent, string name, float scale, Color c, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localRotation = Quaternion.Euler(0, 0, -90f);   // tam giác chỉ phải → chỉ xuống
            g.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = UIIcons.Play;
            sr.color = c;
            sr.sortingOrder = order;
        }

        /// <summary>Đổi chỗ đứng gốc (vd NPC đổi hình → đầu cao / thấp khác).</summary>
        public void SetBase(Vector3 localPos) { _base = localPos; transform.localPosition = localPos; }

        private void Update()
        {
            transform.localPosition = _base + new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * Speed + _phase)) * Amp, 0f);
        }
    }
}
