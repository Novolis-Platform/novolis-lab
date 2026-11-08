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
            $"gun   {FmtLatLon(terrain, gun.X, gun.Z)}  xyz=({gun.X:F0},{gun.Y:F1},{gun.Z:F0}) water={terrain.Profile.IsWater(gun.X, gun.Z)}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"obj   {FmtLatLon(terrain, mission.Objective.X, mission.Objective.Z)}  gun→obj={Horizontal(gun, mission.Objective):F0}m"));

        foreach (var d in mission.DefenderReports())
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"def {d.Label}  {FmtLatLon(terrain, d.X, d.Z)}"));

        foreach (var t in mission.Reports(gun))
        {
            var toGun = MathF.Sqrt((t.X - gun.X) * (t.X - gun.X) + (t.Z - gun.Z) * (t.Z - gun.Z));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"atk {t.Road}#{t.Id}  {FmtLatLon(terrain, t.X, t.Z)}  toObj={t.MetersToCrossing / 1000f:F1}km  toGun={toGun / 1000f:F1}km"));
        }

        DumpAim(terrain, piece, mission, gun);
    }

    private static void DumpAim(TerrainWorld terrain, IArtilleryPieceSpec piece, IDefenseMission mission, Vector3 gun)
    {
        var atmosphere = AtmosphereModel.CreateRegionalDefault();
        var session = new BattleSession(piece, seed: 42);
        var gunModel = new GunModel(piece);
        gunModel.LayCenter(mission.TrailBearingDegrees);
        gunModel.SetElevation(mission.OpeningElevationDegrees);
        gunModel.NudgeAzimuth(OpeningFirePlan.DeltaDegrees(mission.OpeningAzimuthDegrees, gunModel.AzimuthDegrees));
        gunModel.SetCharge(mission.OpeningChargeIndex);

        var crew = new CrewDrill();
        crew.OrderAim(gunModel, mission.OpeningElevationDegrees, mission.OpeningAzimuthDegrees);
        gunModel.SetCharge(mission.OpeningChargeIndex);
        crew.SeatOpening(gunModel);

        var ballistics = new Novolis.Physics.Ballistics.ProjectileBallisticSimulation(
            piece.Projectile,
            Novolis.Physics.Ballistics.DragCoefficientLaw.BoatTail);
        var aim = new AimPreviewService(terrain, atmosphere, session);
        aim.Bind(gunModel, ballistics);
        aim.Rebuild(crew);

        var mpi = aim.PredictedImpact();
        var range = aim.PredictedRangeMeters();
        var ground = terrain.Field.SampleHeight(mpi.X, mpi.Z);
        var maxY = 0f;
        foreach (var p in aim.Ordered)
            maxY = MathF.Max(maxY, p.Y);
        var unsafeSol = range < piece.LaunchClearanceMeters || range < piece.MinEffectiveRangeMeters;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"aim   elev={mission.OpeningElevationDegrees:F1}° az={mission.OpeningAzimuthDegrees:F1}° ch={mission.OpeningChargeIndex} pts={aim.Ordered.Count} range={range:F0}m tof={aim.EstimatedTofSeconds():F1}s maxY={maxY:F0} mpiY={mpi.Y:F1} ground={ground:F1} unsafe={unsafeSol}"));
        if (aim.Ordered.Count >= 1)
        {
            var a0 = aim.Ordered[0];
            var a1 = aim.Ordered[^1];
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"arc   first=({a0.X:F0},{a0.Y:F1},{a0.Z:F0}) last=({a1.X:F0},{a1.Y:F1},{a1.Z:F0})"));
        }
    }

    private static string FmtLatLon(TerrainWorld terrain, float x, float z)
    {
        if (string.Equals(terrain.Profile.Id, "oslofjorden", StringComparison.OrdinalIgnoreCase))
        {
            var (lat, lon) = OslofjordenGeodesy.ToLonLat(x, z);
            return string.Create(CultureInfo.InvariantCulture, $"lat={lat:F5} lon={lon:F5}");
        }

        var krs = KristiansandHeightSampler.ToLonLat(x, z);
        return string.Create(CultureInfo.InvariantCulture, $"lat={krs.Latitude:F5} lon={krs.Longitude:F5}");
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
