using UnityEngine;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// Gắn vào Image màu phẳng muốn GIỮ NGUYÊN màu (UISkin không phủ hình kho lên) — vd hàng / vạch của bảng UITable.
    /// Không có cờ này, UISkin.SkinPanel coi mọi ô màu phẳng là "khung" và thay bằng hình 9-slice (bảng thành nâu nhạt).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoSkin : MonoBehaviour { }
}
