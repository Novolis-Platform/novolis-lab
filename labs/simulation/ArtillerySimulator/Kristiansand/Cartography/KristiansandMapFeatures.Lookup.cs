using Novolis.Agent.Core;

namespace ArtillerySimulator;

/// <summary>Clipped OSM leftovers used for agent map entities (routing uses primary roads.png).</summary>
internal static partial class KristiansandMapFeatures
{
    private static GroundPolyline[]? _visible;

    public static IReadOnlyList<GroundPolyline> Visible =>
        _visible ??= MapFeaturesCatalog.ClipLines(Lines, KristiansandHeightSampler.EastMeters, KristiansandHeightSampler.NorthMeters);

    public static void ResetClipCache() => _visible = null;

    public static AgentEntity[] MapEntities() => MapFeaturesCatalog.MapEntities(Visible);
}
