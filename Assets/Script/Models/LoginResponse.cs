using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Assets.Script.Models
{
    [System.Serializable]
    public class LoginResponse
    {
        public bool success;

        public string message;

        // Dùng JsonProperty để map chính xác key "account_id" từ JSON của Server

        public bool hasCharacter;
        // Đối tượng chứa thông tin nhân vật (sẽ null nếu hasCharacter = false)
        public PlayerModel player;
    }
}
