using System.Globalization;
using System.Numerics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class Program
{
    // Headless agent: --headless  (invisible window; HTTP agent on :18770)
    // Headless profile: --profile [frames=180] [--json path]
    // Placement dump: --dump-sites
    private static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--dump-sites", StringComparison.OrdinalIgnoreCase)))
        {
            DumpSites();
            return;
        }

        var headless = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "--agent", StringComparison.OrdinalIgnoreCase));
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

        if (headless)
        {
            Console.WriteLine("ArtillerySimulator headless — agent HTTP http://127.0.0.1:18770/");
            Console.WriteLine("  GET  /agent/hello  /agent/snapshot  /agent/actions  /agent/mcp/tools");
            Console.WriteLine("  POST /agent/command");
            SilkGame.Run(
                "ArtillerySimulator headless",
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
        var fort = KristiansandFort.ToWorldMeters();

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"gun   {FmtLatLon(gun.X, gun.Z)}  h={gun.Y:F1}  ({KristiansandFort.Address})"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"fort  {FmtLatLon(fort.X, fort.Y)}  water={KristiansandLandSeaMask.IsWater(fort.X, fort.Y)}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"obj   {FmtLatLon(mission.Objective.X, mission.Objective.Z)}  gun→obj={Horizontal(gun, mission.Objective):F0}m"));

        foreach (var d in mission.DefenderReports())
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"def {d.Label}  {FmtLatLon(d.X, d.Z)}"));

        foreach (var t in mission.Reports(gun))
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"atk {t.Road}#{t.Id}  {FmtLatLon(t.X, t.Z)}  toObj={t.MetersToCrossing / 1000f:F1}km"));
    }

    private static string FmtLatLon(float x, float z)
    {
        var (lat, lon) = KristiansandHeightSampler.ToLonLat(x, z);
        return string.Create(CultureInfo.InvariantCulture, $"lat={lat:F5} lon={lon:F5}");
    }

    private static float Horizontal(Vector3 a, Vector3 b)
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
