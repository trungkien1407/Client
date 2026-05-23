using System;
using System.IO;
using System.Text;

namespace Assets.Script.Network
{
    public class MessageWriter
    {
        private MemoryStream ms;
        private BinaryWriter bw;

        public MessageWriter()
        {
            ms = new MemoryStream();
            bw = new BinaryWriter(ms);
        }

        public void WriteByte(byte v) { bw.Write(v); }
        public void WriteByte(sbyte v) { bw.Write((byte)v); }
        public void WriteBoolean(bool v) { bw.Write(v); }

        // Cần đảo ngược mảng byte để C# (Little-Endian) nói chuyện được với Java (Big-Endian)
        public void WriteShort(short v)
        {
            byte[] bytes = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            bw.Write(bytes);
        }

        public void WriteInt(int v)
        {
            byte[] bytes = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            bw.Write(bytes);
        }

        public void WriteLong(long v)
        {
            byte[] bytes = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            bw.Write(bytes);
        }

        public void WriteFloat(float v)
        {
            byte[] bytes = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            bw.Write(bytes);
        }

        // Tương đương với DataOutputStream.writeUTF() bên Java
        public void WriteUTF(string v)
        {
            if (string.IsNullOrEmpty(v))
            {
                WriteShort(0);
                return;
            }
            byte[] stringBytes = Encoding.UTF8.GetBytes(v);
            WriteShort((short)stringBytes.Length); // Ghi 2 byte độ dài chuỗi trước
            bw.Write(stringBytes);                 // Sau đó mới ghi nội dung chuỗi
        }

        public byte[] ToArray()
        {
            return ms.ToArray();
        }

        public void Cleanup()
        {
            if (bw != null) bw.Close();
            if (ms != null) ms.Close();
        }
    }
}