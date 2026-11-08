using System.Numerics;

namespace ArtillerySimulator;

internal static class OslofjordenMissionBootFactory
{
    public static OslofjordenMissionBoot From(TerrainWorld terrain, IArtilleryPieceSpec piece)
    {
        var objectiveXZ = OslofjordenGeodesy.ToWorldMeters(
            OslofjordenGeodesy.ObjectiveLatitude,
            OslofjordenGeodesy.ObjectiveLongitude);
        var objective = new Vector3(
            objectiveXZ.X,
            terrain.Field.SampleHeight(objectiveXZ.X, objectiveXZ.Y),
            objectiveXZ.Y);

        var fort = SnapOscarsborg(terrain);
        var fortY = terrain.Field.SampleHeight(fort.X, fort.Y);
        var site = new GunSite(
            new Vector3(fort.X, fortY, fort.Y),
            openingElevationDegrees: 28f,
            columnLeadMeters: SimulationUnits.MinOpeningEngagementMeters(piece));

        // Invasion historically forced the Sound from the south — trail faces the southern approach.
        var southAim = new Vector3(fort.X, fortY, MathF.Max(250f, fort.Y - 5_000f));
        var trail = OpeningFirePlan.BearingDegrees(site.Position, southAim);
        return new OslofjordenMissionBoot(objective, site.Position.X, site.Position.Z, trail, site);
    }

    private static Vector2 SnapOscarsborg(TerrainWorld terrain)
    {
        var seed = OslofjordenGeodesy.ToWorldMeters(
            OslofjordenGeodesy.OscarsborgLatitude,
            OslofjordenGeodesy.OscarsborgLongitude);
        const float radius = 220f;
        const float step = 10f;
        var best = seed;
        var bestScore = float.NegativeInfinity;
        for (var z = seed.Y - radius; z <= seed.Y + radius; z += step)
        {
            for (var x = seed.X - radius; x <= seed.X + radius; x += step)
            {
                if (!PlayableMapBounds.InPlayableMap(terrain, x, z))
                    continue;
                if (terrain.Profile.IsWater(x, z))
                    continue;
                var h = terrain.Field.SampleHeight(x, z);
                if (h < 4f || h > 90f)
                    continue;
                var dx = x - seed.X;
                var dz = z - seed.Y;
                var dist = MathF.Sqrt(dx * dx + dz * dz);
                var score = h * 0.35f - dist;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = new Vector2(x, z);
            }
        }

        return best;
    }
}
