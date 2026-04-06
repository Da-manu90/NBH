using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    [Table("reports")]
    public class Report : BaseModel
    {
        [PrimaryKey("id", false)]
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [Column("reporter_id")]
        [JsonProperty("reporter_id")]
        public Guid ReporterId { get; set; }

        [Column("reported_user_id")]
        [JsonProperty("reported_user_id")]
        public Guid? ReportedUserId { get; set; }

        [Column("reported_entry_id")]
        [JsonProperty("reported_entry_id")]
        public Guid? ReportedEntryId { get; set; }

        [Column("reason")]
        [JsonProperty("reason")]
        public string Reason { get; set; } = "";

        [Column("description")]
        [JsonProperty("description")]
        public string? Description { get; set; }

        [Column("status")]
        [JsonProperty("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
