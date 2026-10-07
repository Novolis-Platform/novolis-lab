using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>JSON persistence of the canonical visual AST.</summary>
public static class VisualJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new Rgba32Converter() },
    };

    public static string Serialize(VisualDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static string SerializeDiagnostics(IReadOnlyList<CompileDiagnostic> diagnostics) =>
        JsonSerializer.Serialize(diagnostics, Options);

    public static VisualDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<VisualDocument>(json, Options) ?? VisualDocument.Empty;

    private sealed class Rgba32Converter : JsonConverter<Rgba32>
    {
        public override Rgba32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString() ?? "#000000";
                return VisualColors.TryParse(text, out var color) ? color : Rgba32.Black;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
                return Rgba32.Black;
            byte r = 0, g = 0, b = 0, a = 255;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;
                var name = reader.GetString();
                reader.Read();
                var v = (byte)reader.GetInt32();
                if (name is "r" or "R") r = v;
                else if (name is "g" or "G") g = v;
                else if (name is "b" or "B") b = v;
                else if (name is "a" or "A") a = v;
            }

            return new Rgba32(r, g, b, a);
        }

        public override void Write(Utf8JsonWriter writer, Rgba32 value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.R:x2}{value.G:x2}{value.B:x2}");
        }
    }
}
