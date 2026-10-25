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
            2 => "Tài khoản từ 4 ký tự và mật khẩu không được để trống.",
            3 => extra == 0 ? "Sai mật khẩu 5 lần. Tài khoản tạm khoá đăng nhập trên máy này 5 phút."
                : extra > 0 ? $"Sai tài khoản hoặc mật khẩu. Còn {extra} lần thử."
                : "Sai tài khoản hoặc mật khẩu",
            4 => "Tài khoản đang đăng nhập ở nơi khác",
            5 => KickedByOtherLogin,
            6 => "Tài khoản đã bị khoá. Liên hệ quản trị viên.",
            7 => "Máy chủ đã đầy. Vui lòng thử lại sau ít phút.",
            8 => $"Mạng của bạn đăng nhập quá nhiều lần. Đợi {WaitText(extra)} rồi thử lại.",
            9 => $"Sai mật khẩu quá 5 lần. Đợi {WaitText(extra)} rồi thử lại.",
            10 => $"Tài khoản vừa đăng nhập. Đợi {WaitText(extra)} rồi thử lại.",
            11 => "Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!",
            12 => string.IsNullOrEmpty(note) ? "Máy chủ đang bảo trì. Vui lòng quay lại sau." : note,
            _ => "Lỗi máy chủ!"
        };

        /// <summary>Phiên đang chơi bị đá vì tài khoản vừa đăng nhập ở máy khác (LOGIN mã 5 gửi cho phiên cũ).</summary>
        public const string KickedByOtherLogin = "Tài khoản vừa đăng nhập ở nơi khác. Bạn đã bị đưa ra khỏi game.";

        /// <summary>REGISTER (status ≠ 0). {extra}: số giây phải đợi (mã 4), -1 nếu server không gửi.</summary>
        public static string Register(short status, int extra = -1) => status switch
        {
            1 => "Tài khoản 4–32 ký tự (chữ không dấu, số, _), mật khẩu 6–64 ký tự.",
            2 => "Tài khoản hoặc email đã được dùng!",
            3 => "Email không hợp lệ.",
            4 => $"Mạng của bạn thử quá nhiều lần. Đợi {WaitText(extra)} rồi thử lại.",
            5 => "Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!",
            6 => "Máy chủ đang bảo trì, tạm chưa đăng ký được. Vui lòng quay lại sau.",
            _ => "Lỗi máy chủ!"
        };

        /// <summary>
        /// Kiểm form đăng ký ở máy trước khi gửi (luật khớp RegisterHandler server). Trả null = hợp lệ, khác = lời báo lỗi.
        /// </summary>
        public static string CheckRegisterForm(string user, string pass, string confirm, string email)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(email))
                return "Vui lòng điền đầy đủ thông tin!";
            if (user.Length < 4 || user.Length > 32 || !System.Text.RegularExpressions.Regex.IsMatch(user, "^[A-Za-z0-9_]+$"))
                return "Tài khoản 4–32 ký tự, chỉ gồm chữ không dấu, số và _.";
            if (pass.Length < 6 || pass.Length > 64) return "Mật khẩu 6–64 ký tự.";
            if (pass != confirm) return "Mật khẩu nhập lại không khớp.";
            if (email.Length > 100 || !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                return "Email không hợp lệ.";
            return null;
        }

        public static string CreateCharacter(short status) => status switch
        {
            1 => "Tên nhân vật phải từ 3 đến 15 ký tự.",
            2 => "Tên nhân vật đã có người dùng!",
            3 => "Phiên đăng nhập đã hết, vui lòng đăng nhập lại.",
            4 => "Hệ phái không hợp lệ.",
            5 => "Tài khoản đã có nhân vật.",
            6 => "Tên chỉ gồm chữ, số, dấu cách, _ và không chứa từ không phù hợp.",
            _ => "Lỗi tạo nhân vật!"
        };

        /// <summary>CHANGE_PASSWORD (status ≠ 0). {extra}: số lần còn thử (mã 2) / số giây phải đợi (mã 3).</summary>
        public static string ChangePassword(short status, int extra) => status switch
        {
            1 => "Mật khẩu mới 6–64 ký tự và khác mật khẩu cũ.",
            2 => extra > 0 ? $"Sai mật khẩu cũ. Còn {extra} lần thử." : "Sai mật khẩu cũ 5 lần. Đợi 5 phút rồi thử lại.",
            3 => $"Sai quá nhiều lần. Đợi {WaitText(extra)} rồi thử lại.",
            _ => "Máy chủ bận, thử lại sau."
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
