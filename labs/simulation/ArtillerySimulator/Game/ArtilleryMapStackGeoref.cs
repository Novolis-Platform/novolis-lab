using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtillerySimulator.Game;

/// <summary>Loads <c>map/kristiansand-map-stack.json</c> for north-up, marker-aligned layer georef.</summary>
internal static class ArtilleryMapStackGeoref
{
    private static bool _loaded;
    private static float _eastMeters;
    private static float _northMeters;
    private static double _southLat;
    private static double _northLat;
    private static double _westLon;
    private static double _eastLon;
    private static int _reliefWidth;
    private static int _reliefHeight;
    private static bool _hasRelief;
    private static bool _hasWgs84;

    public static bool TryGetReliefGeoref(out float eastMeters, out float northMeters, out int width, out int height)
    {
        EnsureLoaded();
        eastMeters = _eastMeters;
        northMeters = _northMeters;
        width = _reliefWidth;
        height = _reliefHeight;
        return _hasRelief;
    }

    public static bool TryGetScenarioMeters(out float eastMeters, out float northMeters)
    {
        EnsureLoaded();
        eastMeters = _eastMeters;
        northMeters = _northMeters;
        return _hasRelief && _eastMeters > 0 && _northMeters > 0;
    }

    public static bool TryProjectWgs84(double latitudeDegrees, double longitudeDegrees, out float xMeters, out float zMeters)
    {
        EnsureLoaded();
        if (!_hasWgs84 || _eastMeters <= 0 || _northMeters <= 0)
        {
            xMeters = zMeters = 0;
            return false;
        }

        var lonSpan = _eastLon - _westLon;
        var latSpan = _northLat - _southLat;
        if (lonSpan <= 0 || latSpan <= 0)
        {
            xMeters = zMeters = 0;
            return false;
        }

        xMeters = (float)((longitudeDegrees - _westLon) / lonSpan * _eastMeters);
        zMeters = (float)((latitudeDegrees - _southLat) / latSpan * _northMeters);
        return true;
    }

    public static bool TryWorldToReliefPixel(float xMeters, float zMeters, out int ix, out int iy)
    {
        EnsureLoaded();
        ix = iy = 0;
        if (!_hasRelief || _reliefWidth < 2 || _reliefHeight < 2)
            return false;

        if (xMeters < 0f || zMeters < 0f || xMeters > _eastMeters || zMeters > _northMeters)
            return false;

        var u = xMeters / _eastMeters;
        var v = zMeters / _northMeters;
        ix = (int)MathF.Round(u * (_reliefWidth - 1));
        iy = (int)MathF.Round((1f - v) * (_reliefHeight - 1));
        return ix >= 0 && iy >= 0 && ix < _reliefWidth && iy < _reliefHeight;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;

        var path = Path.Combine(AppContext.BaseDirectory, "map", "kristiansand-map-stack.json");
        if (!File.Exists(path))
            return;

        try
        {
            var doc = JsonSerializer.Deserialize<MapStackFile>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (doc?.World is null)
                return;

            _eastMeters = (float)doc.World.EastMeters;
            _northMeters = (float)doc.World.NorthMeters;
            if (doc.World.Wgs84 is { } wgs)
            {
                _southLat = wgs.South;
                _northLat = wgs.North;
                _westLon = wgs.West;
                _eastLon = wgs.East;
                _hasWgs84 = wgs.East > wgs.West && wgs.North > wgs.South;
            }

            var relief = doc.Layers?.FirstOrDefault(l => string.Equals(l.Id, "relief", StringComparison.OrdinalIgnoreCase));
            if (relief is null)
                return;

            _reliefWidth = relief.PixelWidth;
            _reliefHeight = relief.PixelHeight;
            _hasRelief = _reliefWidth > 0 && _reliefHeight > 0 && _eastMeters > 0 && _northMeters > 0;
        }
        catch
        {
            _hasRelief = false;
        }
    }

    private sealed class MapStackFile
    {
        [JsonPropertyName("world")]
        public WorldBlock? World { get; set; }

        [JsonPropertyName("layers")]
        public List<LayerBlock>? Layers { get; set; }
    }

    private sealed class WorldBlock
    {
        [JsonPropertyName("eastMeters")]
        public double EastMeters { get; set; }

        [JsonPropertyName("northMeters")]
        public double NorthMeters { get; set; }

        [JsonPropertyName("wgs84")]
        public Wgs84Block? Wgs84 { get; set; }
    }

    private sealed class Wgs84Block
    {
        [JsonPropertyName("south")]
        public double South { get; set; }

        [JsonPropertyName("north")]
        public double North { get; set; }

        [JsonPropertyName("west")]
        public double West { get; set; }

        [JsonPropertyName("east")]
        public double East { get; set; }
    }

    private sealed class LayerBlock
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("pixelWidth")]
        public int PixelWidth { get; set; }

        [JsonPropertyName("pixelHeight")]
        public int PixelHeight { get; set; }
    }
}
