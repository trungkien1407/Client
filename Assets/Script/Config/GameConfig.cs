using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Script.Constants
{
    public static class GameConfig
    {
        // Version của game - Bạn có thể để Application.version 
        // hoặc ghi đè tại đây nếu muốn quản lý riêng
        public static string ClientVersion = UnityEngine.Application.version;

        // Địa chỉ Server Netty (Socket)
        public static string SocketHost = "127.0.0.1";
        public static int SocketPort = 14444;

        // Địa chỉ Web API (Spring Boot)
        public static string WebApiUrl = "http://127.0.0.1:8080/api/auth";

        // Thời gian Timeout kết nối (giây)
        public static float ConnectionTimeout = 10f;
    }
}
