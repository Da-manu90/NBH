using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using System;

namespace NBH.Resources.Models
{
    [Table("chat_reviews")]
    public class ChatReview : BaseModel
    {
        [JsonProperty("id")]
        [PrimaryKey("id")]
        public Guid id { get; set; }

        // Neue Eigenschaft zur Zuordnung der Bewertung zu einem Chat
        [JsonProperty("chat_id")]
        [Column("chat_id")]
        public Guid chat_id { get; set; }

        [JsonProperty("reviewer_id")]
        [Column("reviewer_id")]
        public Guid reviewer_id { get; set; }

        [JsonProperty("reviewed_id")]
        [Column("reviewed_id")]
        public Guid reviewed_id { get; set; }

        [JsonProperty("friendliness")]
        [Column("friendliness")]
        public int friendliness { get; set; }

        [JsonProperty("response_time")]
        [Column("response_time")]
        public int response_time { get; set; }

        [JsonProperty("helpfulness")]
        [Column("helpfulness")]
        public int helpfulness { get; set; }

        [JsonProperty("created_at")]
        [Column("created_at")]
        public DateTime created_at { get; set; }
    }
}



