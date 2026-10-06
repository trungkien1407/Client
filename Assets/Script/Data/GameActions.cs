using Assets.Script.Constants;
using Assets.Script.Network;

namespace Assets.Script.Data
{
    /// <summary>
    /// MỌI THAO TÁC NGƯỜI CHƠI GỬI LÊN SERVER (C→S) ở 1 chỗ. UI chỉ gọi các hàm này,
    /// không tự viết MessageWriter. Server kiểm tra hợp lệ rồi trả kết quả (INVENTORY, CHARACTER_INFO...).
    /// </summary>
    public static class GameActions
    {
        private static void Send(short cmd, System.Action<MessageWriter> write)
        {
            var w = new MessageWriter();
            write?.Invoke(w);
            NetworkManager.Instance?.Send(cmd, w.ToArray());
            w.Cleanup();
        }

        // ---- Vật phẩm ----
        public static void UseItem(int templateId) => Send(Cmd.USE_ITEM, w => w.WriteInt(templateId));
        public static void Equip(int templateId) => Send(Cmd.EQUIP_ITEM, w => w.WriteInt(templateId));
        public static void Unequip(int slot) => Send(Cmd.UNEQUIP_ITEM, w => w.WriteInt(slot));
        public static void Buy(int npcId, int templateId, int qty) => Send(Cmd.BUY_ITEM, w => { w.WriteInt(npcId); w.WriteInt(templateId); w.WriteInt(qty); });
        public static void Sell(int templateId, int qty) => Send(Cmd.SELL_ITEM, w => { w.WriteInt(templateId); w.WriteInt(qty); });

        // ---- Nhân vật / kỹ năng ----
        /// <param name="stat">0 Sức mạnh · 1 Thân pháp · 2 Chakra · 3 Thể lực</param>
        public static void AddPotential(byte stat, short amount) => Send(Cmd.ADD_POTENTIAL, w => { w.WriteByte(stat); w.WriteShort(amount); });
        public static void UpgradeSkill(int skillId) => Send(Cmd.SKILL_UPGRADE, w => w.WriteInt(skillId));
        public static void SetShortcut(int slot, int skillId) => Send(Cmd.SET_SKILL_SHORTCUT, w => { w.WriteByte((byte)slot); w.WriteInt(skillId); });

        // ---- NPC ----
        public static void NpcTalk(int npcId) => Send(Cmd.NPC_TALK, w => w.WriteInt(npcId));
        public static void NpcSelect(int npcId, int index) => Send(Cmd.NPC_SELECT, w => { w.WriteInt(npcId); w.WriteByte((byte)index); });
    }
}
