using System;
using System.IO;

namespace Assets.Script.Network
{
    public static class PacketEncoder
    {
        // Nhận vào mảng byte thay vì chuỗi JSON
        public static byte[] Encode(short cmd, byte[] data)
        {
            int dataLength = (data != null) ? data.Length : 0;
            int totalLength = 2 + dataLength; // 2 byte cmd + n byte data

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    // 1. Ghi Length (4 byte) - Chuyển sang Big Endian cho Java Netty
                    byte[] lengthBytes = BitConverter.GetBytes(totalLength);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                    writer.Write(lengthBytes);

                    // 2. Ghi Cmd (2 byte) - Chuyển sang Big Endian
                    byte[] cmdBytes = BitConverter.GetBytes(cmd);
                    if (BitConverter.IsLittleEndian) Array.Reverse(cmdBytes);
                    writer.Write(cmdBytes);

                    // 3. Ghi Data (Không cần Reverse vì MessageWriter đã xử lý chuẩn rồi)
                    if (data != null && dataLength > 0)
                    {
                        writer.Write(data);
                    }

                    return ms.ToArray();
                }
            }
        }
    }
}