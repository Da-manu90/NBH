using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class ConversationListRpcItem
    {
        [JsonProperty("conversation_id")]
        public Guid ConversationId { get; set; }

        [JsonProperty("conversation_type")]
        public string? ConversationType { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("partner_id")]
        public Guid? PartnerId { get; set; }

        [JsonProperty("partner_name")]
        public string? PartnerName { get; set; }

        [JsonProperty("partner_image")]
        public string? PartnerImage { get; set; }

        [JsonProperty("member_count")]
        public int MemberCount { get; set; }

        [JsonProperty("last_message")]
        public string? LastMessage { get; set; }

        [JsonProperty("last_message_date")]
        public DateTime? LastMessageDate { get; set; }

        [JsonProperty("is_unread")]
        public bool IsUnread { get; set; }
    }
}
