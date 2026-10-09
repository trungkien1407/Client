using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Network
{
    /// <summary>
    /// Chuyển gói tin từ luồng socket sang luồng chính (Unity chỉ cho đụng GameObject ở luồng chính).
    ///   Luồng socket: EnqueuePacket → hàng đợi (có khoá).
    ///   Luồng chính:  Update rút hết hàng đợi → gọi các hàm đã AddHandler cho Cmd đó.
    /// Mỗi hàm xử lý được gọi riêng trong try/catch: 1 hàm lỗi không làm mất gói của hàm khác, không văng khỏi Update.
    /// </summary>
    public class NetworkEventDispatcher : MonoBehaviour
    {
        public static NetworkEventDispatcher Instance;

        private readonly Queue<PacketDecoder.Packet> packetQueue = new Queue<PacketDecoder.Packet>();
        private readonly Dictionary<short, Action<byte[]>> handlers = new Dictionary<short, Action<byte[]>>();

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            while (true)
            {
                PacketDecoder.Packet packet;
                lock (packetQueue)
                {
                    if (packetQueue.Count == 0) break;
                    packet = packetQueue.Dequeue();
                }

                if (!handlers.TryGetValue(packet.cmd, out var all) || all == null)
                {
                    Debug.LogWarning($"[Mạng] Nhận Cmd {packet.cmd} nhưng chưa có ai AddHandler để xử lý");
                    continue;
                }
                foreach (var d in all.GetInvocationList())
                {
                    try
                    {
                        ((Action<byte[]>)d)(packet.data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Mạng] Lỗi xử lý gói {packet.cmd} ở {d.Method.DeclaringType?.Name}.{d.Method.Name}: {e}");
                    }
                }
            }
        }

        public void AddHandler(short cmd, Action<byte[]> action)
        {
            handlers.TryGetValue(cmd, out var cur);
            handlers[cmd] = cur + action;
        }

        public void RemoveHandler(short cmd, Action<byte[]> action)
        {
            if (!handlers.TryGetValue(cmd, out var cur)) return;
            cur -= action;
            if (cur == null) handlers.Remove(cmd);
            else handlers[cmd] = cur;
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
