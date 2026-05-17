using System.Text.Json;
using System.Text.Json.Serialization;

namespace VitaMR.Services;

public sealed class FlexibleStringListJsonConverter : JsonConverter<List<string>>
{
    public override List<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => ReadArray(ref reader),
            JsonTokenType.String => ReadSingle(reader.GetString()),
            JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False => ReadSingle(JsonDocument.ParseValue(ref reader).RootElement.ToString()),
            JsonTokenType.StartObject => ReadSingle(JsonDocument.ParseValue(ref reader).RootElement.GetRawText()),
            JsonTokenType.Null => [],
            _ => []
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<string> value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }

    private static List<string> ReadArray(ref Utf8JsonReader reader)
    {
        var values = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return values;
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    AddIfPresent(values, reader.GetString());
                    break;
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    AddIfPresent(values, JsonDocument.ParseValue(ref reader).RootElement.GetRawText());
                    break;
            }
        }

        return values;
    }

    private static List<string> ReadSingle(string? value)
    {
        var values = new List<string>();
        AddIfPresent(values, value);
        return values;
    }

    private static void AddIfPresent(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value.Trim());
        }
    }
}
