using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtillerySimulator.Game;

/// <summary>Startup camera matched to a shared Kommunekart 3D link (lat/lon/head/pitch/height).</summary>
internal static class Kommunekart3DView
{
    public const string DefaultUrl =
        "https://3d.kommunekart.com/?x=58.038077082078786&y=8.00709090969718&z=18111.017634206884&head=356.5381777796763&pitch=-58.52425910026143&roll=0.006390997054148519";

    public static void ApplyStartupCamera(ArtilleryCameras camera, TerrainWorld terrain)
    {
        var view = Load() ?? BuiltIn();
        camera.ShowKommunekartView(terrain, view);
    }

    public static KommunekartViewState BuiltIn() =>
        new(
            DefaultUrl,
            58.038077082078786,
            8.00709090969718,
            18111.017634206884,
            356.5381777796763,
            -58.52425910026143);

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
