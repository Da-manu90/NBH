using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using System;
using System.Text;

namespace NBH.Resources.Models
{
    [Table("faq")]
    public class FAQRow : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("category_title")]
        public string CategoryTitle { get; set; } = string.Empty;

        [Column("subtitle")]
        public string Subtitle { get; set; } = string.Empty;

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("image_url")]
        public string ImageUrl { get; set; } = string.Empty;

        public string RenderMarkdown
        {
            get
            {
                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(Content))
                {
                    builder.AppendLine(Content.Trim());
                }

                if (!string.IsNullOrWhiteSpace(ImageUrl) &&
                    !Content.Contains(ImageUrl, StringComparison.OrdinalIgnoreCase))
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine($"![FAQ Bild]({ImageUrl.Trim()})");
                }

                return builder.ToString();
            }
        }
    }
}
