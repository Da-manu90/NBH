using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using System;

namespace NBH.Resources.Models
{
    [Table("chats")]
    public class PublicChat : BaseModel
    {
        [JsonProperty("id")]
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [JsonProperty("user_id")]
        [Column("user_id")]
        public string UserId { get; set; }

        [JsonProperty("partner_id")]
        [Column("partner_id")]
        public string PartnerId { get; set; }

        [JsonProperty("last_message")]
        [Column("last_message")]
        public string LastMessage { get; set; }

        [JsonProperty("last_message_date")]
        [Column("last_message_date")]
        public DateTime? LastMessageDate { get; set; }

        [JsonProperty("created_at")]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Neue Spalten in DB: user_last_read_at, partner_last_read_at (nullable)
        [JsonProperty("user_last_read_at")]
        [Column("user_last_read_at")]
        public DateTime? UserLastReadAt { get; set; }

        [JsonProperty("partner_last_read_at")]
        [Column("partner_last_read_at")]
        public DateTime? PartnerLastReadAt { get; set; }

        [JsonProperty("archived_by_user")]
        [Column("archived_by_user")]
        public bool ArchivedByUser { get; set; }

        [JsonProperty("archived_by_partner")]
        [Column("archived_by_partner")]
        public bool ArchivedByPartner { get; set; }

        // Felder nur fürs UI (nicht in DB)
        [JsonProperty("partner_name")]
        public string PartnerName { get; set; }

        [JsonProperty("partner_image")]
        public string PartnerImageUrl { get; set; }

        // NEU: Flag zum Bestimmen, ob Chat ungelesen ist
        [JsonProperty("is_unread")]
        public bool IsUnread { get; set; }
    }
}
