using Supabase.Postgrest.Models;
using System;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NBH.Resources.Models
{
    [Table("notifications")]
    public class NotificationModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("type")]
        public string Type { get; set; }

        [Column("message")]
        public string Message { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("read")]
        public bool Read { get; set; }

        [Column("action_data")]
        [JsonConverter(typeof(FlexibleJsonStringConverter))]
        public string? ActionData { get; set; }

        [JsonIgnore]
        public JObject? ActionDataObject
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ActionData))
                {
                    return null;
                }

                try
                {
                    return JObject.Parse(ActionData);
                }
                catch
                {
                    return null;
                }
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public bool HasRatingButton
        {
            get
            {
                var jObj = ActionDataObject;
                return jObj?["hasRatingButton"]?.Value<bool>()
                    ?? jObj?["HasRatingButton"]?.Value<bool>()
                    ?? false;
            }
        }
    }
}
