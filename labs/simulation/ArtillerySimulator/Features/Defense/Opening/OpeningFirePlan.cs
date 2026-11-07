using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Opening charge / elevation for Odderøya city defense. Prefer the shortest charge that
/// reaches the nearest column at or beyond <see cref="SimulationUnits.MinEffectiveRangeMeters"/>
/// (HE min ~4 km — downtown is inside that floor).
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
        Vector3 objective)
    {
        var desired = float.MaxValue;
        var aimBearing = trailBearingDegrees;
        var nearestCrossing = float.MaxValue;
        foreach (var unit in units)
        {
            if (!unit.Alive)
                continue;

            var bearing = MathF.Atan2(unit.Z - gun.Z, unit.X - gun.X) * (180f / MathF.PI);
            if (MathF.Abs(DeltaDegrees(bearing, trailBearingDegrees)) > GunModel.MaxTraverseDegrees)
                continue;

            var toCrossing = Horizontal(objective, new Vector3(unit.X, objective.Y, unit.Z));
            if (toCrossing >= nearestCrossing)
                continue;

            nearestCrossing = toCrossing;
            desired = Horizontal(gun, new Vector3(unit.X, gun.Y, unit.Z));
            aimBearing = bearing;
        }

        if (desired == float.MaxValue)
            desired = MathF.Max(Horizontal(gun, objective), SimulationUnits.MinEffectiveRangeMeters);

        // HE floor … outer approach band (never open inside min range or on deep max-zone lob).
        desired = Math.Clamp(
            desired,
            SimulationUnits.MinEffectiveRangeMeters,
            SimulationUnits.MaxCityApproachMeters);

        var atmosphere = AtmosphereModel.CreateRegionalDefault();
        var simulation = new ProjectileBallisticSimulation(GunModel.Educational155Profile, DragCoefficientLaw.BoatTail);
        var bestElev = fallbackElevation;
        var bestCharge = 3;
        var bestScore = float.NegativeInfinity;
        var closestError = float.MaxValue;
        var closeElev = fallbackElevation;
        var closeCharge = 3;

        // Prefer low → reduced → standard → max (shortest usable zone first).
        ReadOnlySpan<int> chargeOrder = [3, 0, 1, 2];
        foreach (var charge in chargeOrder)
        {
            var speed = SimulationUnits.ChargeSpeedsMps[charge];
            for (var elev = GunModel.MaxElevationDegrees; elev >= 12f; elev -= 0.5f)
            {
                var reach = ReachMeters(terrain, atmosphere, simulation, gun, aimBearing, elev, speed);
                if (reach < SimulationUnits.MinEffectiveRangeMeters - 50f)
                    continue;

                var error = MathF.Abs(reach - desired);
                if (error < closestError)
                {
                    closestError = error;
                    closeElev = elev;
                    closeCharge = charge;
                }

                // Prefer slightly short of the column (safer near downtown) within ~120 m.
                var longBy = reach - desired;
                if (longBy > 80f || longBy < -140f)
                    continue;

                // Lower charge index weight + moderate elevation (shorter TOF) + tight error.
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

            // Take the first charge tier that produced an acceptable bracket.
            if (bestScore > float.NegativeInfinity)
                return new Lay(bestElev, bestCharge, aimBearing);
        }

        if (closestError == float.MaxValue)
            return new Lay(Math.Clamp(fallbackElevation, 18f, 55f), 3, aimBearing);

        return new Lay(closeElev, closeCharge, aimBearing);
    }

    public static float ReachMeters(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ProjectileBallisticSimulation simulation,
        Vector3 gun,
        float azimuthDegrees,
        float elevationDegrees,
        float muzzleSpeedMps = 0f)
    {
        var elev = elevationDegrees * (MathF.PI / 180f);
        var az = azimuthDegrees * (MathF.PI / 180f);
        var cosE = MathF.Cos(elev);
        var direction = Vector3.Normalize(new Vector3(cosE * MathF.Cos(az), MathF.Sin(elev), cosE * MathF.Sin(az)));
        var pivot = gun + new Vector3(0f, SimulationUnits.GunHeightOffset, 0f);
        var muzzle = pivot + direction * GunModel.BarrelLength;
        var speed = muzzleSpeedMps > 1f ? muzzleSpeedMps : SimulationUnits.ChargeSpeedsMps[1];
        var state = new ProjectileState(muzzle, direction * speed, GunModel.Educational155Profile.MassKg, timeSeconds: 0);
        var environment = atmosphere.BallisticEnvironment(muzzle.Y);
        var preview = BallisticTrajectoryRunner.BuildPreview(
            simulation,
            environment,
            terrain.Field,
            state,
            dtSeconds: 1.0 / 60.0,
            maxTimeSeconds: 55,
            maxPoints: 4_000);
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
