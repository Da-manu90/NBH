using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NBH.Resources.Models
{
    internal sealed class FlexibleJsonStringConverter : JsonConverter<string?>
    {
        public override string? ReadJson(
            JsonReader reader,
            Type objectType,
            string? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                return reader.Value?.ToString();
            }

            var token = JToken.Load(reader);
            return token.Type == JTokenType.Null
                ? null
                : token.ToString(Formatting.None);
        }

        public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                writer.WriteNull();
                return;
            }

            try
            {
                var token = JToken.Parse(value);
                token.WriteTo(writer);
            }
            catch (JsonReaderException)
            {
                writer.WriteValue(value);
            }
        }
    }
}
