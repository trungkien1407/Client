using UnityEngine;

namespace Assets.Script.Network
{
    /// <summary>
    /// HEARTBEAT 20 giây/lần khi đang trong game: server ngắt kết nối nếu 60 giây không nhận gói nào (vd đứng AFK).
    /// </summary>
    public class Heartbeat : MonoBehaviour
    {
        private const float Interval = 20f;
        private float _last;

        private void Update()
        {
            bool inGame = Player.LocalPlayerState.Id >= 0 && NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            if (!inGame || Time.unscaledTime - _last < Interval) return;
            _last = Time.unscaledTime;
            Data.GameActions.Heartbeat();
        }
    }
}
