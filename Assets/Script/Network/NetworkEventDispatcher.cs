using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Network
{
    public class NetworkEventDispatcher : MonoBehaviour
    {
        public static NetworkEventDispatcher Instance;

        // Hàng đợi chứa các gói tin chờ xử lý ở Main Thread
        private Queue<PacketDecoder.Packet> packetQueue = new Queue<PacketDecoder.Packet>();

        // TỪ ĐIỂN CHỨA HÀM XỬ LÝ: Đổi từ string sang byte[]
        private Dictionary<short, Action<byte[]>> handlers = new Dictionary<short, Action<byte[]>>();

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            float startTime = Time.realtimeSinceStartup;

            // Lặp liên tục nếu Queue vẫn còn đồ và chưa lố 2ms
            while (true)
            {
                bool hasPacket = false;
                PacketDecoder.Packet currentPacket = default;

                lock (packetQueue)
                {
                    if (packetQueue.Count > 0)
                    {
                        currentPacket = packetQueue.Dequeue();
                        hasPacket = true;
                    }
                }

                // Nếu không có gói tin nào thì thoát
                // Nếu không có gói tin nào thì thoát
                if (!hasPacket) break;

                // ==========================================
                // THÊM DEBUG LOG Ở ĐÂY ĐỂ THEO DÕI
                // ==========================================
                int dataLength = currentPacket.data != null ? currentPacket.data.Length : 0;
               
                // ==========================================

                // XỬ LÝ GÓI TIN
                if (handlers.ContainsKey(currentPacket.cmd))
                {
                    if (handlers[currentPacket.cmd] != null)
                    {
                        handlers[currentPacket.cmd].Invoke(currentPacket.data);
                    }
                }
                else
                {
                    // Log thêm cái này cực kỳ hữu ích để phát hiện lỗi quên đăng ký Event
                    Debug.LogWarning($"[MẠNG CẢNH BÁO] Nhận được CMD {currentPacket.cmd} nhưng chưa có ai AddHandler để xử lý!");
                }
            }
        }

        // ĐĂNG KÝ LẮNG NGHE: Action<byte[]>
        public void AddHandler(short cmd, Action<byte[]> action)
        {
            if (!handlers.ContainsKey(cmd))
            {
                handlers[cmd] = action;
            }
            else
            {
                handlers[cmd] += action;
            }
        }

        // HỦY LẮNG NGHE: Action<byte[]>
        public void RemoveHandler(short cmd, Action<byte[]> action)
        {
            if (handlers.ContainsKey(cmd))
            {
                handlers[cmd] -= action;

                if (handlers[cmd] == null)
                {
                    handlers.Remove(cmd);
                }
            }
        }

        public void EnqueuePacket(PacketDecoder.Packet packet)
        {
            lock (packetQueue)
            {
                packetQueue.Enqueue(packet);
            }
        }
    }
}