using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// LƯU TÀI KHOẢN cho nút "Tiếp tục" (ghi nhớ đăng nhập).
    ///
    /// Mật khẩu KHÔNG lưu dạng chữ thường nữa: mã hoá AES-256 bằng khoá sinh từ MÃ THIẾT BỊ
    /// (SystemInfo.deviceUniqueIdentifier) → người khác mở registry (PC quán net) / chép file cài đặt sang máy khác
    /// chỉ thấy chuỗi rác. Kèm HMAC để phát hiện dữ liệu bị sửa / chép từ máy khác (→ coi như chưa lưu).
    /// Giới hạn (nói thật): phần mềm độc chạy trên CHÍNH máy đó vẫn giải mã được — đây là chống xem trộm, không phải két sắt.
    ///
    /// PlayerPrefs dùng: SavedUser (tên), SavedPassEnc (mật khẩu đã mã hoá), RememberLogin (1/0, mặc định 1).
    /// Bản cũ lưu "SavedPass" dạng chữ thường → tự chuyển sang mã hoá và xoá ở lần đọc đầu tiên.
    /// </summary>
    public static class SavedLogin
    {
        private const string KeyUser = "SavedUser";
        private const string KeyPass = "SavedPassEnc";
        private const string KeyOldPlain = "SavedPass";
        private const string KeyRemember = "RememberLogin";
        private const string KeyInstallSecret = "SavedLoginSecret";
        private const string Prefix = "v1:";

        /// <summary>Có ghi nhớ đăng nhập không (mặc định BẬT). Tắt → xoá luôn tài khoản đã lưu.</summary>
        public static bool Remember
        {
            get => PlayerPrefs.GetInt(KeyRemember, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(KeyRemember, value ? 1 : 0);
                if (!value) Clear();
                PlayerPrefs.Save();
            }
        }

        public static string User => PlayerPrefs.GetString(KeyUser, "");
        public static bool HasSaved => !string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(LoadPassword());

        /// <summary>Gọi khi đăng nhập thành công. Không ghi nhớ → không lưu gì.</summary>
        public static void Save(string user, string password)
        {
            if (!Remember || string.IsNullOrEmpty(user)) { Clear(); return; }
            PlayerPrefs.SetString(KeyUser, user);
            PlayerPrefs.SetString(KeyPass, Encrypt(password ?? ""));
            PlayerPrefs.DeleteKey(KeyOldPlain);
            PlayerPrefs.Save();
        }

        /// <summary>Mật khẩu đã lưu, "" nếu chưa lưu / giải mã không được (máy khác, dữ liệu hỏng).</summary>
        public static string LoadPassword()
        {
            if (PlayerPrefs.HasKey(KeyOldPlain)) // bản cũ: chữ thường → mã hoá lại rồi xoá
            {
                string old = PlayerPrefs.GetString(KeyOldPlain, "");
                PlayerPrefs.DeleteKey(KeyOldPlain);
                if (!string.IsNullOrEmpty(old)) PlayerPrefs.SetString(KeyPass, Encrypt(old));
                PlayerPrefs.Save();
            }
            return Decrypt(PlayerPrefs.GetString(KeyPass, "")) ?? "";
        }

        /// <summary>Xoá tài khoản đã lưu (nút "Đổi tài khoản", tắt ghi nhớ, mật khẩu đã lưu bị sai).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyUser);
            PlayerPrefs.DeleteKey(KeyPass);
            PlayerPrefs.DeleteKey(KeyOldPlain);
            PlayerPrefs.Save();
        }

        /// <summary>Chỉ xoá mật khẩu, giữ tên (mật khẩu lưu bị sai → điền sẵn tên cho người chơi gõ lại).</summary>
        public static void ForgetPassword()
        {
            PlayerPrefs.DeleteKey(KeyPass);
            PlayerPrefs.DeleteKey(KeyOldPlain);
            PlayerPrefs.Save();
        }

        // ===================== Mã hoá =====================

        private static byte[] _encKey, _macKey;

        /// <summary>Khoá AES + khoá HMAC = SHA-256(mã thiết bị + tên game + nhãn). Máy khác → khoá khác.</summary>
        private static void EnsureKeys()
        {
            if (_encKey != null) return;
            string device = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(device) || device == SystemInfo.unsupportedIdentifier)
            {
                // Thiết bị không cho mã máy → dùng 1 chuỗi ngẫu nhiên sinh lần đầu cài (vẫn hơn chữ thường)
                device = PlayerPrefs.GetString(KeyInstallSecret, "");
                if (string.IsNullOrEmpty(device))
                {
                    device = Convert.ToBase64String(RandomBytes(32));
                    PlayerPrefs.SetString(KeyInstallSecret, device);
                }
            }
            string seed = device + "|" + Application.identifier + "|NinjaOnline.SavedLogin";
            using (var sha = SHA256.Create())
            {
                _encKey = sha.ComputeHash(Encoding.UTF8.GetBytes("enc|" + seed));
                _macKey = sha.ComputeHash(Encoding.UTF8.GetBytes("mac|" + seed));
            }
        }

        private static string Encrypt(string plain)
        {
            EnsureKeys();
            using (var aes = Aes.Create())
            {
                aes.Key = _encKey;
                aes.GenerateIV(); // IV ngẫu nhiên mỗi lần → cùng mật khẩu cũng ra chuỗi khác nhau
                byte[] data = Encoding.UTF8.GetBytes(plain);
                byte[] cipher;
                using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(data, 0, data.Length);

                byte[] body = new byte[16 + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, body, 0, 16);
                Buffer.BlockCopy(cipher, 0, body, 16, cipher.Length);
                byte[] mac;
                using (var h = new HMACSHA256(_macKey)) mac = h.ComputeHash(body);

                byte[] all = new byte[body.Length + mac.Length];
                Buffer.BlockCopy(body, 0, all, 0, body.Length);
                Buffer.BlockCopy(mac, 0, all, body.Length, mac.Length);
                return Prefix + Convert.ToBase64String(all);
            }
        }

        private static string Decrypt(string stored)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix)) return null;
            try
            {
                EnsureKeys();
                byte[] all = Convert.FromBase64String(stored.Substring(Prefix.Length));
                if (all.Length < 16 + 16 + 32) return null;
                int bodyLen = all.Length - 32;
                byte[] mac;
                using (var h = new HMACSHA256(_macKey)) mac = h.ComputeHash(all, 0, bodyLen);
                int diff = 0; // so sánh hết các byte (không dừng sớm)
                for (int i = 0; i < 32; i++) diff |= mac[i] ^ all[bodyLen + i];
                if (diff != 0) return null; // máy khác / bị sửa

                using (var aes = Aes.Create())
                {
                    aes.Key = _encKey;
                    byte[] iv = new byte[16];
                    Buffer.BlockCopy(all, 0, iv, 0, 16);
                    aes.IV = iv;
                    using (var dec = aes.CreateDecryptor())
                        return Encoding.UTF8.GetString(dec.TransformFinalBlock(all, 16, bodyLen - 16));
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] RandomBytes(int n)
        {
            byte[] b = new byte[n];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return b;
        }

        /// <summary>Cho AutoTestRunner: chuỗi đang lưu có phải dạng mã hoá (không chứa mật khẩu thô) không.</summary>
        public static bool StoredIsEncrypted(string plain)
        {
            string s = PlayerPrefs.GetString(KeyPass, "");
            return s.StartsWith(Prefix) && !s.Contains(plain) && !PlayerPrefs.HasKey(KeyOldPlain);
        }
    }
}
