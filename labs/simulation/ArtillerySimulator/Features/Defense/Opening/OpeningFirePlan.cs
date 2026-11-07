using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Opening charge / elevation. Prefer the shortest charge that reaches the nearest column
/// at or beyond the active piece's HE min range.
/// </summary>
internal static class OpeningFirePlan
{
    internal readonly record struct Lay(float Elevation, int Charge, float Bearing);

    public static Lay Tune(
        TerrainWorld terrain,
        Vector3 gun,
        float trailBearingDegrees,
        float fallbackElevation,
        IReadOnlyList<ColumnUnit> units,
        Vector3 objective,
        IArtilleryPieceSpec piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var desired = float.MaxValue;
        var aimBearing = trailBearingDegrees;
        var nearestCrossing = float.MaxValue;
        foreach (var unit in units)
        {
            if (!unit.Alive)
                continue;

            var bearing = MathF.Atan2(unit.Z - gun.Z, unit.X - gun.X) * (180f / MathF.PI);
            if (MathF.Abs(DeltaDegrees(bearing, trailBearingDegrees)) > piece.MaxTraverseDegrees)
                continue;

            var toCrossing = Horizontal(objective, new Vector3(unit.X, objective.Y, unit.Z));
            if (toCrossing >= nearestCrossing)
                continue;

            nearestCrossing = toCrossing;
            desired = Horizontal(gun, new Vector3(unit.X, gun.Y, unit.Z));
            aimBearing = bearing;
        }

        if (desired == float.MaxValue)
            desired = MathF.Max(Horizontal(gun, objective), piece.MinEffectiveRangeMeters);

        desired = Math.Clamp(
            desired,
            piece.MinEffectiveRangeMeters,
            SimulationUnits.MaxCityApproachMeters);

        var atmosphere = AtmosphereModel.CreateRegionalDefault();
        var simulation = new ProjectileBallisticSimulation(piece.Projectile, DragCoefficientLaw.BoatTail);
        var bestElev = fallbackElevation;
        var bestCharge = Math.Min(3, piece.Charges.Count - 1);
        var bestScore = float.NegativeInfinity;
        var closestError = float.MaxValue;
        var closeElev = fallbackElevation;
        var closeCharge = bestCharge;

        ReadOnlySpan<int> chargeOrder = [3, 0, 1, 2];
        foreach (var charge in chargeOrder)
        {
            if (charge < 0 || charge >= piece.Charges.Count)
                continue;

            var speed = piece.ChargeAt(charge).MuzzleSpeedMps;
            for (var elev = piece.MaxElevationDegrees; elev >= 12f; elev -= 2f)
            {
                var reach = ReachMeters(terrain, atmosphere, simulation, gun, aimBearing, elev, piece, speed);
                if (reach < piece.MinEffectiveRangeMeters - 50f)
                    continue;

                var error = MathF.Abs(reach - desired);
                if (error < closestError)
                {
                    closestError = error;
                    closeElev = elev;
                    closeCharge = charge;
                }

                var longBy = reach - desired;
                if (longBy > 80f || longBy < -140f)
                    continue;

                var chargeBias = charge switch
                {
                    3 => 40f,
                    0 => 30f,
                    1 => 12f,
                    _ => 0f,
                };
                var elevBias = elev <= 45f ? 8f : elev <= 60f ? 3f : 0f;
                var score = chargeBias + elevBias - error * 0.08f - MathF.Max(0f, longBy) * 0.12f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestElev = elev;
                bestCharge = charge;
            }

            if (bestScore > float.NegativeInfinity)
                return new Lay(bestElev, bestCharge, aimBearing);
        }

        if (closestError == float.MaxValue)
            return new Lay(Math.Clamp(fallbackElevation, 18f, 55f), closeCharge, aimBearing);

        return new Lay(closeElev, closeCharge, aimBearing);
    }

    public static float ReachMeters(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ProjectileBallisticSimulation simulation,
        Vector3 gun,
        float azimuthDegrees,
        float elevationDegrees,
        IArtilleryPieceSpec piece,
        float muzzleSpeedMps = 0f)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var elev = elevationDegrees * (MathF.PI / 180f);
        var az = azimuthDegrees * (MathF.PI / 180f);
        var cosE = MathF.Cos(elev);
        var direction = Vector3.Normalize(new Vector3(cosE * MathF.Cos(az), MathF.Sin(elev), cosE * MathF.Sin(az)));
        var pivot = gun + new Vector3(0f, piece.GunHeightOffsetMeters, 0f);
        var muzzle = pivot + direction * piece.BarrelLengthMeters;
        var speed = muzzleSpeedMps > 1f ? muzzleSpeedMps : piece.ChargeAt(1).MuzzleSpeedMps;
        var state = new ProjectileState(muzzle, direction * speed, piece.Projectile.MassKg, timeSeconds: 0);
        var environment = atmosphere.BallisticEnvironment(muzzle.Y);
        var preview = BallisticTrajectoryRunner.BuildPreview(
            simulation,
            environment,
            terrain.Field,
            state,
            dtSeconds: 1.0 / 20.0,
            maxTimeSeconds: 120,
            maxPoints: 1_200);
        return Horizontal(pivot, preview[^1]);
    }

    public static float Horizontal(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public static float BearingDegrees(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        return MathF.Atan2(dz, dx) * (180f / MathF.PI);
    }

    public static float DeltaDegrees(float a, float b)
    {
        var delta = a - b;
        while (delta > 180f)
            delta -= 360f;
        while (delta < -180f)
            delta += 360f;
        return delta;
    }
}
