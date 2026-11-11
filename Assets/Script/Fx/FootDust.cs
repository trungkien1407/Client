using System.Collections.Generic;
using Assets.Script.Core;
using UnityEngine;

namespace Assets.Script.Fx
{
    /// <summary>
    /// BỤI CHÂN: đám bụi nhỏ dưới chân lúc BẮT ĐẦU CHẠY (bụi phía sau lưng) và lúc TIẾP ĐẤT (bụi hai bên).
    /// PlayerVisualController gọi OnAnim mỗi khi đổi anim di chuyển — dùng chung cho mình và người khác.
    ///
    /// Hình: 5 khung "dust_0" … "dust_4" (Addressables) hoặc Resources/FootDust/dust_N.png, pivot giữa-đáy, 96 px = 1 ô.
    /// Demo nội bộ: Tools → Naruto → Demo art (06_HieuUng/BuiChan_DuongDi). Thiếu hình → không hiện gì, game vẫn chạy.
    /// </summary>
    public sealed class FootDust : MonoBehaviour
    {
        public const int Frames = 5;
        private const float FrameTime = 0.06f;   // 5 khung ≈ 0,3 giây

        private static readonly Sprite[] Sprites = new Sprite[Frames];
        private static bool _requested;
        private static readonly Stack<FootDust> Pool = new Stack<FootDust>();
        private static Transform _root;

        /// <summary>Số đám bụi đã tung từ lúc mở game (autotest kiểm).</summary>
        public static int Spawned { get; private set; }

        private SpriteRenderer _sr;
        private int _frame;
        private float _next;

        /// <summary>Anim đổi từ {from} sang {to}: chạy từ chỗ đứng / tiếp đất → tung bụi.</summary>
        public static void OnAnim(Transform feet, string from, string to, bool facingLeft, Renderer sortLike)
        {
            if (feet == null || !feet.gameObject.activeInHierarchy) return;
            bool wasAir = from == Player.AnimNames.Fall || from == Player.AnimNames.FallDown || from == Player.AnimNames.Jump;
            bool onGround = to == Player.AnimNames.Idle || to == Player.AnimNames.Run;
            if (wasAir && onGround)
            {
                Spawn(feet.position + new Vector3(-0.25f, 0f), true, sortLike);    // tiếp đất: 2 đám toả ra 2 bên
                Spawn(feet.position + new Vector3(0.25f, 0f), false, sortLike);
            }
            else if (to == Player.AnimNames.Run && from == Player.AnimNames.Idle)
            {
                // Bụi bay về phía sau lưng: quay trái → bụi bên phải
                Spawn(feet.position + new Vector3(facingLeft ? 0.3f : -0.3f, 0f), !facingLeft, sortLike);
            }
        }

        private static void Spawn(Vector3 pos, bool flip, Renderer sortLike)
        {
            Request();
            if (Sprites[0] == null) return;   // chưa tải / không có hình
            FootDust d = null;
            while (Pool.Count > 0 && d == null) d = Pool.Pop();
            if (d == null)
            {
                if (_root == null) { var r = new GameObject("[FootDust]"); DontDestroyOnLoad(r); _root = r.transform; }
                var go = new GameObject("Dust");
                go.transform.SetParent(_root, false);
                d = go.AddComponent<FootDust>();
                d._sr = go.AddComponent<SpriteRenderer>();
            }
            d.transform.position = pos;
            d._sr.flipX = flip;
            if (sortLike != null) { d._sr.sortingLayerID = sortLike.sortingLayerID; d._sr.sortingOrder = sortLike.sortingOrder + 1; }
            d._frame = 0;
            d._sr.sprite = Sprites[0];
            d._next = Time.time + FrameTime;
            d.gameObject.SetActive(true);
            Spawned++;
        }

        /// <summary>Tải 5 khung 1 lần (lần đầu gọi có thể chưa kịp có hình — lần sau đã sẵn).</summary>
        private static void Request()
        {
            if (_requested) return;
            _requested = true;
            for (int i = 0; i < Frames; i++)
            {
                int k = i;
                AssetSource.Load<Sprite>("dust_" + k, "FootDust/dust_" + k, s => Sprites[k] = s);
            }
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + FrameTime;
            if (++_frame >= Frames || Sprites[_frame] == null)
            {
                gameObject.SetActive(false);
                Pool.Push(this);
                return;
            }
            _sr.sprite = Sprites[_frame];
        }
    }
}
