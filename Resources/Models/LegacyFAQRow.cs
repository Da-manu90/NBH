using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NBH.Resources.Models
{
    [Table("faqs")]
    public class LegacyFAQRow : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("question")]
        public string? Question { get; set; }

        [Column("answer")]
        public string? Answer { get; set; }

        [Column("category")]
        public string? Category { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
