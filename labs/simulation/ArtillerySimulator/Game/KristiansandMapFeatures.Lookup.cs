using Novolis.Agent.Core;
using Novolis.Raylib.Game;

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

    public static void DrawWater(RayGameContext ctx, TerrainWorld terrain)
    {
        // Open water and lakes come from Norgeskart #5182FF land/sea mask, not OSM voxels.
    }

    public static void ResetClipCache()
    {
        _visible = null;
        _waterVisible = null;
        _waterForFill = null;
    }

    public static void DrawTableLinework(RayGameContext ctx) =>
        MapFeaturesCatalog.DrawLinesOnTable(
            ctx,
            Visible,
            KristiansandReferenceMap.TableYMeters + 0.45f,
            skipCoastline: true);

    public static void DrawLinework(RayGameContext ctx, TerrainWorld terrain)
    {
        var maskOwnsWater = KristiansandLandSeaMask.IsAvailable;
        MapFeaturesCatalog.DrawLines(ctx, terrain, Visible, skipCoastline: maskOwnsWater);
        if (!maskOwnsWater)
        {
            var east = KristiansandHeightSampler.EastMeters;
            var north = KristiansandHeightSampler.NorthMeters;
            MapFeaturesCatalog.DrawWaterOutlines(ctx, terrain, WaterVisible, east, north);
        }
    }

    private static GroundPolygon[]? _waterForFill;

    private static IReadOnlyList<GroundPolygon> WaterForFill =>
        _waterForFill ??= MapFeaturesCatalog.ClipWaterForFill(
            Water,
            KristiansandHeightSampler.EastMeters,
            KristiansandHeightSampler.NorthMeters);

    public static AgentEntity[] MapEntities() => MapFeaturesCatalog.MapEntities(Visible);
}
