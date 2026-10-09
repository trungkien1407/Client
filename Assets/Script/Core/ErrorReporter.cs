using System.Collections.Concurrent;
using System.Collections.Generic;
using Assets.Script.Constants;
using Assets.Script.Network;
using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// BÁO LỖI VỀ SERVER: mọi Exception / Debug.LogError trong game của người chơi được gửi lên server
    /// (gói CLIENT_ERROR) → ghi vào logs/client_errors.log bên server. Biết lỗi trên máy người chơi mà không cần
    /// dịch vụ trả phí. Mỗi lỗi giống nhau chỉ gửi 1 lần / phiên, tối đa 20 lỗi / phiên (server cũng giới hạn).
    /// Tự chạy khi mở game (RuntimeInitializeOnLoadMethod). Trong Unity Editor KHÔNG gửi (lỗi lúc dev xem ở Console).
    /// </summary>
    public class ErrorReporter : MonoBehaviour
    {
        private const int MaxPerSession = 20;
        private static readonly ConcurrentQueue<string[]> Pending = new ConcurrentQueue<string[]>();
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static int _sent;
        private static string _platform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor) return;
            _platform = $"{Application.platform} · {SystemInfo.operatingSystem} · {SystemInfo.deviceModel}";
            var go = new GameObject("[ErrorReporter]");
            DontDestroyOnLoad(go);
            go.AddComponent<ErrorReporter>();
            // Threaded: bắt cả lỗi từ luồng phụ (luồng mạng) — chỉ xếp hàng, gửi ở Update (luồng chính)
            Application.logMessageReceivedThreaded += OnLog;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            string firstLine = string.IsNullOrEmpty(stack) ? "" : stack.Split('\n')[0];
            string key = message + "|" + firstLine;
            lock (Seen)
            {
                if (Seen.Count >= 200 || !Seen.Add(key)) return; // lỗi trùng chỉ báo 1 lần
            }
            Pending.Enqueue(new[] { type + ": " + message, stack ?? "" });
        }

        private void Update()
        {
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected) return; // chưa kết nối thì giữ lại, kết nối xong gửi
            while (_sent < MaxPerSession && Pending.TryDequeue(out var e))
            {
                Data.GameActions.ReportError(Application.version, _platform, Cut(e[0], 500), Cut(e[1], 2000));
                _sent++;
            }
        }

        private static string Cut(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

        private void OnDestroy() => Application.logMessageReceivedThreaded -= OnLog;
    }
}
