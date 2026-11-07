using Novolis.Agent.Core;

namespace ArtillerySimulator.Game;

internal static partial class KristiansandMapFeatures
{
    private static GroundPolyline[]? _visible;
    private static GroundPolygon[]? _waterVisible;

    public static IReadOnlyList<GroundPolyline> Visible =>
        _visible ??= MapFeaturesCatalog.ClipLines(Lines, KristiansandHeightSampler.EastMeters, KristiansandHeightSampler.NorthMeters);

    public static IReadOnlyList<GroundPolygon> WaterVisible =>
        _waterVisible ??= MapFeaturesCatalog.ClipWater(Water, KristiansandHeightSampler.EastMeters, KristiansandHeightSampler.NorthMeters);

    public static GroundPolyline? Longest(string name, string kind) =>
        MapFeaturesCatalog.Longest(Visible, name, kind);

    public static void DrawWater(SilkWireBatch wires, TerrainWorld terrain)
    {
        // Open water and lakes come from Norgeskart land/sea mask wires, not OSM voxels.
    }

    public static void ResetClipCache()
    {
        _visible = null;
        _waterVisible = null;
        _waterForFill = null;
    }

    public static void DrawTableLinework(SilkWireBatch wires) =>
        MapFeaturesCatalog.DrawLinesOnTable(
            wires,
            Visible,
            KristiansandReferenceMap.TableYMeters + 0.45f,
            skipCoastline: true);

    public static void DrawLinework(SilkWireBatch wires, TerrainWorld terrain) =>
        KristiansandPdfRoads.Draw(wires, terrain);

    private static GroundPolygon[]? _waterForFill;

    private static IReadOnlyList<GroundPolygon> WaterForFill =>
        _waterForFill ??= MapFeaturesCatalog.ClipWaterForFill(
            Water,
            KristiansandHeightSampler.EastMeters,
            KristiansandHeightSampler.NorthMeters);

    public static AgentEntity[] MapEntities() => MapFeaturesCatalog.MapEntities(Visible);
}
