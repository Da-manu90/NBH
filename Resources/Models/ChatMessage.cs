using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    [Table("chat_messages")]
    public class ChatMessage : BaseModel
    {
        [PrimaryKey("id", false)]
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [Column("chat_id")]
        [JsonProperty("chat_id")]
        public Guid ChatId { get; set; }

        [Column("sender_id")]
        [JsonProperty("sender_id")]
        public Guid SenderId { get; set; }

        [Column("message")]
        [JsonProperty("message")]
        public string? Message { get; set; }

        [Column("created_at")]
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
