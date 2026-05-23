using System;
using Newtonsoft.Json;

namespace Assets.Script.Models
{
    [System.Serializable]
    public class CreateCharacterResponse : BaseResponse
    {
        // Vì đã kế thừa BaseResponse, class này tự động có sẵn 'success' và 'message'

        // Hứng object player từ Server gửi về khi tạo thành công
        [JsonProperty("player")]
        public PlayerModel Player { get; set; }
    }
}