using System;
using Newtonsoft.Json;

namespace Assets.Script.Models
{
    [System.Serializable]
    public class PlayerModel
    {
        // Dùng JsonProperty để map chính xác tên trường từ JSON của Server
        // Dùng { get; set; } để đóng gói dữ liệu an toàn

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("account_id")]
        public int AccountId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("class_type")]
        public short ClassType { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("exp")]
        public long Exp { get; set; }

        [JsonProperty("yen")]
        public int Yen { get; set; }

        [JsonProperty("luong")]
        public int Luong { get; set; }

        // --- Chỉ số sinh tồn ---
        [JsonProperty("hp")]
        public int MaxHp { get; set; }          // Lưu ý: Đổi tên thành MaxHp cho rõ nghĩa

        [JsonProperty("current_hp")]
        public int CurrentHp { get; set; }

        [JsonProperty("mp")]
        public int MaxMp { get; set; }

        [JsonProperty("current_mp")]
        public int CurrentMp { get; set; }

        // --- Vị trí trong thế giới ---
        [JsonProperty("map_id")]
        public int MapId { get; set; }

        [JsonProperty("zoneId")]
        public int ZoneId { get; set; }

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        // --- Dữ liệu mảng/chuỗi phức tạp ---
        [JsonProperty("inventory")]
        public string Inventory { get; set; }

        [JsonProperty("equipment")]
        public string Equipment { get; set; }

        [JsonProperty("skills")]
        public string Skills { get; set; }

        [JsonProperty("quest")]
        public string Quest { get; set; }

        [JsonProperty("settings")]
        public string Settings { get; set; }
    }
}