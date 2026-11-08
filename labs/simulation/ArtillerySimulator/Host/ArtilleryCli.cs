using System.Globalization;
using System.Numerics;
using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

/// <summary>CLI parse + one-shot commands (--help, --list-levels, --dump-sites).</summary>
internal static class ArtilleryCli
{
    public static bool TryHandleEarlyExit(string[] args, ILogger logger, out int exitCode)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            PrintHelp();
            exitCode = 0;
            return true;
        }

        if (args.Any(a => string.Equals(a, "--list-levels", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var entry in GameLevelCatalog.All)
            {
                var mark = entry.IsPlayable ? "playable" : "stub";
                Console.WriteLine($"{entry.Id,-28}  {mark,-8}  {entry.DisplayName}  ({entry.SiteName})");
            }

            exitCode = 0;
            return true;
        }

        if (args.Any(a => string.Equals(a, "--dump-sites", StringComparison.OrdinalIgnoreCase)))
        {
            var level = ResolveLevel(args);
            DumpSites(level, logger);
            exitCode = 0;
            return true;
        }

        exitCode = 0;
        return false;
    }

    public static ArtilleryRunOptions Parse(string[] args)
    {
        var headless = args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "--agent", StringComparison.OrdinalIgnoreCase));
        var smoke = args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase));
        var attachAgent = headless
            || args.Any(a => string.Equals(a, "--attach-agent", StringComparison.OrdinalIgnoreCase));
        var verbose = args.Any(a => a is "-v" or "--verbose");

        return new ArtilleryRunOptions
        {
            Level = ResolveLevel(args),
            Seed = ParseSeed(args),
            Headless = headless,
            Smoke = smoke,
            AttachAgent = attachAgent,
            ProfileFrames = ParseProfileFrames(args),
            ProfileJsonPath = ParseJsonPath(args),
            Verbose = verbose,
        };
    }

    public static void PrintHelp()
    {
        Console.WriteLine($"""
            {ProductIdentity.Title} — {ProductIdentity.Subtitle}
            {ProductLegal.Tagline}

            Usage:
              ArtillerySimulator [--level <id>] [--seed <int|hex>] [-v|--verbose]
              ArtillerySimulator --list-levels
              ArtillerySimulator --dump-sites [--level <id>]
              ArtillerySimulator --headless [--level <id>] [--seed <int>]
              ArtillerySimulator --smoke [--level <id>] [--seed <int>]
              ArtillerySimulator --profile [frames] [--json path]
              ArtillerySimulator --attach-agent   (desktop + HTTP agent; or ARTILLERY_ATTACH_AGENT=1)

            Splash: ← → change level, Enter begin, L licenses.
            Play:   WASD/arrows lay, Space fire, 1-4 charge, Esc/P pause, L licenses.
            """);
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

    private static void DumpSites(IGameLevel level, ILogger logger)
    {
        using var terrain = new TerrainWorld();
        level.RebuildMap(terrain);
        var piece = level.ResolvePiece();
        var mission = level.StartMission(terrain, piece, seed: 42);
        terrain.PlaceGun(mission.GunX, mission.GunZ, piece.GunHeightOffsetMeters);
        var gun = terrain.GunBaseline;

        logger.LogInformation("Dump sites for {LevelId} ({DisplayName})", level.Id, level.DisplayName);
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

    private static int? ParseSeed(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--seed", StringComparison.OrdinalIgnoreCase))
                continue;
            if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                return seed;
            if (int.TryParse(args[i + 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed))
                return seed;
            throw new ArgumentException($"Invalid --seed '{args[i + 1]}' (need int or hex).");
        }

        return null;
    }
}
