using System.Globalization;
using System.Numerics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class Program
{
    // Headless agent: --headless  (invisible window; HTTP agent on :18770)
    // Headless profile: --profile [frames=180] [--json path]
    // Level: --level <id>   list: --list-levels
    // Placement dump: --dump-sites
    private static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--list-levels", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var entry in GameLevelCatalog.All)
            {
                var mark = entry.IsPlayable ? "playable" : "stub";
                Console.WriteLine($"{entry.Id,-28}  {mark,-8}  {entry.DisplayName}  ({entry.SiteName})");
            }

            return;
        }

        if (args.Any(a => string.Equals(a, "--dump-sites", StringComparison.OrdinalIgnoreCase)))
        {
            DumpSites(ResolveLevel(args));
            return;
        }

        var headless = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "--agent", StringComparison.OrdinalIgnoreCase));
        var profileFrames = ParseProfileFrames(args);
        var jsonPath = ParseJsonPath(args);
        var level = ResolveLevel(args);
        using var game = new ArtilleryApp(level, skipShell: headless || profileFrames is not null);
        if (profileFrames is int frames)
        {
            game.EnableProfile(frames, jsonPath);
            SilkGame.Run(
                $"{ProductIdentity.Title} profile",
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
            Console.WriteLine($"{ProductIdentity.Title} headless — level={level.Id} — agent HTTP http://127.0.0.1:18770/");
            Console.WriteLine("  GET  /agent/hello  /agent/snapshot  /agent/actions  /agent/mcp/tools");
            Console.WriteLine("  POST /agent/command");
            Console.WriteLine(ProductLegal.Tagline);
            SilkGame.Run(
                $"{ProductIdentity.Title} headless",
                1280,
                720,
                game.Initialize,
                game.Update,
                renderOverlay: null,
                captureSession: null,
                visible: false);
            return;
        }

        SilkGame.Run(ProductIdentity.WindowTitle, 1920, 1080, game.Initialize, game.Update);
    }

    private static IGameLevel ResolveLevel(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--level", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!GameLevelCatalog.TryGet(args[i + 1], out var level))
                throw new ArgumentException($"Unknown level '{args[i + 1]}'. Use --list-levels.");
            if (!level.IsPlayable)
                throw new ArgumentException($"Level '{level.Id}' is not playable yet.");
            return level;
        }

        return GameLevelCatalog.Default;
    }

    private static void DumpSites(IGameLevel level)
    {
        using var terrain = new TerrainWorld();
        level.RebuildMap(terrain);
        var mission = level.StartMission(terrain);
        terrain.PlaceGun(mission.GunX, mission.GunZ);
        var gun = terrain.GunBaseline;

        Console.WriteLine($"level {level.Id}  {level.DisplayName}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"gun   {FmtLatLon(gun.X, gun.Z)}  h={gun.Y:F1}"));
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
