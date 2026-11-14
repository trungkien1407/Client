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
    /// ĐỒ RƠI TRÊN ĐẤT + phím bình máu:
    ///   ITEM_DROP   -> vẽ icon + tên tại chỗ rơi (có chủ / thời gian giữ cho chủ ở đuôi gói)
    ///   ITEM_REMOVE -> xoá
    ///   NHẶT (2026-11-14): CHỌN vật phẩm làm mục tiêu (bấm chuột / Tab) rồi BẤM ĐÁNH (J / Enter / nút Đánh) → tự chạy tới và nhặt
    ///     (ActionInput, loại mục tiêu Item). Không còn tự nhặt khi đi ngang — trừ lúc bật Tự động (AutoPlay) thì vẫn tự nhặt.
    ///   LUẬT (server GroundItem): đồ sống 30 giây; 25 giây đầu chỉ người hạ quái nhặt được (tên trắng với chủ, XÁM với người khác),
    ///     5 giây cuối thành đồ tự do (tên VÀNG) — ai nhặt trước được trước. Server mới là nơi quyết định, client chỉ tô màu cho dễ thấy.
    ///   Phím Q = dùng bình máu, E = bình chakra (QuickPotion chọn bình hợp nhất trong túi)
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
            public int ownerId = -1;     // người hạ quái (-1 = không chủ)
            public float freeAt;         // Time.time lúc thành đồ tự do (server gửi "còn bao nhiêu ms")
            public int shownState = -1;  // màu tên đang vẽ: 0 của mình · 1 khoá (của người khác) · 2 tự do
        }

        /// <summary>Mình nhặt được món này lúc này không (theo dữ liệu server gửi — server vẫn kiểm lại).</summary>
        private static bool CanPick(GroundItem it) => it.ownerId < 0 || it.ownerId == LocalPlayerState.Id || Time.time >= it.freeAt;

        /// <summary>Món {id} mình nhặt được ngay không (ActionInput hỏi trước khi chạy tới).</summary>
        public bool CanPick(int id) => _items.TryGetValue(id, out var it) && CanPick(it);

        /// <summary>Gửi PICK_ITEM (giãn 0,5 giây giữa 2 lần cho cùng 1 món).</summary>
        public void Pick(int id)
        {
            if (!_items.TryGetValue(id, out var it) || Time.time - it.lastPickTry < 0.5f) return;
            it.lastPickTry = Time.time;
            Data.GameActions.PickItem(id);
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
                if (it.go == null || !CanPick(it) || (skip != null && skip(it.id))) continue;   // đồ người khác còn khoá → bỏ
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

            if (AutoPlay.IsOn) AutoPick(local.transform.position);   // tay: chọn vật phẩm + bấm đánh (ActionInput)
            RefreshLabels();

            // Q / E: tự chọn bình hợp nhất trong túi (trước đây chỉ Bình Nhỏ id 1 / 2)
            if (GameInput.Pressed(GameKey.PotionHp)) QuickPotion.Use(true);
            if (GameInput.Pressed(GameKey.PotionMp)) QuickPotion.Use(false);
        }

        private void AutoPick(Vector3 playerPos)
        {
            foreach (var it in _items.Values)
            {
                if (Time.time - it.lastPickTry < PickRetrySeconds || !CanPick(it)) continue;
                if (((Vector2)(it.go.transform.position - playerPos)).sqrMagnitude > PickRadius * PickRadius) continue;

                it.lastPickTry = Time.time;
                Data.GameActions.PickItem(it.id);
            }
        }

        private float _nextLabels;

        /// <summary>Tô lại tên đồ rơi khi quyền nhặt đổi (hết 25 giây → đồ tự do). 4 lần / giây là đủ.</summary>
        private void RefreshLabels()
        {
            if (Time.time < _nextLabels) return;
            _nextLabels = Time.time + 0.25f;
            foreach (var it in _items.Values)
            {
                int state = it.ownerId < 0 || Time.time >= it.freeAt ? 2 : it.ownerId == LocalPlayerState.Id ? 0 : 1;
                if (state == it.shownState || it.go == null) continue;
                it.shownState = state;
                var label = it.go.transform.Find("Label")?.GetComponent<TextMeshPro>();
                if (label != null) label.color = state == 0 ? Color.white : state == 1 ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.85f, 0.3f);
            }
        }

        /// <summary>ITEM_DROP: int groundId, int templateId, int qty, float x, float y, đuôi: int ownerId, int ms giữ cho chủ, int ms còn sống</summary>
        private void OnItemDrop(byte[] data)
        {
            var r = new MessageReader(data);
            var it = new GroundItem { id = r.ReadInt(), templateId = r.ReadInt(), qty = r.ReadInt() };
            float x = r.ReadFloat();
            float y = r.ReadFloat();
            if (r.Available() >= 12)   // server cũ không gửi đuôi → coi như đồ tự do
            {
                it.ownerId = r.ReadInt();
                it.freeAt = Time.time + r.ReadInt() / 1000f;
                r.ReadInt();   // ms còn sống (chưa dùng — server tự gửi ITEM_REMOVE khi hết)
            }
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
            AddTarget(it);
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
            Assets.Script.UI.Kit.UIStyle.Readable(tmp);   // viền + bóng đổ → đọc được cả trên nền trắng
            tmp.sortingOrder = 6;
            return go;
        }

        // ---------------------------------------------------------------- chọn làm mục tiêu
        private static int _targetLayer = -1;

        /// <summary>
        /// Cho đồ rơi CHỌN ĐƯỢC như quái: khung va chạm "trigger" (không cản ai) nằm trên layer mà PlayerTargeting quét
        /// (click chuột = Raycast, Tab = OverlapCircle chỉ bắt layer trong targetableLayer) + thành phần ITargetable.
        /// </summary>
        private static void AddTarget(GroundItem it)
        {
            if (_targetLayer < 0)
            {
                var targeting = FindAnyObjectByType<PlayerTargeting>();
                int mask = targeting != null ? targeting.targetableLayer.value : 0;
                for (int i = 0; i < 32 && _targetLayer < 0; i++) if ((mask & (1 << i)) != 0) _targetLayer = i;
                if (_targetLayer < 0) return;   // chưa có PlayerTargeting (chưa vào game) → lần sau thử lại
            }
            it.go.layer = _targetLayer;
            var box = it.go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.1f, 1.3f);
            box.offset = new Vector2(0f, 0.55f);
            var t = it.go.AddComponent<GroundItemTarget>();
            t.Init(it.id, it.go.transform.Find("Label")?.GetComponent<TextMeshPro>());
        }

        /// <summary>Đồ rơi làm mục tiêu: tên lên thanh mục tiêu, đang chọn thì tên xanh lá + to hơn chút.</summary>
        private class GroundItemTarget : MonoBehaviour, Interfaces.ITargetable
        {
            private int _id;
            private TextMeshPro _label;
            private Color _before;

            public void Init(int id, TextMeshPro label) { _id = id; _label = label; }
            public int GetId() => _id;
            public string GetTargetName() => _label != null ? _label.text : "Vật phẩm";
            public Interfaces.TargetType GetTargetType() => Interfaces.TargetType.Item;
            public Transform GetTransform() => transform;

            public void OnTargeted()
            {
                if (_label == null) return;
                _before = _label.color;
                _label.color = new Color(0.45f, 1f, 0.45f);
                transform.localScale = Vector3.one * 1.15f;
            }

            public void OnDeselected()
            {
                transform.localScale = Vector3.one;
                if (_label != null) _label.color = _before;
            }
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
