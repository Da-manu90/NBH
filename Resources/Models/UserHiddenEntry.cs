using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NBH.Resources.Models
{
    [Table("user_hidden_entries")]
    public class UserHiddenEntry : BaseModel
    {
        [PrimaryKey("user_id", false)]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [PrimaryKey("entry_id", false)]
        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Column("hidden_at")]
        public DateTime HiddenAt { get; set; }
    }
}
