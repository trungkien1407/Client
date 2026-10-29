using System.IO;
using Assets.Script.Data;
using Assets.Script.Network;
using NUnit.Framework;

namespace NarutoTests
{
    /// <summary>
    /// UNIT TEST CLIENT (Unity Test Framework, chế độ EditMode — chạy không cần vào game, không cần server).
    /// Mỗi hàm [Test] là 1 ca: chuẩn bị dữ liệu → gọi hàm cần kiểm → Assert so kết quả với điều mong đợi.
    /// Chạy: Unity → Window → General → Test Runner → EditMode → Run All; hoặc dòng lệnh bash chay_unit_test.sh (repo Server docs/hoc/VIET_UNIT_TEST.md §10).
    ///
    /// Ở đây kiểm phần dễ sai nhất mà không cần màn hình: ĐỌC / GHI GÓI TIN. Client và server phải hiểu byte giống hệt nhau
    /// (big-endian như Java DataOutputStream) — sai 1 byte là mọi trường phía sau lệch hết.
    /// </summary>
    public class NetworkCodecTests
    {
        /// <summary>Ghi bằng MessageWriter rồi đọc lại bằng MessageReader → ra đúng giá trị (cả số âm, số lớn, chữ tiếng Việt).</summary>
        [Test]
        public void WriteThenRead_RoundTripsEveryType()
        {
            var w = new MessageWriter();
            w.WriteByte((byte)200);
            w.WriteShort(-12345);
            w.WriteInt(int.MinValue);
            w.WriteInt(123456789);
            w.WriteLong(9_000_000_000L);
            w.WriteFloat(-3.25f);
            w.WriteUTF("Ninja Làng Lá ✓");
            w.WriteBoolean(true);

            var r = new MessageReader(w.ToArray());
            Assert.AreEqual(200, r.ReadByte());
            Assert.AreEqual(-12345, r.ReadShort());
            Assert.AreEqual(int.MinValue, r.ReadInt());
            Assert.AreEqual(123456789, r.ReadInt());
            Assert.AreEqual(9_000_000_000L, r.ReadLong());
            Assert.AreEqual(-3.25f, r.ReadFloat());
            Assert.AreEqual("Ninja Làng Lá ✓", r.ReadUTF());
            Assert.IsTrue(r.ReadBoolean());
            Assert.AreEqual(0, r.Available(), "đọc xong phải hết sạch byte");
        }

        /// <summary>Thứ tự byte giống Java: int 1 = 00 00 00 01 (big-endian), không phải 01 00 00 00 như BitConverter trên PC.</summary>
        [Test]
        public void Read_IsBigEndianLikeJava()
        {
            var r = new MessageReader(new byte[] { 0, 0, 0, 1, 0x01, 0x00, 0x3F, 0x80, 0x00, 0x00 });
            Assert.AreEqual(1, r.ReadInt());
            Assert.AreEqual(256, r.ReadShort());
            Assert.AreEqual(1.0f, r.ReadFloat());   // 0x3F800000 = 1.0 theo chuẩn IEEE 754
        }

        /// <summary>Gói hụt byte: ReadInt báo lỗi + trả 0 (không làm sập game); ReadShort ném lỗi rõ ràng.</summary>
        [Test]
        public void Read_ShortPacket_DoesNotCrashSilently()
        {
            // ReadInt cố ý ghi Debug.LogError — báo trước cho Test Runner (không thì log lỗi bị coi là ca hỏng)
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("NETWORK ERROR"));
            Assert.AreEqual(0, new MessageReader(new byte[] { 1, 2 }).ReadInt());
            Assert.Throws<EndOfStreamException>(() => new MessageReader(new byte[] { 1 }).ReadShort());
            Assert.AreEqual(0, new MessageReader(null).Available());
        }

        /// <summary>Độ dài chuỗi > 32767 (Java ghi không dấu) vẫn đọc đúng — từng là lỗi thật (đọc short có dấu ra số âm).</summary>
        [Test]
        public void ReadUTF_LongStringOver32767Bytes()
        {
            string big = new string('a', 40000);
            var w = new MessageWriter();
            w.WriteUTF(big);
            Assert.AreEqual(big, new MessageReader(w.ToArray()).ReadUTF());
        }

        /// <summary>"Ô đồ" (khớp server ItemService.writeSlot): id, SL, cấp, khoá, phẩm chất, danh sách ngọc.</summary>
        [Test]
        public void BagSlot_ReadsServerSlotFormat()
        {
            var w = new MessageWriter();
            w.WriteInt(56); w.WriteInt(1); w.WriteByte((byte)7); w.WriteByte((byte)1);   // id 56, SL 1, +7, khoá
            w.WriteByte((byte)15); w.WriteByte((byte)2); w.WriteInt(90); w.WriteInt(95);  // phẩm chất 15%, 2 ngọc
            var s = BagSlot.Read(new MessageReader(w.ToArray()));
            Assert.AreEqual(56, s.tpl);
            Assert.AreEqual(7, s.level);
            Assert.IsTrue(s.locked);
            Assert.AreEqual(15, s.bonus);
            CollectionAssert.AreEqual(new[] { 90, 95 }, s.gems);
        }
    }

    /// <summary>Hàm thuần của GameData — không đụng màn hình, chỉ vào → ra.</summary>
    public class GameDataRuleTests
    {
        [TestCase(0, 0)]
        [TestCase(8, 0)]
        [TestCase(9, 1)]
        [TestCase(12, 2)]
        [TestCase(15, 3)]
        [TestCase(16, 3)]
        public void GlowTier_ByUpgradeLevel(int level, int expected)
        {
            Assert.AreEqual(expected, GameData.GlowTier(level));   // hào quang vũ khí: +9 / +12 / +15
        }

        [Test]
        public void FormatDuration_PicksLargestUnit()
        {
            Assert.AreEqual("6 ngày 23 giờ", GameData.FormatDuration(6 * 86400 + 23 * 3600 + 59));
            Assert.AreEqual("5 giờ 12 phút", GameData.FormatDuration(5 * 3600 + 12 * 60));
            Assert.AreEqual("12 phút", GameData.FormatDuration(12 * 60 + 30));
            Assert.AreEqual("dưới 1 phút", GameData.FormatDuration(59));
        }

        [Test]
        public void EquipSlots_TenSlotsInSetOrder()
        {
            Assert.AreEqual(10, GameData.EquipSlotCount);
            Assert.AreEqual("Nón", GameData.SlotNames[10]);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, GameData.EquipOrder, "mỗi ô đúng 1 lần");
            Assert.AreEqual(1, GameData.EquipOrder[0], "vũ khí đứng đầu (cấp thấp nhất trong bộ)");
        }
    }
}
