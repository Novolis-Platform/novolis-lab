using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Astro.Abstractions;
using Novolis.Astro.Catalog;

namespace NearSolPolity;

/// <summary>Loads the embedded Johnston-based near-Sol catalog (100 systems).</summary>
internal static class NearSolCatalog
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  public static StarCatalog Load()
  {
    using var stream = typeof(NearSolCatalog).Assembly
      .GetManifestResourceStream("NearSolPolity.nearsol-100.json")
      ?? throw new InvalidOperationException("Embedded nearsol-100.json missing.");
    var entries = JsonSerializer.Deserialize<List<StarEntry>>(stream, JsonOptions)
      ?? throw new InvalidOperationException("Failed to parse nearsol-100.json.");

    var catalog = new StarCatalog();
    foreach (var e in entries.OrderBy(x => Dist(x)).ThenBy(x => x.Id, StringComparer.Ordinal))
    {
      var spectral = Enum.TryParse<SpectralClass>(e.Spectral, ignoreCase: true, out var sc)
        ? sc
        : SpectralClass.Unknown;
      catalog.Add(new StarSystem(e.Id, e.Name, new StarCoords(e.X, e.Y, e.Z), spectral, e.Tags ?? []));
    }

    if (!catalog.TryGet("sol", out _))
    {
      throw new InvalidOperationException("Catalog must include sol.");
    }

    return catalog;
  }

  private static double Dist(StarEntry e) => Math.Sqrt(e.X * e.X + e.Y * e.Y + e.Z * e.Z);

  private sealed class StarEntry
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    [JsonPropertyName("spectral")]
    public string Spectral { get; set; } = "Unknown";

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
  }
}
