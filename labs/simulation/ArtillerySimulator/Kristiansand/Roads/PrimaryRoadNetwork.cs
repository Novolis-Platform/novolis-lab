using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Graph of <strong>primary</strong> roads from <c>nvdb-roads.bin</c> (E/R/S/F).
/// Hostile columns pathfind on this network toward Kvadraturen.
/// </summary>
internal static class PrimaryRoadNetwork
{
    public static void Reset() => NvdbPrimaryRoadGraphBuilder.Reset();

    public static GroundPolyline? RouteFromSeed(
        Vector2 goal,
        Vector2 approachSeed,
        string name,
        float minLengthMeters = 1_200f,
        float maxSeedSnapMeters = 2_400f) =>
        PrimaryRoadQueries.RouteFromSeed(goal, approachSeed, name, minLengthMeters, maxSeedSnapMeters);

    public static GroundPolyline? RouteToward(Vector2 goal, float approachBearingDegrees, string name, float minLengthMeters = 1_200f) =>
        PrimaryRoadQueries.RouteToward(goal, approachBearingDegrees, name, minLengthMeters);

    public static Vector2? Snap(Vector2 world) => PrimaryRoadQueries.Snap(world);

    public static IReadOnlyList<GroundPolyline> BuildApproachSpawns(
        Vector2 goal,
        Vector2 gun,
        float ringMeters,
        int count,
        float minLengthMeters = 1_200f,
        float mapEastMeters = float.PositiveInfinity,
        float mapNorthMeters = float.PositiveInfinity,
        float mapMarginMeters = 120f) =>
        ApproachSpawnPlanner.BuildApproachSpawns(
            goal,
            gun,
            ringMeters,
            count,
            minLengthMeters,
            mapEastMeters,
            mapNorthMeters,
            mapMarginMeters);
}
