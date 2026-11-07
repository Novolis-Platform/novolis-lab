using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtillerySimulator;

/// <summary>
/// Startup camera matched to a Kommunekart 3D share link.
/// URL: x=lat, y=lon, z=ellipsoidal eye height; head/pitch/roll = Cesium camera HPR (degrees).
/// SPA: <c>Cartesian3.fromDegrees(y, x, z)</c>.
/// </summary>
internal static class Kommunekart3DView
{
    public const string DefaultUrl =
        "https://3d.kommunekart.com/?x=58.1467205326313&y=7.956351060738769&z=717.6761064074427&head=96.78343556922412&pitch=-20.987984196512883&roll=0.0007120414865100719";

    public static void ApplyStartupCamera(ArtilleryCameras camera, TerrainWorld terrain)
    {
        var view = Load() ?? BuiltIn();
        camera.ShowKommunekartView(terrain, view);
    }

    public static KommunekartViewState BuiltIn() =>
        new(
            DefaultUrl,
            58.1467205326313,
            7.956351060738769,
            717.6761064074427,
            96.78343556922412,
            -20.987984196512883);

    static KommunekartViewState? Load()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = JsonSerializer.Deserialize<KommunekartJson>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (json is null)
                    continue;

                return new KommunekartViewState(
                    json.Url ?? DefaultUrl,
                    json.Latitude,
                    json.Longitude,
                    json.CameraHeightMeters,
                    json.HeadDegrees,
                    json.PitchDegrees);
            }
            catch
            {
                // fall through
            }
        }

        return null;
    }

    static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "map", "kommunekart-3d-view.json");
        yield return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "map", "kommunekart-3d-view.json"));
        yield return @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\kommunekart-3d-view.json";
    }

    internal readonly record struct KommunekartViewState(
        string Url,
        double Latitude,
        double Longitude,
        double CameraHeightMeters,
        double HeadDegrees,
        double PitchDegrees);

    sealed class KommunekartJson
    {
        public string? Url { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double CameraHeightMeters { get; set; }

        public double HeadDegrees { get; set; }

        public double PitchDegrees { get; set; }

        [JsonPropertyName("rollDegrees")]
        public double RollDegrees { get; set; }
    }
}
