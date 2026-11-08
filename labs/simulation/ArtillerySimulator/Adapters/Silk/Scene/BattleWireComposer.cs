using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Dynamic world-line pass: haze, terrain, mission, gun, aim preview, shots, particles.</summary>
internal static class BattleWireComposer
{
    private static readonly Color TrailHot = BallisticArcColors.Flight;
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color FlashColor = Color.FromArgb(255, 255, 214, 150);

    public static void Compose(
        SilkWireBatch wires,
        FrameViewModel vm,
        Vector3 cameraPosition)
    {
        DrawSkyHaze(wires, cameraPosition, vm.Terrain);
        vm.Terrain.DrawDynamic(wires);
        vm.Mission?.Draw(wires);
        wires.Ring(vm.Terrain.GunBaseline, vm.Gun.Spec.MinEffectiveRangeMeters, BallisticArcColors.MinRange);
        DrawAimPreview(wires, vm.Terrain, vm.Aim, vm.SolutionUnsafe);
        vm.Gun.Draw(
            wires,
            vm.Terrain.GunBaseline,
            showPivotGlow: vm.Crew.BreechReady,
            loading: vm.Crew.Loading);
        DrawMuzzleFlash(wires, vm.Gun, vm.Terrain, vm.Battle.MuzzleFlash);
        DrawShot(wires, vm.Terrain, vm.Rounds, vm.Battle, vm.Mission, vm.Gun);
        vm.Particles.Draw(wires);
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
