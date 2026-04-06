using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using System;

[Table("event_rating")]
public class EventRating : BaseModel
{
    [JsonProperty("event_id")]
    [Column("event_id")]
    public Guid event_id { get; set; }

    [JsonProperty("reviewer_id")]
    [Column("reviewer_id")]
    public Guid reviewer_id { get; set; }

    [JsonProperty("reviewed_id")]
    [Column("reviewed_id")]
    public Guid reviewed_id { get; set; }

    [JsonProperty("event_friendliness")]
    [Column("event_friendliness")]
    public int event_friendliness { get; set; }

    [JsonProperty("description_accuracy")]
    [Column("description_accuracy")]
    public int description_accuracy { get; set; }

    [JsonProperty("reliability")]
    [Column("reliability")]
    public int reliability { get; set; }

    [JsonProperty("created_at")]
    [Column("created_at")]
    public DateTime created_at { get; set; }

    [JsonProperty("comment")]
    [Column("comment")]
    public string comment { get; set; }
}
