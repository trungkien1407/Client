using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Network
{
    public class PacketDecoder
    {
        public struct Packet
        {
            public short cmd;
            public byte[] data; // Đổi từ string json sang byte[] data
        }

        public static List<Packet> Decode(List<byte> byteList)
        {
            List<Packet> packets = new List<Packet>();

            // Phải có ít nhất 4 byte để đọc được Length
            while (byteList.Count >= 4)
            {
                // Đọc 4 byte đầu để lấy Length
                byte[] lengthBytes = byteList.GetRange(0, 4).ToArray();
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes); // C# đọc Big-Endian từ Java gửi về
                int length = BitConverter.ToInt32(lengthBytes, 0);

                // Kiểm tra xem buffer đã nhận đủ độ dài gói chưa (4 byte length + số byte nội dung)
                if (byteList.Count >= 4 + length)
                {

                    // Đọc Cmd (2 byte)
                    byte[] cmdBytes = byteList.GetRange(4, 2).ToArray();

                    if (BitConverter.IsLittleEndian) Array.Reverse(cmdBytes);
                    short cmd = BitConverter.ToInt16(cmdBytes, 0);
               //     Debug.Log($"[RECV] CMD: {cmd} | Length: {length} | Hex: {BitConverter.ToString(byteList.GetRange(0, 4 + length).ToArray())}");
                    // Đọc phần Data thô (nếu có)
                    byte[] data = null;
                    if (length > 2) // Nếu length > 2 tức là có data đi kèm ngoài cái cmd
                    {
                        data = byteList.GetRange(6, length - 2).ToArray();
                    }

                    // Đưa vào danh sách gói tin đã giải mã thành công
                    packets.Add(new Packet { cmd = cmd, data = data });

                    // Xóa phần đã xử lý khỏi danh sách byte chờ để đọc gói tiếp theo (nếu bị dính gói)
                    byteList.RemoveRange(0, 4 + length);
                }
                else
                {
                    break; // Chưa nhận đủ byte của gói này, thoát vòng lặp chờ mạng đẩy thêm về
                }
            }
            return packets;
        }
    }
}