using UnityEngine;

namespace Assets.Script.Constants
{
    /// <summary>
    /// Cấu hình kết nối của client.
    ///
    /// Địa chỉ server đọc từ Assets/Resources/server_config.json — ĐỔI IP KHÔNG CẦN SỬA CODE:
    ///   { "host": "127.0.0.1", "port": 14444 }
    /// Khi build bằng Tools/Naruto/Build (hoặc dòng lệnh với -serverHost x.x.x.x), tool tự ghi file này.
    /// [CẦN ĐIỀN khi phát hành] host = IP/tên miền VPS chạy server.
    /// </summary>
    public static class GameConfig
    {
        // Version của game = Player Settings → Version (bundleVersion). Server so sánh ở CHECK_VERSION.
        public static string ClientVersion = Application.version;

        public static string SocketHost = "127.0.0.1";
        public static int SocketPort = 14444;

        // Thời gian Timeout kết nối (giây)
        public static float ConnectionTimeout = 10f;

        [System.Serializable]
        private class ServerConfigFile { public string host; public int port; }

        /// <summary>Unity tự gọi trước khi scene đầu tiên load → mọi script đọc SocketHost đều thấy giá trị từ file.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadFromResources()
        {
            var file = Resources.Load<TextAsset>("server_config");
            if (file == null) return;
            var cfg = JsonUtility.FromJson<ServerConfigFile>(file.text);
            if (!string.IsNullOrEmpty(cfg.host)) SocketHost = cfg.host;
            if (cfg.port > 0) SocketPort = cfg.port;
            Debug.Log($"[Config] Server = {SocketHost}:{SocketPort}");
        }
    }
}
