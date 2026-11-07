using System.Numerics;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>
/// Bilinear sampler over <see cref="KristiansandElevation"/> (Kartverket NDH meters).
/// World X is east, world Z is north, origin at the southwest corner of the EPSG:25833 frame
/// shared with hoydedata NHM / NVDB (not equirectangular WGS84 UV).
/// </summary>
internal sealed class KristiansandHeightSampler : IHeightSampler
{
    // Match layers/georef.json map rectangle @ 1:60 000.
    public const double SouthLatitude = 58.06181603859646;
    public const double NorthLatitude = 58.20818396140355;
    public const double WestLongitude = 7.825738542655645;
    public const double EastLongitude = 8.234261457344356;
    public const double MeanLatitudeDegrees = (SouthLatitude + NorthLatitude) / 2.0;

    public static float EastMeters { get; } = 24008.08f;
    public static float NorthMeters { get; } = 16266.16f;
    public static float MapExtentMeters { get; } = MathF.Max(EastMeters, NorthMeters);

    /// <summary>
    /// SW corner of the scenario rectangle in EPSG:25833
    /// (<c>FrameOriginFromWgs84Box</c> of the cutout + PDF meter extents).
    /// </summary>
    public const double UtmOriginEasting = 78006.06073350424;
    public const double UtmOriginNorthing = 6456827.344437925;

    /// <summary>Peak NDH height in the loaded grid (meters).</summary>
    public static float MaxElevationMeters { get; } = ComputeMaxElevation();

    /// <summary>Draw height is 1:1 NDH meters. Sea at Y=0. No vertical exaggeration.</summary>
    public const float VisualReliefScale = 1f;

    private static float ComputeMaxElevation()
    {
        short max = 0;
        foreach (var h in KristiansandElevation.Meters)
        {
            if (h > max)
                max = h;
        }

        return max;
    }

    public float SampleHeight(float x, float z) => KristiansandSurface.SampleDrawHeight(x, z);

    public float SampleMeters(float x, float z) => SampleMetersStatic(x, z);

    public static float SampleMetersStatic(float x, float z)
    {
        var cells = KristiansandElevation.Count - 1;
        var u = Math.Clamp(KristiansandGeoref.WorldToDataU(x, EastMeters), 0f, 1f) * cells;
        var v = Math.Clamp(z / NorthMeters, 0f, 1f) * cells;
        var i0 = (int)MathF.Floor(u);
        var j0 = (int)MathF.Floor(v);
        var i1 = Math.Min(i0 + 1, cells);
        var j1 = Math.Min(j0 + 1, cells);
        var tx = u - i0;
        var tz = v - j0;
        var h00 = At(i0, j0);
        var h10 = At(i1, j0);
        var h01 = At(i0, j1);
        var h11 = At(i1, j1);
        var south = h00 + (h10 - h00) * tx;
        var north = h01 + (h11 - h01) * tx;
        return south + (north - south) * tz;
    }

    private static float At(int i, int j) => KristiansandElevation.Meters[j * KristiansandElevation.Count + i];

    /// <summary>WGS84 → world meters on the shared hoydedata EPSG:25833 frame.</summary>
    public static Vector2 ToWorldMeters(double latitudeDegrees, double longitudeDegrees)
    {
        var (easting, northing) = Etrs89Utm33N.ToEastingNorthing(latitudeDegrees, longitudeDegrees);
        return new(
            (float)(easting - UtmOriginEasting),
            (float)(northing - UtmOriginNorthing));
    }

    /// <summary>World meters → WGS84 (inverse of <see cref="ToWorldMeters"/>).</summary>
    public static (double Latitude, double Longitude) ToLonLat(float xMeters, float zMeters) =>
        Etrs89Utm33N.FromEastingNorthing(UtmOriginEasting + xMeters, UtmOriginNorthing + zMeters);
}
