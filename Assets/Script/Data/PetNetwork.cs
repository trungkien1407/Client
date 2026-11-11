using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;

namespace Assets.Script.Data
{
    /// <summary>
    /// GÓI THÚ CƯNG (GĐ27): PET_LIST → GameData.Pets + vẽ lại cửa sổ Thú cưng.
    /// Hình thú đi theo (của mình lẫn người khác) KHÔNG đọc ở đây mà ở đuôi PLAYER_PVP_INFO (SocialNetwork → Fx.PetFollower).
    /// Vừa nở được con đầu tiên → tự mở cửa sổ để người chơi thấy thú mới.
    /// </summary>
    public class PetNetwork : NetworkListener
    {
        protected override void RegisterHandlers() => Listen(Cmd.PET_LIST, OnPetList);

        /// <summary>PET_LIST: byte tối đa, byte n, [int uid, short tpl, short art, UTF tên, byte độ hiếm, byte loại, short cấp,
        /// int exp, int exp cần, byte mang, byte dáng, int hp, int mp, int sát thương, int phòng thủ] × n</summary>
        private void OnPetList(byte[] data)
        {
            var r = new MessageReader(data);
            int before = GameData.Pets.Count;
            bool hadSession = GameData.PetListReceived;
            GameData.PetMax = r.ReadByte();
            int n = r.ReadByte();
            GameData.Pets.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = new PetInfo
                {
                    uid = r.ReadInt(), tpl = r.ReadShort(), art = r.ReadShort(), name = r.ReadUTF(),
                    rarity = r.ReadByte(), kind = r.ReadByte(), level = r.ReadShort(), exp = r.ReadInt(), expNext = r.ReadInt(),
                };
                p.on = r.ReadByte() == 1;
                p.stage = r.ReadByte();
                for (int k = 0; k < 4; k++) p.bonus[k] = r.ReadInt();
                GameData.Pets.Add(p);
            }
            r.Cleanup();
            GameData.PetListReceived = true;
            GameData.Notify(DataKind.Pets);
            // Nở thêm thú (không phải lần nhận đầu lúc vào game) → mở cửa sổ, chọn con mới
            if (hadSession && GameData.Pets.Count > before) GameWindow.Get<PetWindow>().ShowNewest();
        }
    }
}
