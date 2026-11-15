using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.UI.Windows;

namespace Assets.Script.Data
{
    /// <summary>GÓI TÀI KHOẢN khi đang trong game: kết quả đổi mật khẩu (CHANGE_PASSWORD). Payload: PROTOCOL.md (repo server).</summary>
    public class AccountNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
            Listen(Cmd.CHANGE_PASSWORD, OnChangePassword);
            Listen(Cmd.ACCOUNT_INFO, OnAccountInfo);
            Listen(Cmd.ACCOUNT_VERIFY, OnAccountVerify);
        }

        /// <summary>ACCOUNT_INFO: byte isGuest, byte canVerify. Khách vừa đủ điều kiện → thú đưa tin nhắc đi xác thực.</summary>
        private void OnAccountInfo(byte[] data)
        {
            var r = new MessageReader(data);
            bool guest = r.ReadByte() == 1;
            bool can = r.Available() >= 1 && r.ReadByte() == 1;
            r.Cleanup();
            bool wasEligible = GameData.IsGuest && GameData.CanVerify;
            GameData.IsGuest = guest;
            GameData.CanVerify = guest && can;
            if (GameData.CanVerify && !wasEligible)
                UI.NoticeMascot.Show("Bạn đang chơi bằng tài khoản khách. Vào Cài đặt → Xác thực tài khoản để giữ nhân vật khi đổi máy.");
        }

        /// <summary>ACCOUNT_VERIFY: short status, [UTF tên mới] (mã 0) / [int giây] (mã 7)</summary>
        private void OnAccountVerify(byte[] data)
        {
            var r = new MessageReader(data);
            short status = r.ReadShort();
            string name = status == 0 && r.Available() >= 2 ? r.ReadUTF() : null;
            int extra = status == 7 && r.Available() >= 4 ? r.ReadInt() : -1;
            r.Cleanup();
            AccountVerifyWindow.OnResult(status, name, extra);
        }

        /// <summary>CHANGE_PASSWORD: short status, [int extra] (mã 2: số lần còn thử · mã 3: số giây phải đợi)</summary>
        private void OnChangePassword(byte[] data)
        {
            var r = new MessageReader(data);
            short status = r.ReadShort();
            int extra = r.Available() >= 4 ? r.ReadInt() : -1;
            r.Cleanup();
            ChangePasswordWindow.OnResult(status, extra);
        }
    }
}
