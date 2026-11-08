namespace ArtillerySimulator;

internal static class PlayableMapBounds
{
    public const float MapMarginMeters = 20f;

    public static bool InPlayableMap(TerrainWorld terrain, float x, float z) =>
        x >= MapMarginMeters
        && z >= MapMarginMeters
        && x <= terrain.EastMeters - MapMarginMeters
        && z <= terrain.NorthMeters - MapMarginMeters;

    public static float ClampX(TerrainWorld terrain, float x) =>
        Math.Clamp(x, MapMarginMeters, terrain.EastMeters - MapMarginMeters);

    public static float ClampZ(TerrainWorld terrain, float z) =>
        Math.Clamp(z, MapMarginMeters, terrain.NorthMeters - MapMarginMeters);
}
