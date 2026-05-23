using System;
using UnityEngine;
using System.IO;
using System.Text;

namespace Assets.Script.Network
{
    public class MessageReader
    {
        private MemoryStream ms;
        private BinaryReader br;

        public MessageReader(byte[] data)
        {
            if (data == null) data = new byte[0];
            ms = new MemoryStream(data);
            br = new BinaryReader(ms);
        }

        public byte ReadByte() { return br.ReadByte(); }
        public sbyte ReadSByte() { return (sbyte)br.ReadByte(); }
        public bool ReadBoolean() { return br.ReadBoolean(); }

        public short ReadShort()
        {
            byte[] bytes = br.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        public int ReadInt()
        {
            int available = Available();
            if (available < 4)
            {
                // Đây là "chuông báo động"
              Debug.LogError($"[NETWORK ERROR] Không đủ 4 byte để đọc Int! Còn dư: {available} byte. Tổng gói: {ms.Length}");
                return 0;
            }
            byte[] bytes = br.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public long ReadLong()
        {
            byte[] bytes = br.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }

        public float ReadFloat()
        {
            byte[] bytes = br.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        // Tương đương với DataInputStream.readUTF() bên Java
        public string ReadUTF()
        {
            short length = ReadShort();
            if (length <= 0) return "";
            byte[] stringBytes = br.ReadBytes(length);
            return Encoding.UTF8.GetString(stringBytes);
        }

        // Kiểm tra xem còn data để đọc không (tránh lỗi End of Stream)
        public int Available()
        {
            return (int)(ms.Length - ms.Position);
        }

        public void Cleanup()
        {
            if (br != null) br.Close();
            if (ms != null) ms.Close();
        }
    }
}