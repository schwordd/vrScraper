using Newtonsoft.Json;

namespace vrScraper.Misc
{
  public class TimeSpanSecondsConverter : JsonConverter<TimeSpan?>
  {
    public override void WriteJson(JsonWriter writer, TimeSpan? value, JsonSerializer serializer)
    {
      writer.WriteValue(value?.TotalSeconds);
    }

    public override TimeSpan? ReadJson(JsonReader reader, Type objectType, TimeSpan? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
      if (reader.TokenType == JsonToken.Null)
      {
        return null;
      }

      if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
      {
        double seconds = Convert.ToDouble(reader.Value);
        return TimeSpan.FromSeconds(seconds);
      }

      throw new JsonSerializationException($"Unexpected token type: {reader.TokenType}");
    }
  }
}
