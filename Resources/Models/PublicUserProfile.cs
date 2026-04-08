using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class PublicUserProfile
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [JsonProperty("wohnort")]
        public string? Wohnort { get; set; }

        [JsonProperty("ueber_mich")]
        public string? UeberMich { get; set; }

        [JsonProperty("qualifikationen")]
        public string? Qualifikationen { get; set; }

        [JsonProperty("is_verified")]
        public bool IsVerified { get; set; }

        [JsonProperty("is_public")]
        public bool IsPublic { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
