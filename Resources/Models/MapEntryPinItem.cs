using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class MapEntryPinItem
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("location")]
        public string? Location { get; set; }

        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }

        [JsonProperty("event_type")]
        public string? EventType { get; set; }

        [JsonProperty("user_id")]
        public Guid UserId { get; set; }
    }
}
