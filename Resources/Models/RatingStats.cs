using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class RatingStats
    {
        [JsonProperty("final_rating")]
        public double FinalRating { get; set; }

        [JsonProperty("total_count")]
        public int TotalCount { get; set; }
        
        [JsonProperty("chat_avg")]
        public double ChatAvg { get; set; }
        
        [JsonProperty("event_avg")]
        public double EventAvg { get; set; }
    }
}
