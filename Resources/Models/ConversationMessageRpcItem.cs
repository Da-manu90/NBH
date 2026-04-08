using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class ConversationMessageRpcItem
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("conversation_id")]
        public Guid ConversationId { get; set; }

        [JsonProperty("sender_id")]
        public Guid SenderId { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
