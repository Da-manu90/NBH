using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    [Table("group_event_participants")]
    public class GroupEventParticipant : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("role_id")]
        public Guid? RoleId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("applied_at")]
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        [Column("confirmed_at")]
        public DateTime? ConfirmedAt { get; set; }

        // -- Nicht-persistente Properties (Join-Daten, e.g. from Users or Roles) --

        [JsonIgnore]
        public string Username { get; set; } = string.Empty;

        [JsonIgnore]
        public string ProfileImageUrl { get; set; } = string.Empty;

        [JsonIgnore]
        public string RoleName { get; set; } = string.Empty;
    }
}
