using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Models;

/// <summary>JSON options for Calypso .cadjson / sidecar interchange.</summary>
internal static class CadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}
