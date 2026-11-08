using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>
/// Drapes committed <c>map/layers/nvdb-roads.bin</c> onto the NDH mesh.
/// Main = E/R/S/F (single dark-gray centerline); local K/P/… optional thin gray. Never reads PNG.
/// Geometry outside the heightfield cutout is discarded — nothing is clamped onto the border.
/// </summary>
internal static class KristiansandNvdbRoads
{
    private static readonly Color PrimaryColor = Color.FromArgb(255, 36, 36, 36);
    private static readonly Color SecondaryColor = Color.FromArgb(220, 48, 48, 48);
    /// <summary>Drape spacing — dense enough that roads follow relief and stay continuous.</summary>
    private const float DrapeMeters = 40f;
    private const float SecondaryDrapeMeters = 70f;
    /// <summary>Skip local K/P strokes in the runtime mesh — primary roads carry OPFOR + map intent.</summary>
    private const bool DrawSecondaryRoads = false;

    private static NvdbRoadsBinaryLoader.RoadStroke[]? _strokes;
    private static bool _loadAttempted;

    public static WorldLineVertex[] Build(IHeightSampler heights)
    {
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return [];

        var batch = new SilkWireBatch();
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        foreach (var stroke in _strokes)
        {
            if (!stroke.Primary && !DrawSecondaryRoads)
                continue;
            var color = stroke.Primary ? PrimaryColor : SecondaryColor;
            var drape = stroke.Primary ? DrapeMeters : SecondaryDrapeMeters;
            NvdbRoadDraper.DrapePolyline(batch, heights, east, north, stroke.Points, color, drape);
        }

        return batch.AsSpan().ToArray();
    }

    /// <summary>Primary (E/R/S/F) consecutive vertex pairs for column routing — on-map only.</summary>
    public static IReadOnlyList<(Vector2 A, Vector2 B)> PrimarySegments()
    {
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return [];

        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var list = new List<(Vector2, Vector2)>(65_536);
        foreach (var stroke in _strokes)
        {
            if (!stroke.Primary || stroke.Points.Length < 2)
                continue;
            for (var i = 1; i < stroke.Points.Length; i++)
            {
                var a = stroke.Points[i - 1];
                var b = stroke.Points[i];
                if (!NvdbMapClip.ClipSegmentToMap(ref a, ref b, east, north))
                    continue;
                list.Add((a, b));
            }
        }

        return list;
    }

    public static void Reset()
    {
        _strokes = null;
        _loadAttempted = false;
        PrimaryRoadNetwork.Reset();
    }

    /// <summary>Kept for call-site compatibility with older clip helpers.</summary>
    internal static bool ClipSegmentToMap(ref Vector2 a, ref Vector2 b, float east, float north) =>
        NvdbMapClip.ClipSegmentToMap(ref a, ref b, east, north);

    private static void EnsureLoaded()
    {
        if (_loadAttempted)
            return;
        _loadAttempted = true;
        _strokes = NvdbRoadsBinaryLoader.LoadOnMapStrokes(
            KristiansandHeightSampler.EastMeters,
            KristiansandHeightSampler.NorthMeters);
    }
}
