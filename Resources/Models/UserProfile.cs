using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class UserProfile
    {
        [JsonProperty("profile_image_url")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("wohnort")]
        public string Wohnort { get; set; }

        [JsonProperty("ueber_mich")]
        public string UeberMich { get; set; }

        [JsonProperty("qualifikationen")]
        public string Qualifikationen { get; set; }
    }
}