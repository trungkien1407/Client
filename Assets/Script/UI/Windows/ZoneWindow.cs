using System.Collections.Generic;
using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CHỌN KHU (giống NSO): mỗi map chia nhiều khu, mỗi khu tối đa ~15 người. Mở bằng nút "Khu N" dưới minimap.
    /// Mở → gửi ZONE_LIST_REQ → server trả ZONE_LIST (số người từng khu) → bấm 1 khu → CHANGE_ZONE.
    /// Màu: xanh = vắng · vàng = khá đông · đỏ = đầy. Đang ở phó bản / lôi đài thì không đổi được.
    /// </summary>
    public class ZoneWindow : GameWindow
    {
        private RectTransform _grid;
        private TextMeshProUGUI _info;
        private readonly List<int[]> _zones = new List<int[]>(); // [id, players, max]
        private int _current = -1;
        /// <summary>Số khu đã nhận từ server (AutoTest kiểm).</summary>
        public int ZoneCount => _zones.Count;

        protected override void Build()
        {
            Title = "Chọn khu";
            Size = new Vector2(520, 420);
            var body = CreateBody();
            _info = UIKit.Text("Info", body, "", 15, TextAlignmentOptions.MidlineLeft, UIKit.DimText);
            _info.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 26), new Vector2(0.5f, 1));
            var scroll = UIKit.ScrollGrid("Grid", body, new Vector2(112, 56), new Vector2(8, 8), 4);
            ((RectTransform)scroll.parent).Fill(0, 0, 30, 0);
            _grid = scroll;
        }

        private void OnEnable()
        {
            if (LocalPlayerState.Id >= 0) Data.GameActions.ZoneListRequest();
        }

        /// <summary>ZONE_LIST: byte currentZone (255 = khu riêng), byte n, [byte zoneId, byte players, byte max] x n</summary>
        public void SetData(int current, List<int[]> zones)
        {
            _current = current;
            _zones.Clear();
            _zones.AddRange(zones);
            RefreshIfOpen();
        }

        protected override void Refresh()
        {
            UIKit.Clear(_grid);
            if (_current == 255) { _info.text = "Bạn đang ở khu riêng (phó bản / lôi đài) — không đổi khu được."; return; }
            _info.text = _zones.Count == 0 ? "Đang tải danh sách khu..." : $"Bạn đang ở khu {_current + 1}. Chọn khu khác:";
            foreach (var z in _zones)
            {
                int id = z[0], n = z[1], max = Mathf.Max(1, z[2]);
                float fill = (float)n / max;
                string col = fill >= 1f ? "#ff6060" : fill >= 0.67f ? "#ffd84a" : "#7fff7f";
                var b = UIKit.Button("Z" + id, _grid, $"Khu {id + 1}\n<size=13><color={col}>{n}/{max}</color></size>", () =>
                {
                    if (id == _current) return;
                    Data.GameActions.ChangeZone(id);
                    Hide();
                }, 16);
                b.image.color = id == _current ? UIKit.ButtonHot : UIKit.SlotColor;
                b.interactable = id == _current || fill < 1f;
            }
        }
    }
}
