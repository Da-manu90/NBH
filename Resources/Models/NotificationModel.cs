using Supabase.Postgrest.Models;
using System;
using System.Text.Json.Serialization;
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
        public string ActionData { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool HasRatingButton
        {
            get
            {
                if (!string.IsNullOrEmpty(ActionData))
                {
                    try
                    {
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(ActionData);
                        return jObj["hasRatingButton"]?.Value<bool>() ?? false;
                    }
                    catch { }
                }
                return false;
            }
        }
    }
}
