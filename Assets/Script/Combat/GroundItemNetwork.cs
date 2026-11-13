using Assets.Script.Core;
using System.Collections.Generic;
using System.Text;
using Assets.Script.Constants;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.Combat
{
    /// <summary>
    /// ĐỒ RƠI + TÚI ĐỒ (bản tối giản để chơi được):
    ///   ITEM_DROP   -> vẽ 1 ô vuông nhỏ + nhãn tại chỗ rơi
    ///   ITEM_REMOVE -> xoá
    ///   Tự nhặt     : đứng gần (<= 1.2 đơn vị) -> gửi PICK_ITEM (server kiểm tầm 3.0 và quyền nhặt)
    ///   Phím Q = dùng bình máu (item 1), E = bình chakra (item 2) — số lượng đọc từ GameData.Inventory
    /// </summary>
    public class GroundItemNetwork : NetworkListener
    {
        private const float PickRadius = 1.2f;
        private const float PickRetrySeconds = 1.0f;

        private class GroundItem
        {
            public int id, templateId, qty;
            public float dropX, dropY;   // chỗ rơi server gửi (hình có thể dời sang bên cho khỏi chồng)
            public GameObject go;
            public float lastPickTry = -10f;
        }

        private readonly Dictionary<int, GroundItem> _items = new Dictionary<int, GroundItem>();
        private static Sprite _squareSprite;

        public static GroundItemNetwork Instance { get; private set; }

        private void Awake() { Instance = this; Data.GameData.OnSessionReset += ClearAll; }

        /// <summary>
        /// Món đồ trên đất gần {from} nhất trong {radius} ô (bỏ món {skip} trả true) — AutoPlay đi tới, tới sát thì AutoPick tự nhặt.
        /// </summary>
        /// <summary>Món {id} còn nằm trên đất không.</summary>
        public bool Has(int id) => _items.ContainsKey(id);

        public bool TryNearest(Vector3 from, float radius, System.Predicate<int> skip, out int id, out Vector3 pos)
        {
            id = -1; pos = Vector3.zero;
            float best = radius * radius;
            foreach (var it in _items.Values)
            {
                if (it.go == null || (skip != null && skip(it.id))) continue;
                float sqr = ((Vector2)(it.go.transform.position - from)).sqrMagnitude;
                if (sqr >= best) continue;
                best = sqr; id = it.id; pos = it.go.transform.position;
            }
            return id >= 0;
        }
        protected override void OnDestroy() { base.OnDestroy(); Data.GameData.OnSessionReset -= ClearAll; }

        protected override void RegisterHandlers()
        {
            Listen(Cmd.ITEM_DROP, OnItemDrop);
            Listen(Cmd.ITEM_REMOVE, OnItemRemove);
            Listen(Cmd.CHANGE_MAP, OnChangeMapOrZone);
            Listen(Cmd.CHANGE_ZONE, OnChangeMapOrZone);
        }

        protected override void Update()
        {
            base.Update();

            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local == null || LocalPlayerState.IsDead) return;

            AutoPick(local.transform.position);

            // Q / E: tự chọn bình hợp nhất trong túi (trước đây chỉ Bình Nhỏ id 1 / 2)
            if (GameInput.Pressed(GameKey.PotionHp)) QuickPotion.Use(true);
            if (GameInput.Pressed(GameKey.PotionMp)) QuickPotion.Use(false);
        }

        private void AutoPick(Vector3 playerPos)
        {
            foreach (var it in _items.Values)
            {
                if (Time.time - it.lastPickTry < PickRetrySeconds) continue;
                if (((Vector2)(it.go.transform.position - playerPos)).sqrMagnitude > PickRadius * PickRadius) continue;

                it.lastPickTry = Time.time;
                Data.GameActions.PickItem(it.id);
            }
        }

        /// <summary>ITEM_DROP: int groundId, int templateId, int qty, float x, float y</summary>
        private void OnItemDrop(byte[] data)
        {
            var r = new MessageReader(data);
            var it = new GroundItem { id = r.ReadInt(), templateId = r.ReadInt(), qty = r.ReadInt() };
            float x = r.ReadFloat();
            float y = r.ReadFloat();
            r.Cleanup();

            if (_items.ContainsKey(it.id)) return;
            // Nhiều món rơi cùng chỗ (quái chết rơi 2–3 món) → dàn sang 2 bên 0,9 ô / món, tên so le cao thấp → không chồng chữ.
            // Chỉ dời HÌNH; nhặt vẫn theo vị trí server (tầm nhặt server 3 ô, rộng hơn độ dời).
            int near = 0;
            foreach (var other in _items.Values)
                if (other.go != null && Mathf.Abs(other.dropX - x) < 0.6f && Mathf.Abs(other.dropY - y) < 0.6f) near++;
            float offset = near == 0 ? 0f : (near % 2 == 1 ? 1f : -1f) * ((near + 1) / 2) * 0.9f;
            it.dropX = x; it.dropY = y;
            it.go = CreateVisual(it, new Vector3(x + offset, y, 0f));
            if (near % 2 == 1) { var lb = it.go.transform.Find("Label"); if (lb != null) lb.localPosition += new Vector3(0f, 0.35f, 0f); }
            _items[it.id] = it;
        }

        /// <summary>ITEM_REMOVE: int groundId (đã bị nhặt hoặc hết 60s)</summary>
        private void OnItemRemove(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            r.Cleanup();
            if (_items.TryGetValue(id, out var it))
            {
                if (it.go != null) Destroy(it.go);
                _items.Remove(id);
            }
        }

        /// <summary>Đổi map/khu: đồ rơi của khu cũ không còn -> xoá hết (khu mới sẽ gửi lại ITEM_DROP).</summary>
        private void OnChangeMapOrZone(byte[] _) => ClearAll();

        private void ClearAll()
        {
            foreach (var it in _items.Values) if (it.go != null) Destroy(it.go);
            _items.Clear();
        }

        private const float IconSize = 0.75f;   // cạnh dài của icon trên mặt đất (đơn vị thế giới)
        private static Sprite _shadowSprite;

        /// <summary>
        /// Hình đồ rơi: ICON THẬT của vật phẩm (kho ảnh img{iconId}, như trong túi đồ) + bóng đổ dưới chân + tên có viền.
        /// Vừa rơi ra thì nảy lên rồi lơ lửng nhẹ (ItemBob). Ảnh chưa tải xong / chưa có → ô vuông màu như cũ.
        /// </summary>
        private static GameObject CreateVisual(GroundItem it, Vector3 pos)
        {
            if (_squareSprite == null) _squareSprite = SolidSprite(8, false);
            if (_shadowSprite == null) _shadowSprite = SolidSprite(32, true);

            var go = new GameObject($"GroundItem_{it.id}");
            go.transform.position = pos;

            // bóng đổ hình bầu dục dưới chân → món đồ "nằm" trên mặt đất
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localScale = new Vector3(0.7f, 0.2f, 1f);
            var ssr = shadow.AddComponent<SpriteRenderer>();
            ssr.sprite = _shadowSprite;
            ssr.color = new Color(0f, 0f, 0f, 0.35f);
            ssr.sortingOrder = 4;

            var icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform, false);
            icon.transform.localPosition = new Vector3(0f, IconSize * 0.5f + 0.05f, 0f);
            icon.transform.localScale = Vector3.one * (IconSize / 0.4f) * 0.6f;   // ô vuông tạm (0,4 đơn vị) nhỏ hơn icon thật
            var sr = icon.AddComponent<SpriteRenderer>();
            sr.sprite = _squareSprite;
            sr.color = it.templateId == ItemIds.HpPotion ? new Color(1f, 0.3f, 0.3f)
                     : it.templateId == ItemIds.MpPotion ? new Color(0.3f, 0.5f, 1f)
                     : new Color(1f, 0.85f, 0.3f);
            sr.sortingOrder = 5;
            icon.AddComponent<ItemBob>();
            int iconId = Assets.Script.Data.GameData.Items.TryGetValue(it.templateId, out var tpl) ? tpl.iconId : 0;
            if (iconId > 0)
                ImageBank.Get(iconId, sprite =>
                {
                    if (sr == null || sprite == null) return;
                    sr.sprite = sprite;
                    sr.color = Color.white;
                    float big = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                    if (big > 0.0001f) icon.transform.localScale = Vector3.one * (IconSize / big);
                });

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, IconSize + 0.45f, 0f);
            var tmp = label.AddComponent<TextMeshPro>();
            string nm = Assets.Script.Data.GameData.ItemName(it.templateId);
            tmp.text = it.qty > 1 ? $"{nm} x{it.qty}" : nm;
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
            tmp.sortingOrder = 6;
            return go;
        }

        /// <summary>Ảnh trắng vuông (8 px) hoặc tròn mờ mép (bóng đổ), gốc ở giữa.</summary>
        private static Sprite SolidSprite(int size, bool round)
        {
            var tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    px[y * size + x] = new Color(1, 1, 1, round ? Mathf.Clamp01(1f - d * d) : 1f);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 0.4f);   // 0,4 đơn vị
        }

        /// <summary>Đồ rơi: nảy ra (to dần + bật lên) trong 0,25 giây rồi lơ lửng lên xuống nhẹ — dễ thấy trên đất.</summary>
        private class ItemBob : MonoBehaviour
        {
            private float _baseY, _phase, _born;

            private void Start()
            {
                _baseY = transform.localPosition.y;
                _phase = Random.value * 6.28f;
                _born = Time.time;
            }

            private void Update()
            {
                float t = Mathf.Clamp01((Time.time - _born) / 0.25f);
                float hop = Mathf.Sin(t * Mathf.PI) * 0.45f;                       // bật lên rồi rơi xuống chỗ nằm
                var p = transform.localPosition;
                p.y = _baseY + hop + (t >= 1f ? Mathf.Sin((Time.time + _phase) * 3f) * 0.06f : 0f);
                transform.localPosition = p;
            }
        }
    }
}
