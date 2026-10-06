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
    ///   INVENTORY   -> lưu lại; phím Q = dùng bình máu (item 1), E = bình chakra (item 2)
    ///   Góc phải trên: Level / EXP / Yen / túi đồ (IMGUI, chưa có UI thật).
    /// </summary>
    public class GroundItemNetwork : MonoBehaviour
    {
        private const float PickRadius = 1.2f;
        private const float PickRetrySeconds = 1.0f;
        private const int HpPotionId = 1;
        private const int MpPotionId = 2;

        private class GroundItem
        {
            public int id, templateId, qty;
            public GameObject go;
            public float lastPickTry = -10f;
        }

        private readonly Dictionary<int, GroundItem> _items = new Dictionary<int, GroundItem>();
        // templateId -> số lượng (theo gói INVENTORY mới nhất)
        private readonly SortedDictionary<int, int> _inventory = new SortedDictionary<int, int>();
        private bool _registered;
        private static Sprite _squareSprite;
        private GUIStyle _style;

        private void Update()
        {
            if (!_registered && NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.AddHandler(Cmd.ITEM_DROP, OnItemDrop);
                NetworkEventDispatcher.Instance.AddHandler(Cmd.ITEM_REMOVE, OnItemRemove);
                NetworkEventDispatcher.Instance.AddHandler(Cmd.INVENTORY, OnInventory);
                NetworkEventDispatcher.Instance.AddHandler(Cmd.CHANGE_MAP, OnChangeMapOrZone);
                NetworkEventDispatcher.Instance.AddHandler(Cmd.CHANGE_ZONE, OnChangeMapOrZone);
                _registered = true;
            }

            var local = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (local == null || LocalPlayerState.IsDead) return;

            AutoPick(local.transform.position);

            var kb = Keyboard.current;
            if (kb == null || ChatBox.IsTyping) return;
            if (kb.qKey.wasPressedThisFrame) UseItem(HpPotionId);
            if (kb.eKey.wasPressedThisFrame) UseItem(MpPotionId);
        }

        private void OnDestroy()
        {
            if (!_registered || NetworkEventDispatcher.Instance == null) return;
            NetworkEventDispatcher.Instance.RemoveHandler(Cmd.ITEM_DROP, OnItemDrop);
            NetworkEventDispatcher.Instance.RemoveHandler(Cmd.ITEM_REMOVE, OnItemRemove);
            NetworkEventDispatcher.Instance.RemoveHandler(Cmd.INVENTORY, OnInventory);
            NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CHANGE_MAP, OnChangeMapOrZone);
            NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CHANGE_ZONE, OnChangeMapOrZone);
        }

        private void AutoPick(Vector3 playerPos)
        {
            foreach (var it in _items.Values)
            {
                if (Time.time - it.lastPickTry < PickRetrySeconds) continue;
                if (((Vector2)(it.go.transform.position - playerPos)).sqrMagnitude > PickRadius * PickRadius) continue;

                it.lastPickTry = Time.time;
                var w = new MessageWriter();
                w.WriteInt(it.id);
                NetworkManager.Instance?.Send(Cmd.PICK_ITEM, w.ToArray());
                w.Cleanup();
            }
        }

        private void UseItem(int templateId)
        {
            if (!_inventory.TryGetValue(templateId, out int qty) || qty <= 0) return;
            var w = new MessageWriter();
            w.WriteInt(templateId);
            NetworkManager.Instance?.Send(Cmd.USE_ITEM, w.ToArray());
            w.Cleanup();
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
            it.go = CreateVisual(it, new Vector3(x, y, 0f));
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

        /// <summary>INVENTORY: short count, [int templateId, int qty] x count — luôn là TOÀN BỘ túi</summary>
        private void OnInventory(byte[] data)
        {
            var r = new MessageReader(data);
            short count = r.ReadShort();
            _inventory.Clear();
            for (int i = 0; i < count; i++)
            {
                int tpl = r.ReadInt();
                int qty = r.ReadInt();
                _inventory[tpl] = qty;
            }
            r.Cleanup();
        }

        /// <summary>Đổi map/khu: đồ rơi của khu cũ không còn -> xoá hết (khu mới sẽ gửi lại ITEM_DROP).</summary>
        private void OnChangeMapOrZone(byte[] _)
        {
            foreach (var it in _items.Values) if (it.go != null) Destroy(it.go);
            _items.Clear();
        }

        private static GameObject CreateVisual(GroundItem it, Vector3 pos)
        {
            if (_squareSprite == null)
            {
                var tex = new Texture2D(8, 8);
                var px = new Color[64];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _squareSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0f), 20f); // 8px/20ppu = 0.4 đơn vị
            }

            var go = new GameObject($"GroundItem_{it.id}");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _squareSprite;
            sr.color = it.templateId == HpPotionId ? new Color(1f, 0.3f, 0.3f)
                     : it.templateId == MpPotionId ? new Color(0.3f, 0.5f, 1f)
                     : new Color(1f, 0.85f, 0.3f);
            sr.sortingOrder = 5;

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text = it.qty > 1 ? $"Item {it.templateId} x{it.qty}" : $"Item {it.templateId}";
            tmp.fontSize = 2.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 6;
            return go;
        }

        private void OnGUI()
        {
            if (LocalPlayerState.Id < 0) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
                _style.normal.textColor = Color.white;
            }

            var sb = new StringBuilder();
            sb.Append($"{LocalPlayerState.Name}  Lv {LocalPlayerState.Level}\n");
            sb.Append($"EXP {LocalPlayerState.Exp}/{LocalPlayerState.Level * 1000L}   Yen {LocalPlayerState.Yen}\n");
            sb.Append("Túi: ");
            if (_inventory.Count == 0) sb.Append("(trống)");
            foreach (var kv in _inventory) sb.Append($"[{kv.Key}]x{kv.Value} ");
            sb.Append("\nJ đánh · Q bình máu · E bình chakra · Enter chat");
            if (LocalPlayerState.IsDead) sb.Append("\n<ĐÃ CHẾT> bấm R để hồi sinh");

            GUI.Box(new Rect(Screen.width - 330, 10, 320, LocalPlayerState.IsDead ? 92 : 76), sb.ToString(), _style);
        }
    }
}
