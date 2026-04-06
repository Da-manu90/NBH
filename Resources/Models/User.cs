using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id", false)]
        [JsonProperty("id")]
        public Guid Id { get; set; }

        // Falls es Fälle gibt, wo username = null in DB, setz string? 
        [Column("username")]
        [JsonProperty("username")]
        public string? Username { get; set; }

        [Column("email")]
        [JsonProperty("email")]
        public string? Email { get; set; }

        [Column("role")]
        [JsonProperty("role")]
        public string? Role { get; set; }

        // created_at kann in DB "NULL" oder versch. Formate haben
        // => DateTime? beibehalten
        [Column("created_at")]
        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("full_name")]
        [JsonProperty("full_name")]
        public string? FullName { get; set; }

        // AppleUserId -> kann in DB null sein
        [Column("apple_user_id")]
        [JsonProperty("apple_user_id")]
        public string? AppleUserId { get; set; }

        // Häufig JSON-Feld -> lieber string? oder Dictionary
        [Column("raw_user_meta_data")]
        [JsonProperty("raw_user_meta_data")]
        public string? RawUserMetaData { get; set; }

        // profile_image_url => in DB. Falls es null sein kann -> string?
        [Column("profile_image_url")]
        [JsonProperty("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [Column("wohnort")]
        [JsonProperty("wohnort")]
        public string? Wohnort { get; set; }

        [Column("ueber_mich")]
        [JsonProperty("ueber_mich")]
        public string? UeberMich { get; set; }

        [Column("qualifikationen")]
        [JsonProperty("qualifikationen")]
        public string? Qualifikationen { get; set; }

        [Column("subscription_id")]
        [JsonProperty("subscription_id")]
        public string? SubscriptionId { get; set; }

        [Column("is_verified")]
        [JsonProperty("is_verified")]
        public bool IsVerified { get; set; }

        [Column("is_public")]
        [JsonProperty("is_public")]
        public bool IsPublic { get; set; } = true;

        [Column("is_banned")]
        [JsonProperty("is_banned")]
        public bool IsBanned { get; set; }

        [Column("banned_reason")]
        [JsonProperty("banned_reason")]
        public string? BannedReason { get; set; }

        [Column("banned_at")]
        [JsonProperty("banned_at")]
        public DateTime? BannedAt { get; set; }

        // DSGVO-Felder
        [Column("terms_accepted_at")]
        [JsonProperty("terms_accepted_at")]
        public DateTime? TermsAcceptedAt { get; set; }

        [Column("privacy_accepted_at")]
        [JsonProperty("privacy_accepted_at")]
        public DateTime? PrivacyAcceptedAt { get; set; }

        [Column("terms_version")]
        [JsonProperty("terms_version")]
        public string? TermsVersion { get; set; } = "1.0";

        [Column("deletion_requested_at")]
        [JsonProperty("deletion_requested_at")]
        public DateTime? DeletionRequestedAt { get; set; }

        [Column("deletion_scheduled_at")]
        [JsonProperty("deletion_scheduled_at")]
        public DateTime? DeletionScheduledAt { get; set; }
    }
}
