namespace Assets.Script.Network
{
    /// <summary>
    /// ĐỊNH DẠNG GÓI DÙNG CHUNG cho nhiều Cmd — mỗi định dạng đọc ở ĐÚNG 1 hàm (khớp docs/PROTOCOL.md).
    /// Gói chỉ thuộc 1 Cmd thì đọc ngay trong hàm xử lý Cmd đó.
    /// </summary>
    public static class Packets
    {
        /// <summary>Kết quả thao tác: byte ok, UTF lời báo — GIFTCODE_RESULT, GEM_RESULT, MARKET_RESULT.</summary>
        public struct Result
        {
            public bool ok;
            public string msg;
            /// <summary>Lời báo tô màu: xanh thành công, đỏ thất bại.</summary>
            public string Colored => (ok ? "<color=#7f7>" : "<color=#f77>") + msg + "</color>";
        }

        public static Result ReadResult(byte[] data)
        {
            var r = new MessageReader(data);
            var res = new Result { ok = r.ReadByte() != 0, msg = r.ReadUTF() };
            r.Cleanup();
            return res;
        }

        /// <summary>Lời mời: int fromId, UTF tên — TRADE_INVITE, DUEL_INVITE, PARTY_INVITE_RECV, FRIEND_INVITE.</summary>
        public struct Invite { public int fromId; public string name; }

        public static Invite ReadInvite(byte[] data)
        {
            var r = new MessageReader(data);
            var inv = new Invite { fromId = r.ReadInt(), name = r.ReadUTF() };
            r.Cleanup();
            return inv;
        }

        /// <summary>1 con quái: int id, int templateId, float x, float y, int hp, int maxHp, byte dead — phần tử MOB_LIST, cả gói MOB_ADD.</summary>
        public struct MobInfo
        {
            public int id, templateId, hp, maxHp;
            public float x, y;
            public bool dead;

            public static MobInfo Read(MessageReader r) => new MobInfo
            {
                id = r.ReadInt(), templateId = r.ReadInt(), x = r.ReadFloat(), y = r.ReadFloat(),
                hp = r.ReadInt(), maxHp = r.ReadInt(), dead = r.ReadByte() != 0,
            };
        }
    }
}
