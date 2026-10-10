namespace Assets.Script.Manager
{
    /// <summary>
    /// Mã trả về của LOGIN / REGISTER / CREATE_CHARACTER → lời báo cho người chơi. Bảng mã: docs/ky-thuat/PROTOCOL.md (repo server).
    /// </summary>
    public static class AuthMessages
    {
        /// <summary>
        /// LOGIN (status ≠ 0, 1). {extra}: số lần thử còn lại (mã 3) / số giây phải đợi (8, 9, 10), -1 nếu server không gửi.
        /// {note}: lời nhắn bảo trì của GM (mã 12).
        /// </summary>
        public static string Login(short status, int extra, string note) => status switch
        {
            3 => extra == 0 ? "Sai mật khẩu 5 lần. Tài khoản tạm khoá đăng nhập trên máy này 5 phút."
                : extra > 0 ? $"Sai tài khoản hoặc mật khẩu. Còn {extra} lần thử."
                : "Sai tài khoản hoặc mật khẩu",
            4 => "Tài khoản đang đăng nhập ở nơi khác",
            6 => "Tài khoản đã bị khoá. Liên hệ quản trị viên.",
            7 => "Máy chủ đã đầy. Vui lòng thử lại sau ít phút.",
            8 => $"Mạng của bạn đăng nhập quá nhiều lần. Đợi {WaitText(extra)} rồi thử lại.",
            9 => $"Sai mật khẩu quá 5 lần. Đợi {WaitText(extra)} rồi thử lại.",
            10 => $"Tài khoản vừa đăng nhập. Đợi {WaitText(extra)} rồi thử lại.",
            11 => "Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!",
            12 => string.IsNullOrEmpty(note) ? "Máy chủ đang bảo trì. Vui lòng quay lại sau." : note,
            _ => "Lỗi máy chủ!"
        };

        public static string Register(short status) => status switch
        {
            1 => "Tài khoản từ 4 ký tự, mật khẩu từ 6 ký tự.",
            2 => "Tài khoản hoặc email đã được dùng!",
            3 => "Email không hợp lệ.",
            4 => "Mạng của bạn thử quá nhiều lần. Đợi ít phút rồi thử lại.",
            5 => "Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!",
            6 => "Máy chủ đang bảo trì, tạm chưa đăng ký được. Vui lòng quay lại sau.",
            _ => "Lỗi máy chủ!"
        };

        public static string CreateCharacter(short status) => status switch
        {
            1 => "Tên nhân vật phải từ 3 đến 15 ký tự.",
            2 => "Tên nhân vật đã có người dùng!",
            3 => "Phiên đăng nhập đã hết, vui lòng đăng nhập lại.",
            4 => "Hệ phái không hợp lệ.",
            5 => "Tài khoản đã có nhân vật.",
            _ => "Lỗi tạo nhân vật!"
        };

        /// <summary>Số giây → "45 giây" / "5 phút".</summary>
        private static string WaitText(int seconds)
        {
            if (seconds <= 0) return "ít phút";
            if (seconds < 60) return seconds + " giây";
            return ((seconds + 59) / 60) + " phút";
        }
    }
}
