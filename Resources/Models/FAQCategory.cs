using System.Collections.Generic;

namespace NBH.Resources.Models
{
    public class FAQCategory
    {
        public string CategoryTitle { get; set; } = "";
        public List<FAQRow> Items { get; set; } = new();
    }
}
