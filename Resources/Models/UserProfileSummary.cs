using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class UserProfileSummary
    {
        [JsonProperty("user")]
        public UserProfileSummaryUser? User { get; set; }

        [JsonProperty("rating")]
        public RatingStats? Rating { get; set; }

        [JsonProperty("entries_created")]
        public int EntriesCreated { get; set; }

        [JsonProperty("entries_participated")]
        public int EntriesParticipated { get; set; }
    }

    public class UserProfileSummaryUser
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

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
