using System.Globalization;
using System.Numerics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class Program
{
    // Headless profile: --profile [frames=180] [--json path]
    // Placement dump: --dump-sites
    private static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--dump-sites", StringComparison.OrdinalIgnoreCase)))
        {
            DumpSites();
            return;
        }

        var profileFrames = ParseProfileFrames(args);
        var jsonPath = ParseJsonPath(args);
        using var game = new ArtilleryApp();
        if (profileFrames is int frames)
        {
            game.EnableProfile(frames, jsonPath);
            SilkGame.Run(
                "ArtillerySimulator profile",
                1280,
                720,
                game.Initialize,
                game.Update,
                renderOverlay: null,
                captureSession: null,
                visible: false);
            return;
        }

        SilkGame.Run("Artillery Simulator — Kristiansand", 1920, 1080, game.Initialize, game.Update);
    }

    private static void DumpSites()
    {
        using var terrain = new TerrainWorld();
        terrain.Rebuild();
        var mission = new KristiansandDefenseMission(terrain);
        terrain.PlaceGun(mission.GunX, mission.GunZ);
        var gun = terrain.GunBaseline;
        var raw = KristiansandHeightSampler.ToWorldMeters(KristiansandFort.Latitude, KristiansandFort.Longitude);
        var fort = KristiansandFort.ToWorldMeters();

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"fort_const lat={KristiansandFort.Latitude:F5} lon={KristiansandFort.Longitude:F5}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"fort_raw   xz=({raw.X:F1},{raw.Y:F1})  {FmtLatLon(raw.X, raw.Y)}  h={terrain.Field.SampleHeight(raw.X, raw.Y):F1} water={KristiansandLandSeaMask.IsWater(raw.X, raw.Y)}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"fort_snap  xz=({fort.X:F1},{fort.Y:F1})  {FmtLatLon(fort.X, fort.Y)}  h={terrain.Field.SampleHeight(fort.X, fort.Y):F1} water={KristiansandLandSeaMask.IsWater(fort.X, fort.Y)}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"gun        xz=({gun.X:F1},{gun.Z:F1})  {FmtLatLon(gun.X, gun.Z)}  y={gun.Y:F1}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"objective  xz=({mission.Objective.X:F1},{mission.Objective.Z:F1})  {FmtLatLon(mission.Objective.X, mission.Objective.Z)}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"gun→obj    {Horizontal(gun, mission.Objective):F0} m  trail={mission.TrailBearingDegrees:F1}°"));

        // Probe candidate fort seeds on Odderøya for land/height.
        (double Lat, double Lon, string Label)[] probes =
        [
            (58.130278, 8.006944, "wiki-fort"),
            (58.132672, 8.006630, "tourist-fort"),
            (58.13650, 8.00450, "lasarett"),
            (58.13780, 8.00380, "odderoy-n"),
            (58.13400, 8.00800, "mid-east"),
            (58.13216, 8.01088, "galgebakken"),
            (58.13950, 8.00150, "bridge-n"),
        ];
        foreach (var p in probes)
        {
            var xz = KristiansandHeightSampler.ToWorldMeters(p.Lat, p.Lon);
            var h = terrain.Field.SampleHeight(xz.X, xz.Y);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"probe {p.Label,-12} lat={p.Lat:F5} lon={p.Lon:F5} h={h:F1} water={KristiansandLandSeaMask.IsWater(xz.X, xz.Y)}"));
        }

        foreach (var d in mission.DefenderReports())
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"defender {d.Label} xz=({d.X:F1},{d.Z:F1})  {FmtLatLon(d.X, d.Z)}"));

        foreach (var name in new[] { "Strai", "Hellemyr", "Søm", "Møvik", "Vågsbygd" })
        {
            var (lat, lon) = name switch
            {
                "Strai" => (58.1893, 7.92826),
                "Hellemyr" => (58.1680, 7.9450),
                "Søm" => (58.1520, 8.0780),
                "Møvik" => (58.1250, 7.9550),
                _ => (58.1320, 7.9480),
            };
            var seed = KristiansandHeightSampler.ToWorldMeters(lat, lon);
            var snapped = PrimaryRoadNetwork.Snap(seed);
            if (snapped is { } s)
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"seed {name,-10} want {FmtLatLon(seed.X, seed.Y)}  snap {FmtLatLon(s.X, s.Y)}  d={Vector2.Distance(seed, s):F0}m"));
            else
                Console.WriteLine($"seed {name} NO SNAP");
        }

        foreach (var t in mission.Reports(gun))
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"hostile  {t.Road}#{t.Id} xz=({t.X:F1},{t.Z:F1})  {FmtLatLon(t.X, t.Z)}  fromGun={t.MetersFromGun:F0}m toObj={t.MetersToCrossing:F0}m"));
    }

    private static string FmtLatLon(float x, float z)
    {
        var lat = KristiansandHeightSampler.SouthLatitude
            + (z / KristiansandHeightSampler.NorthMeters)
            * (KristiansandHeightSampler.NorthLatitude - KristiansandHeightSampler.SouthLatitude);
        var lon = KristiansandHeightSampler.WestLongitude
            + (x / KristiansandHeightSampler.EastMeters)
            * (KristiansandHeightSampler.EastLongitude - KristiansandHeightSampler.WestLongitude);
        return string.Create(CultureInfo.InvariantCulture, $"lat={lat:F5} lon={lon:F5}");
    }

    private static float Horizontal(System.Numerics.Vector3 a, System.Numerics.Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static int? ParseProfileFrames(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var n) && n > 0)
                return n;
            return 180;
        }

        return null;
    }

    private static string? ParseJsonPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--json", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }
}
