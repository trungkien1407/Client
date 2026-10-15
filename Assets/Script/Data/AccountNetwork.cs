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
