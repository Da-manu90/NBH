using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;

namespace NBH.Resources.Models
{
    public class NullOrDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? ReadJson(JsonReader reader, Type objectType, DateTime? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Null:
                        return null;

                    case JsonToken.Date:
                        return serializer.Deserialize<DateTime?>(reader);

                    case JsonToken.String:
                        if (DateTime.TryParse(reader.Value?.ToString(), out var dt))
                            return dt;
                        return null;

                    case JsonToken.StartArray:
                        Debug.WriteLine("⚠️ JSON-Array für DateTime erkannt - wird ignoriert.");
                        reader.Skip(); // ❗ Stellt sicher, dass JSON-Arrays keine Fehler verursachen
                        return null;

                    default:
                        Debug.WriteLine($"⚠️ Unbekanntes JSON-Token für DateTime: {reader.TokenType}");
                        reader.Skip();
                        return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Fehler in NullOrDateTimeConverter: {ex.Message}");
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, DateTime? value, JsonSerializer serializer)
        {
            if (value.HasValue)
                writer.WriteValue(value.Value.ToString("o")); // ISO 8601 Format für Konsistenz
            else
                writer.WriteNull();
        }
    }
}
