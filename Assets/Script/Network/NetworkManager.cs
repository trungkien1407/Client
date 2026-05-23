using UnityEngine;
using System;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Assets.Script.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;

        private TcpClient client;
        private NetworkStream stream;
        private byte[] receiveBuffer = new byte[4096];
        private List<byte> byteList = new List<byte>();

        // Các Event giao tiếp với UI
        public event Action OnConnectedSuccessfully;
        public event Action<string> OnConnectionFailed;
        public event Action OnDisconnected;

        // Cờ xử lý Thread-Safe
        private bool _isConnectionSuccessPending = false;
        private string _connectionErrorPending = "";
        private bool _isDisconnectedPending = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        void Update()
        {
            if (_isConnectionSuccessPending)
            {
                _isConnectionSuccessPending = false;
                OnConnectedSuccessfully?.Invoke();
            }

            if (!string.IsNullOrEmpty(_connectionErrorPending))
            {
                OnConnectionFailed?.Invoke(_connectionErrorPending);
                _connectionErrorPending = "";
            }

            if (_isDisconnectedPending)
            {
                _isDisconnectedPending = false;
                OnDisconnected?.Invoke();
            }
        }

        public void Connect(string ip, int port)
        {
            try
            {
                // QUAN TRỌNG: Dọn dẹp sạch sẽ kết nối/luồng cũ trước khi tạo mới
                CleanUp();

                client = new TcpClient();
                client.BeginConnect(ip, port, OnConnect, null);
            }
            catch (Exception e)
            {
                _connectionErrorPending = "Lỗi khởi tạo kết nối: " + e.Message;
            }
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {
                if (client == null) return;

                client.EndConnect(ar);
                stream = client.GetStream();

                _isConnectionSuccessPending = true;

                // Bắt đầu lắng nghe
                stream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, OnReceive, null);
            }
            catch (Exception e)
            {
                _connectionErrorPending = "Kết nối thất bại: " + e.Message;
                CleanUp();
            }
        }

        // ==========================================
        // GỬI DỮ LIỆU
        // ==========================================
        public void Send(short cmd, byte[] data)
        {
            if (client == null || !client.Connected || stream == null) return;

            try
            {
                byte[] packet = PacketEncoder.Encode(cmd, data);
                stream.Write(packet, 0, packet.Length);
            }
            catch (Exception)
            {
                _isDisconnectedPending = true;
                CleanUp();
            }
        }

        // ==========================================
        // NHẬN DỮ LIỆU
        // ==========================================
        private void OnReceive(IAsyncResult ar)
        {
            NetworkStream s = stream;

            try
            {
                if (s == null || !s.CanRead) return;

                int bytesRead = s.EndRead(ar);

                if (bytesRead <= 0)
                {
                    Debug.Log("[NET] Server đã chủ động đóng kết nối.");
                    _isDisconnectedPending = true;
                    CleanUp();
                    return;
                }

                byte[] data = new byte[bytesRead];
                Array.Copy(receiveBuffer, 0, data, 0, bytesRead);

                lock (byteList)
                {
                    byteList.AddRange(data);
                    var packets = PacketDecoder.Decode(byteList);
                    foreach (var packet in packets)
                    {
                        NetworkEventDispatcher.Instance.EnqueuePacket(packet);
                    }
                }

                if (stream != null && stream.CanRead)
                {
                    stream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, OnReceive, null);
                }
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("[NET] Stream đã đóng an toàn.");
            }
            catch (Exception ex)
            {
                if (stream != null)
                {
                    Debug.LogWarning($"[RECV ERROR] {ex.Message}");
                    _isDisconnectedPending = true;
                    CleanUp();
                }
            }
        }

        // Đóng kết nối chủ động từ Client (Không gọi OnDisconnected để tránh loop)
        public void Disconnect()
        {
            CleanUp();
        }

        private void CleanUp()
        {
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (client != null)
            {
                client.Close();
                client = null;
            }

            lock (byteList)
            {
                byteList.Clear();
            }
        }

        public bool IsConnected
        {
            get { return client != null && client.Connected; }
        }

        private void OnApplicationQuit()
        {
            CleanUp();
        }
    }
}