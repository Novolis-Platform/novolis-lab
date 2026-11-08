using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class ShotFxWireComposer
{
    private static readonly Color TrailHot = BallisticArcColors.Flight;
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color FlashColor = Color.FromArgb(255, 255, 214, 150);

    public static void DrawMuzzleFlash(SilkWireBatch wires, GunModel gun, TerrainWorld terrain, float muzzleFlash)
    {
        if (muzzleFlash <= 0f)
            return;

        var muzzle = gun.MuzzlePosition(terrain.GunBaseline);
        var radius = 1.4f + (0.22f - muzzleFlash) * 18f;
        wires.WireSphere(muzzle, radius, FlashColor, segments: 10);
    }

    public static void DrawShots(
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
