using Newtonsoft.Json;

namespace NBH.Resources.Models
{
    public class EntriesPageRpcResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("entries")]
        public List<EntryItem> Entries { get; set; } = new();
    }
}
