using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using System;

namespace NBH.Resources.Models
{
    [Table("faq")]
    public class FAQRow : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("category_title")]
        public string CategoryTitle { get; set; }

        [Column("subtitle")]
        public string Subtitle { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("image_url")]
        public string ImageUrl { get; set; }
    }
}
