using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Network
{
    public class PacketDecoder
    {
        /// <summary>Gói server → client lớn nhất chấp nhận (gói dữ liệu tĩnh lớn nhất chỉ vài chục KB).</summary>
        public const int MaxFrame = 1024 * 1024;

        public struct Packet
        {
            public short cmd;
            public byte[] data; // Đổi từ string json sang byte[] data
        }

        /// <summary>
        /// Cắt các gói đủ trong bộ đệm: [int độ dài (big-endian, gồm cmd)] [short cmd] [dữ liệu]. Gói chưa đủ byte để lại chờ lần sau.
        /// Đọc theo chỉ số rồi xoá phần đã đọc 1 LẦN ở cuối — trước đây mỗi gói chép mảng 3 lần + xoá đầu danh sách
        /// (dồn cả bộ đệm lên), nhiều gói dính nhau thì tốn bình phương.
        /// </summary>
        public static List<Packet> Decode(List<byte> byteList)
        {
            var packets = new List<Packet>();
            int pos = 0, count = byteList.Count;
            while (count - pos >= 4)   // phải có 4 byte độ dài
            {
                int length = byteList[pos] << 24 | byteList[pos + 1] << 16 | byteList[pos + 2] << 8 | byteList[pos + 3];   // big-endian như Java
                if (length < 2 || length > MaxFrame)
                {
                    Debug.LogError($"[Network] Khung tin hỏng (độ dài {length}) — bỏ bộ đệm");
                    byteList.Clear();
                    return packets;
                }
                if (count - pos < 4 + length) break;   // chưa nhận đủ gói này → chờ mạng đẩy thêm
                short cmd = (short)(byteList[pos + 4] << 8 | byteList[pos + 5]);
                byte[] data = null;
                if (length > 2)   // có dữ liệu ngoài cmd
                {
                    data = new byte[length - 2];
                    byteList.CopyTo(pos + 6, data, 0, data.Length);
                }
                packets.Add(new Packet { cmd = cmd, data = data });
                pos += 4 + length;
            }
            if (pos > 0) byteList.RemoveRange(0, pos);
            return packets;
        }
    }
}