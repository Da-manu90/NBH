using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class RatingPageDetailsResponse
    {
        [JsonProperty("chat")]
        public RatingAverageSection? Chat { get; set; }

        [JsonProperty("event")]
        public RatingAverageSection? Event { get; set; }

        [JsonProperty("comments")]
        public List<RatingCommentItem> Comments { get; set; } = new();

        [JsonProperty("total_comments")]
        public int TotalComments { get; set; }
    }

    public class RatingAverageSection
    {
        [JsonProperty("helpfulness_average")]
        public double HelpfulnessAverage { get; set; }

        [JsonProperty("friendliness_average")]
        public double FriendlinessAverage { get; set; }

        [JsonProperty("response_time_average")]
        public double ResponseTimeAverage { get; set; }

        [JsonProperty("description_accuracy_average")]
        public double DescriptionAccuracyAverage { get; set; }

        [JsonProperty("reliability_average")]
        public double ReliabilityAverage { get; set; }

        [JsonProperty("total_count")]
        public int TotalCount { get; set; }
    }
}
