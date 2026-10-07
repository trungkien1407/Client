using Assets.Script.Data;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI
{
    /// <summary>
    /// TÊN TRÊN ĐẦU người chơi khác (chữ TextMeshPro trong thế giới, không cần prefab).
    /// Màu theo PvP (giống NSO):
    ///   trắng = bình thường · cam = đang bật Đồ sát · đỏ = có điểm PK (vừa giết người) · xanh lá = cùng gia tộc/nhóm.
    /// Dòng dưới: [Tên gia tộc] nếu có. Dữ liệu từ PLAYER_PVP_INFO (GameData.Pvp).
    ///
    /// [CẦN ĐIỀN tuỳ chọn] chỉnh HeightOffset (đơn vị thế giới, tính từ chân) nếu nhân vật Spine cao/thấp hơn; font = font mặc định TMP.
    /// </summary>
    public class NameTag : MonoBehaviour
    {
        public static float HeightOffset = 2.1f;

        private RemotePlayer _owner;
        private TextMeshPro _text;
        private string _last;

        public static void Attach(RemotePlayer rp)
        {
            if (rp == null) return;
            var tag = rp.GetComponentInChildren<NameTag>(true);
            if (tag == null)
            {
                var go = new GameObject("NameTag");
                go.transform.SetParent(rp.transform, false);
                tag = go.AddComponent<NameTag>();
            }
            tag._owner = rp;
            tag._last = null;
        }

        private void Awake()
        {
            _text = gameObject.AddComponent<TextMeshPro>();
            _text.fontSize = 3f;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.enableWordWrapping = false;
            _text.outlineWidth = 0.2f;
            _text.outlineColor = Color.black;
            _text.rectTransform.sizeDelta = new Vector2(8, 1.2f);
            // Vẽ trên mọi sprite (nhân vật, NPC, map): sorting layer TRÊN CÙNG + order cao
            var r = _text.GetComponent<MeshRenderer>();
            var layers = SortingLayer.layers;
            if (layers.Length > 0) r.sortingLayerID = layers[layers.Length - 1].id;
            r.sortingOrder = 32767; // tối đa — Spine/NPC cũng ở layer Player
        }

        private void LateUpdate()
        {
            if (_owner == null || _owner.myData == null) return;
            // Đặt theo TOẠ ĐỘ THẾ GIỚI (nhân vật có thể scale < 1 hoặc lật trái/phải bằng scale.x âm)
            transform.position = _owner.transform.position + new Vector3(0, HeightOffset, 0);
            var ls = transform.parent.lossyScale;
            float sx = Mathf.Abs(ls.x) > 0.0001f ? 1f / ls.x : 1f, sy = Mathf.Abs(ls.y) > 0.0001f ? 1f / ls.y : 1f;
            transform.localScale = new Vector3(sx, sy, 1); // chữ luôn cùng cỡ, không bị ngược

            int id = _owner.GetId();
            GameData.Pvp.TryGetValue(id, out var pvp);
            string color = "#ffffff";
            bool ally = GameData.Party.Exists(m => m.id == id)
                        || (pvp != null && !string.IsNullOrEmpty(pvp.guild) && GameData.Guild.Has && pvp.guild == GameData.Guild.name);
            if (pvp != null && pvp.pkPoint > 0) color = "#ff4a4a";
            else if (pvp != null && pvp.pkMode == 1) color = "#ffa030";
            else if (ally) color = "#6cff6c";

            string s = $"<color={color}>{_owner.myData.name}</color>";
            if (pvp != null && !string.IsNullOrEmpty(pvp.guild)) s = $"<size=2.2><color=#9fd3ff>[{pvp.guild}]</color></size>\n" + s;
            if (s != _last) { _text.text = s; _last = s; }
        }
    }
}
