using Newtonsoft.Json;

namespace Assets.Script.Models
{
    [System.Serializable]
    public class PlayerSkillData
    {
       
    //    [JsonProperty("skillId")]
        public int templateId { get; set; }

        [JsonProperty("point")]
        public int point { get; set; }
    }
}