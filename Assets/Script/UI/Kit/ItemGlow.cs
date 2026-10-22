using Assets.Script.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// VIỀN SÁNG NHẤP NHÁY cho ô đồ cường hoá cao (+9 tím · +12 cam · +15 đỏ — cùng màu chữ +N và hào quang nhân vật).
    /// Dùng component Outline có sẵn của Unity UI (vẽ thêm bản sao hình ô lệch 4 phía → thành viền), chỉ đổi độ đậm theo thời gian.
    ///   ItemGlow.Add(ô, cấp)   — dưới +9 thì không làm gì
    /// </summary>
    public class ItemGlow : MonoBehaviour
    {
        private Outline _outline;
        private Color _color;

        public static ItemGlow Add(Component cell, int level)
        {
            int tier = GameData.GlowTier(level);
            if (cell == null || tier == 0 || cell.GetComponent<Graphic>() == null) return null;
            var g = cell.gameObject.AddComponent<ItemGlow>();
            g._color = Fx.BodyAura.GlowColors[tier];
            g._outline = cell.gameObject.AddComponent<Outline>();
            g._outline.effectDistance = new Vector2(2f + tier, 2f + tier);   // bậc cao viền dày hơn
            g._outline.useGraphicAlpha = false;
            g.Update();
            return g;
        }

        private void Update()
        {
            if (_outline == null) return;
            var c = _color;
            c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            _outline.effectColor = c;
        }
    }
}
