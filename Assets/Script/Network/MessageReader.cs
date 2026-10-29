using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.Script.Network
{
    /// <summary>
    /// Đọc dữ liệu 1 gói tin theo kiểu Java DataInputStream (big-endian).
    /// Đọc thẳng trên mảng byte bằng chỉ số vị trí — trước đây mỗi ReadInt / ReadFloat cấp 1 mảng byte mới rồi đảo ngược,
    /// gói MOB_MOVE_BATCH 30 quái ra ~90 mảng rác → GC giật trên Android (rà soát hiệu năng 2026-10-29).
    /// </summary>
    public class MessageReader
    {
        private readonly byte[] _buf;
        private int _pos;

        public MessageReader(byte[] data)
        {
            _buf = data ?? Array.Empty<byte>();
        }

        /// <summary>Còn đủ n byte không; thiếu → ném lỗi như BinaryReader cũ (EndOfStream).</summary>
        private void Need(int n)
        {
            if (_buf.Length - _pos < n) throw new EndOfStreamException($"Gói tin thiếu dữ liệu: cần {n} byte, còn {_buf.Length - _pos}");
        }

        public byte ReadByte() { Need(1); return _buf[_pos++]; }
        public sbyte ReadSByte() { return (sbyte)ReadByte(); }
        public bool ReadBoolean() { return ReadByte() != 0; }

        public short ReadShort()
        {
            Need(2);
            short v = (short)((_buf[_pos] << 8) | _buf[_pos + 1]);
            _pos += 2;
            return v;
        }

        public int ReadInt()
        {
            int available = Available();
            if (available < 4)
            {
                // Đây là "chuông báo động"
                Debug.LogError($"[NETWORK ERROR] Không đủ 4 byte để đọc Int! Còn dư: {available} byte. Tổng gói: {_buf.Length}");
                return 0;
            }
            int v = (_buf[_pos] << 24) | (_buf[_pos + 1] << 16) | (_buf[_pos + 2] << 8) | _buf[_pos + 3];
            _pos += 4;
            return v;
        }

        public long ReadLong()
        {
            Need(8);
            long hi = (uint)((_buf[_pos] << 24) | (_buf[_pos + 1] << 16) | (_buf[_pos + 2] << 8) | _buf[_pos + 3]);
            long lo = (uint)((_buf[_pos + 4] << 24) | (_buf[_pos + 5] << 16) | (_buf[_pos + 6] << 8) | _buf[_pos + 7]);
            _pos += 8;
            return (hi << 32) | lo;
        }

        public float ReadFloat()
        {
            Need(4);
            int bits = (_buf[_pos] << 24) | (_buf[_pos + 1] << 16) | (_buf[_pos + 2] << 8) | _buf[_pos + 3];
            _pos += 4;
            return BitConverter.Int32BitsToSingle(bits);
        }

        // Tương đương với DataInputStream.readUTF() bên Java
        public string ReadUTF()
        {
            int length = (ushort)ReadShort(); // Java ghi độ dài không dấu 0..65535 → đọc short có dấu sẽ ra số âm khi > 32767
            if (length == 0) return "";
            length = Math.Min(length, Available());   // gói hụt: đọc phần còn lại (như BinaryReader.ReadBytes cũ)
            string s = Encoding.UTF8.GetString(_buf, _pos, length);
            _pos += length;
            return s;
        }

        // Kiểm tra xem còn data để đọc không (tránh lỗi End of Stream)
        public int Available()
        {
            return _buf.Length - _pos;
        }

        /// <summary>Giữ cho code cũ gọi — không còn stream nào phải đóng.</summary>
        public void Cleanup() { }
    }
}
