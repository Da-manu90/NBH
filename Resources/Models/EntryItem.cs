using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using NBH.Resources.Models;  //

namespace NBH.Resources.Models
{
    [Table("entries")] // Name der Tabelle in der Datenbank
    public class EntryItem : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid id { get; set; }

        [Column("title")]
        public string title { get; set; } = string.Empty;

        [Column("description")]
        public string description { get; set; } = string.Empty;

        [Column("category")]
        public string category { get; set; } = "Allgemein";

        [Column("location")]
        public string? location { get; set; }

        [Column("created_date")]
        public DateTime created_date { get; set; } = DateTime.Now;

        [Column("user_id")]
        public string user_id { get; set; } = string.Empty;

        // **Neu hinzugefügte Spalten für Events & Bewertungen**
        [Column("event_partner_id")]
        public Guid? event_partner_id { get; set; }

        [Column("is_confirmed")]
        public bool is_confirmed { get; set; } = false;

        [Column("auto_complete_at")]
        [JsonConverter(typeof(NBH.Resources.Models.NullOrDateTimeConverter))]
        public DateTime? auto_complete_at { get; set; }

        // **** NEU HINZUGEFÜGT: Status für "abgeschlossen" (Archiv / nicht mehr anzeigen) ****
        [Column("is_completed")]
        public bool is_completed { get; set; } = false;

        // **Neue Spalten für Gruppen-Events**
        [Column("event_type")]
        public string event_type { get; set; } = "single";

        [Column("max_participants")]
        public int? max_participants { get; set; }

        [Column("event_date")]
        [JsonConverter(typeof(NBH.Resources.Models.NullOrDateTimeConverter))]
        public DateTime? event_date { get; set; }

        [Column("event_end_date")]
        [JsonConverter(typeof(NBH.Resources.Models.NullOrDateTimeConverter))]
        public DateTime? event_end_date { get; set; }

        [Column("event_image_url")]
        public string? event_image_url { get; set; }

        // -- Nicht-persistente Listen und Eigenschaften (nach dem Laden befüllt) --
        
        [JsonIgnore]
        public List<GroupEventRole> Roles { get; set; } = new();

        [JsonIgnore]
        public List<GroupEventParticipant> Participants { get; set; } = new();

        [JsonIgnore]
        public int ConfirmedParticipantCount { get; set; }

        [JsonIgnore]
        public double GeneralFillPercentage => max_participants > 0 ? ((double)ConfirmedParticipantCount / max_participants.Value) * 100.0 : 0;

        [JsonIgnore]
        public bool IsGroupEvent => event_type == "group";

        [JsonIgnore]
        public bool HasRoles => Roles?.Count > 0;
    }
}
