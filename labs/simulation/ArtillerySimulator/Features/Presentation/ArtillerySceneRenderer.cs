using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>World + HUD draw for the playing frame (Silk host stays thin).</summary>
internal sealed class ArtillerySceneRenderer
{
    private static readonly Color TrailHot = BallisticArcColors.Flight;
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color FlashColor = Color.FromArgb(255, 255, 214, 150);
    private static readonly Rgba32 SkyZenith = new(28, 58, 98, 255);
    private static readonly Rgba32 SkyHorizon = new(210, 140, 90, 220);
    private static readonly Rgba32 ClearSky = new(18, 28, 42, 255);

    private readonly SilkWireBatch _wires = new();
    private readonly WorldSolidBatch _solids = new();
    private readonly SmoothedFps _fps = new();

    public SilkWireBatch Wires => _wires;
    public float Fps => _fps.Value;

    public void DrawPlaying(
        SilkFrame frame,
        ArtilleryCameras camera,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        GunModel gun,
        CrewDrill crew,
        ArtilleryRoundPool rounds,
        AimPreviewService aim,
        ArtilleryBattleLoop battle,
        ArtilleryParticles particles,
        IDefenseMission? mission,
        IGameLevel level,
        FireControlUi ui,
        ArtilleryAudioHub? audio,
        Func<string?> fireBlockReason,
        Func<bool> solutionUnsafe,
        Func<bool> launchDetonation,
        float fxTime,
        FrameProfiler? profiler)
    {
        frame.Clear(ClearSky);
        var cam = camera.Build(frame.DeltaSeconds);
        var aspect = frame.Width / (float)Math.Max(1, frame.Height);
        var view = KristiansandGeoref.CreateMapView(cam.Position, cam.Target, cam.Up);
        var near = 2f;
        var far = MathF.Max(40_000f, Vector3.Distance(cam.Position, cam.Target) * 8f);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            near,
            far);
        var mvp = view * proj;

        _solids.Clear();
        DrawAtmosphere(_solids, cam.Position, terrain.GunBaseline);
        frame.DrawWorldTriangles(_solids.Triangles, mvp);

        if (profiler is not null)
            profiler.Time("draw.static", () => terrain.DrawStatic(frame, mvp));
        else
            terrain.DrawStatic(frame, mvp);

        _wires.Clear();
        void BuildDynamic()
        {
            DrawSkyHaze(_wires, cam.Position, terrain);
            terrain.DrawDynamic(_wires);
            mission?.Draw(_wires);
            _wires.Ring(terrain.GunBaseline, gun.Spec.MinEffectiveRangeMeters, BallisticArcColors.MinRange);
            DrawAimPreview(_wires, terrain, aim, solutionUnsafe());
            gun.Draw(
                _wires,
                terrain.GunBaseline,
                showPivotGlow: crew.BreechReady,
                loading: crew.Loading);
            DrawMuzzleFlash(_wires, gun, terrain, battle.MuzzleFlash);
            DrawShot(_wires, terrain, rounds, battle, mission, gun);
            particles.Draw(_wires);
        }

        if (profiler is not null)
            profiler.Time("draw.dynamic.build", BuildDynamic);
        else
            BuildDynamic();

        if (profiler is not null)
            profiler.Time("draw.dynamic.upload", () => frame.DrawWorldLines(_wires.AsSpan(), mvp));
        else
            frame.DrawWorldLines(_wires.AsSpan(), mvp);

        frame.DrawScreenGrade(fxTime, intensity: 0.92f);

        _fps.Update(frame.DeltaSeconds);
        var predicted = aim.PredictedImpact();
        var predictedRange = aim.PredictedRangeMeters();
        var tof = aim.EstimatedTofSeconds();

        void DrawHud()
        {
            ui.Draw(
                frame,
                gun,
                crew.OrderedElevation,
                crew.OrderedAzimuth,
                terrain,
                atmosphere,
                rounds.Active,
                camera.FollowProjectile,
                _fps.Value,
                predictedRange,
                tof,
                predicted,
                mission is null
                    ? $"{level.DisplayName} — {level.SiteName}"
                    : $"{level.DisplayName.ToUpperInvariant()}  |  {mission.HudLine()}",
                fireBlockReason(),
                solutionUnsafe(),
                launchDetonation(),
                audio?.RadioLine,
                audio?.RadioSpeaker);
        }

        if (profiler is not null)
            profiler.Time("draw.hud", DrawHud);
        else
            DrawHud();
    }

    private static void DrawAtmosphere(WorldSolidBatch solids, Vector3 camera, Vector3 gunBaseline)
    {
        var center = new Vector3(camera.X, MathF.Min(camera.Y, gunBaseline.Y + 40f), camera.Z);
        solids.SkyDome(center, radius: 28_000f, SkyZenith, SkyHorizon, slices: 28, stacks: 10);
    }

    private static void DrawSkyHaze(SilkWireBatch wires, Vector3 camera, TerrainWorld terrain)
    {
        var light = new Vector3(
            terrain.ExtentMeters * 0.55f,
            6800f,
            terrain.GunBaseline.Z - 4000f);
        var toLight = Vector3.Normalize(light - camera);
        var origin = camera + toLight * 2200f;
        for (var i = 0; i < 10; i++)
        {
            var t = i / 9f;
            var lateral = Vector3.Normalize(Vector3.Cross(toLight, Vector3.UnitY));
            if (lateral.LengthSquared() < 1e-4f)
                lateral = Vector3.UnitZ;
            var offset = lateral * ((t - 0.5f) * 900f) + Vector3.UnitY * ((t - 0.5f) * 120f);
            var a = origin + offset;
            var b = a + toLight * (4800f + t * 2200f);
            var alpha = (int)(28 + 22 * (1f - MathF.Abs(t - 0.5f) * 2f));
            wires.Line(a, b, Color.FromArgb(alpha, 255, 200, 140));
        }

        var gun = terrain.GunBaseline;
        for (var r = 2; r <= 5; r++)
        {
            var radius = r * 1800f;
            var alpha = 18 + r * 4;
            wires.Ring(
                new Vector3(gun.X, gun.Y + 40f + r * 12f, gun.Z),
                radius,
                Color.FromArgb(alpha, 180, 150, 110),
                segments: 40,
                yLift: 0f);
        }
    }

    private static void DrawMuzzleFlash(SilkWireBatch wires, GunModel gun, TerrainWorld terrain, float muzzleFlash)
    {
        if (muzzleFlash <= 0f)
            return;

        var muzzle = gun.MuzzlePosition(terrain.GunBaseline);
        var radius = 1.4f + (0.22f - muzzleFlash) * 18f;
        wires.WireSphere(muzzle, radius, FlashColor, segments: 10);
    }

    private static void DrawAimPreview(SilkWireBatch wires, TerrainWorld terrain, AimPreviewService aim, bool unsafeSol)
    {
        var ordered = unsafeSol ? BallisticArcColors.OrderedUnsafe : BallisticArcColors.Ordered;
        var mpiColor = unsafeSol ? BallisticArcColors.MpiMarkUnsafe : BallisticArcColors.MpiMark;

        DrawProbabilityFootprint(wires, terrain, aim, unsafeSol);

        if (aim.Tube.Count >= 2)
            DrawArc(wires, aim.Tube, BallisticArcColors.Tube, maxSegments: 160);

        if (aim.Ordered.Count >= 2)
        {
            DrawArc(wires, aim.Ordered, ordered, maxSegments: 280);
            var mpi = aim.Ordered[^1];
            wires.WireSphere(mpi, 28f, mpiColor, segments: 10);
            wires.Ring(mpi, 40f, mpiColor);
            var ground = terrain.Field.SampleHeight(mpi.X, mpi.Z);
            wires.Line(mpi, new Vector3(mpi.X, ground + 1f, mpi.Z), mpiColor);
        }
    }

    private static void DrawProbabilityFootprint(
        SilkWireBatch wires,
        TerrainWorld terrain,
        AimPreviewService aim,
        bool unsafeSol)
    {
        if (aim.Dispersion.Count < 2 || aim.Ordered.Count < 2)
            return;

        var footprint = unsafeSol
            ? BallisticArcColors.ProbabilityFootprintUnsafe
            : BallisticArcColors.ProbabilityFootprint;

        var impacts = new List<Vector3>(aim.Dispersion.Count + 1);
        foreach (var arc in aim.Dispersion)
            impacts.Add(arc[^1]);
        impacts.Add(aim.Ordered[^1]);

        var cx = 0f;
        var cz = 0f;
        foreach (var p in impacts)
        {
            cx += p.X;
            cz += p.Z;
        }

        cx /= impacts.Count;
        cz /= impacts.Count;
        var radius = 0f;
        foreach (var p in impacts)
        {
            var dx = p.X - cx;
            var dz = p.Z - cz;
            radius = MathF.Max(radius, MathF.Sqrt(dx * dx + dz * dz));
        }

        radius = Math.Clamp(radius, 40f, 900f);
        var y = terrain.Field.SampleHeight(cx, cz) + 2.5f;
        wires.Ring(new Vector3(cx, y, cz), radius, footprint, segments: 36, yLift: 0f);
    }

    private static void DrawArc(SilkWireBatch wires, IReadOnlyList<Vector3> points, Color color, int maxSegments = 180)
    {
        var stride = Math.Max(1, points.Count / Math.Max(8, maxSegments));
        for (var i = stride; i < points.Count; i += stride)
            wires.Line(points[i - stride], points[i], color);

        var last = points.Count - 1;
        var drawn = last - (last % stride);
        if (drawn != last && drawn >= 0)
            wires.Line(points[drawn], points[last], color);
    }

    private static void DrawShot(
        SilkWireBatch wires,
        TerrainWorld terrain,
        ArtilleryRoundPool rounds,
        ArtilleryBattleLoop battle,
        IDefenseMission? mission,
        GunModel gun)
    {
        foreach (var round in rounds.Rounds)
        {
            if (round.Phase is not (BallisticTrajectoryPhase.InFlight or BallisticTrajectoryPhase.Impacted))
                continue;

            var trail = round.Trail;
            for (var i = 1; i < trail.Count; i++)
            {
                var heat = i / (float)trail.Count;
                var color = Color.FromArgb(
                    (int)(TrailCool.A + (TrailHot.A - TrailCool.A) * heat),
                    (int)(TrailCool.R + (TrailHot.R - TrailCool.R) * heat),
                    (int)(TrailCool.G + (TrailHot.G - TrailCool.G) * heat),
                    (int)(TrailCool.B + (TrailHot.B - TrailCool.B) * heat));
                wires.Line(trail[i - 1], trail[i], color);
            }

            if (round.Phase != BallisticTrajectoryPhase.InFlight)
                continue;

            var span = AimPreviewService.HorizontalRange(terrain.GunBaseline, round.CurrentPosition);
            var radius = Math.Clamp(span * 0.012f, 14f, 70f);
            wires.WireSphere(round.CurrentPosition, radius * 0.45f, TrailHot, segments: 10);
            wires.WireSphere(round.CurrentPosition, radius, FlashColor, segments: 10);
            var ground = terrain.Field.SampleHeight(round.CurrentPosition.X, round.CurrentPosition.Z);
            var drop = new Vector3(round.CurrentPosition.X, ground + 2f, round.CurrentPosition.Z);
            wires.Line(round.CurrentPosition, drop, Color.FromArgb(160, TrailHot.R, TrailHot.G, TrailHot.B));
        }

        if (battle.BurstAge >= 0f)
            DrawBurst(wires, terrain, battle, mission, gun);
    }

    private static void DrawBurst(
        SilkWireBatch wires,
        TerrainWorld terrain,
        ArtilleryBattleLoop battle,
        IDefenseMission? mission,
        GunModel gun)
    {
        var age = MathF.Min(battle.BurstAge, 8f);
        var fade = 1f - age / 8f;
        var alpha = (int)(220 * Math.Clamp(fade, 0.35f, 1f));
        var color = Color.FromArgb(alpha, BallisticArcColors.Impact.R, BallisticArcColors.Impact.G, BallisticArcColors.Impact.B);
        var pale = Color.FromArgb(alpha / 2, BallisticArcColors.Impact.R, BallisticArcColors.Impact.G, BallisticArcColors.Impact.B);
        var column = 40f + age * 30f;
        var burstAt = battle.BurstAt;
        var ground = new Vector3(burstAt.X, terrain.Field.SampleHeight(burstAt.X, burstAt.Z), burstAt.Z);
        wires.Line(ground, burstAt + Vector3.UnitY * column, color);
        wires.WireSphere(burstAt, 22f + age * 8f, color, segments: 12);
        wires.WireSphere(burstAt, 10f + age * 3f, pale, segments: 10);
        var caught = mission?.CaughtMeters ?? gun.Spec.Warhead.CaughtMeters;
        var pinned = mission?.PinnedMeters ?? gun.Spec.Warhead.PinnedMeters;
        wires.Ring(ground + Vector3.UnitY * 2f, caught, color);
        wires.Ring(ground + Vector3.UnitY * 2f, pinned, pale);
    }
}
