using System.Collections.Generic;
using Assets.Script.Core;
using Assets.Script.Data;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// PHÁT HIỆU ỨNG THEO DỮ LIỆU (học từ NSO / G4M gameeff): hiệu ứng = danh sách khung [ảnh kho số, dx, dy] do server gửi
    /// (GAME_DATA_EFFECTS, sửa ở data/effects*.json bên server) → 1 component phát chung, không cần prefab từng chiêu.
    ///   EffectPlayer.Play(id, vịTrí, lật)        — gốc = chân / tâm mục tiêu
    /// Ảnh chưa tải xong thì khung đó trống (lần sau đã có sẵn). Đối tượng dùng lại qua hàng chờ.
    /// </summary>
    public class EffectPlayer : MonoBehaviour
    {
        public const string SortingLayer = "Default";
        public const int SortingOrder = 60;   // trên nhân vật / quái

        private static readonly Stack<EffectPlayer> Pool = new Stack<EffectPlayer>();
        private static Transform _root;

        private PartRenderer _parts;
        private EffectTpl _tpl;
        private bool _flip;
        private int _frame;
        private float _next;
        private Transform _follow;
        private Vector3 _offset;
        private readonly Models.PartFrame _one = new Models.PartFrame { parts = new Models.FramePart[1] };

        /// <summary>Diễn hiệu ứng {id} tại {pos}. {follow} khác null → bám theo đối tượng đó (pos tính tương đối).</summary>
        public static void Play(int id, Vector3 pos, bool flip = false, Transform follow = null)
        {
            if (id < 0 || !GameData.Effects.TryGetValue(id, out var tpl) || tpl.frames == null || tpl.frames.Length == 0) return;
            foreach (var f in tpl.frames) ImageBank.Preload(f.img);
            var p = Take();
            p._tpl = tpl;
            p._flip = flip;
            p._follow = follow;
            p._offset = follow != null ? pos : Vector3.zero;
            p.transform.position = follow != null ? follow.position + pos : pos;
            p._frame = -1;
            p._next = 0f;
            p.gameObject.SetActive(true);
        }

        private static EffectPlayer Take()
        {
            while (Pool.Count > 0)
            {
                var e = Pool.Pop();
                if (e != null) return e;
            }
            if (_root == null)
            {
                _root = new GameObject("Effects").transform;
                DontDestroyOnLoad(_root.gameObject);
            }
            var go = new GameObject("Fx");
            go.transform.SetParent(_root, false);
            var fx = go.AddComponent<EffectPlayer>();
            fx._parts = go.AddComponent<PartRenderer>();
            fx._parts.SetSorting(SortingLayer, SortingOrder);
            return fx;
        }

        private void Update()
        {
            if (_follow != null) transform.position = _follow.position + _offset;
            if (Time.time < _next) return;
            _frame++;
            if (_frame >= _tpl.frames.Length) { Recycle(); return; }
            _next = Time.time + Mathf.Max(_tpl.frameMs, 16) / 1000f;
            _one.parts[0] = _tpl.frames[_frame];
            _parts.Show(_one, ImageBank.Cached, _flip);
        }

        private void Recycle()
        {
            _parts.Hide();
            _follow = null;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
