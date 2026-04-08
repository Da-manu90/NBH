using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class ChatListRpcItem
    {
        [JsonProperty("chat_id")]
        public Guid ChatId { get; set; }

        [JsonProperty("partner_id")]
        public Guid PartnerId { get; set; }

        [JsonProperty("partner_name")]
        public string? PartnerName { get; set; }

        [JsonProperty("partner_image")]
        public string? PartnerImage { get; set; }

        [JsonProperty("last_message")]
        public string? LastMessage { get; set; }

        [JsonProperty("last_message_date")]
        public DateTime? LastMessageDate { get; set; }

        [JsonProperty("is_unread")]
        public bool IsUnread { get; set; }

        [JsonProperty("archived_by_user")]
        public bool ArchivedByUser { get; set; }

        [JsonProperty("archived_by_partner")]
        public bool ArchivedByPartner { get; set; }
    }
}
