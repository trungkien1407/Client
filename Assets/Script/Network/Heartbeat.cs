using UnityEngine;

namespace Assets.Script.Network
{
    /// <summary>
    /// HEARTBEAT 20 giây/lần mỗi khi đang kết nối: server ngắt kết nối nếu 60 giây không nhận gói nào (vd đứng AFK).
    /// Gửi cả lúc CHƯA vào game: trước đây chỉ gửi trong game → chọn hệ / đặt tên quá 60 giây ở màn tạo nhân vật là bị ngắt,
    /// bấm Tạo thì kẹt "Đang tạo nhân vật..." (gói không đi đâu cả).
    /// </summary>
    public class Heartbeat : MonoBehaviour
    {
        private const float Interval = 20f;
        private float _last;

        private void Update()
        {
            bool connected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            if (!connected || Time.unscaledTime - _last < Interval) return;
            _last = Time.unscaledTime;
            Data.GameActions.Heartbeat();
        }
    }
}
