using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    [Table("group_event_roles")]
    public class GroupEventRole : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Column("role_name")]
        public string RoleName { get; set; } = string.Empty;

        [Column("max_slots")]
        public int MaxSlots { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // -- Nicht-persistente Properties (berechnet, z.B. in ViewModels befüllt) --
        
        [JsonIgnore]
        public int ConfirmedCount { get; set; }

        [JsonIgnore]
        public int PendingCount { get; set; }

        [JsonIgnore]
        public bool IsFull => MaxSlots > 0 && ConfirmedCount >= MaxSlots;

        [JsonIgnore]
        public double FillPercentage => MaxSlots > 0 ? (double)ConfirmedCount / MaxSlots * 100.0 : 0;

        [JsonIgnore]
        public double RoleProgress => MaxSlots > 0 ? Math.Min(1.0, (double)ConfirmedCount / MaxSlots) : 0;
    }
}
