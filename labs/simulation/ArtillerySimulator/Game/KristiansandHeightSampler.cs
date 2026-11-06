using System.Numerics;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator.Game;

/// <summary>
/// Bilinear sampler over <see cref="KristiansandElevation"/>. World X is east, world Z is north, origin at the southwest corner.
/// </summary>
internal sealed class KristiansandHeightSampler : IHeightSampler
{
    // Aligned to relief PDF map frame @ 1:60 000 (ETRS89/UTM 33N footer), Kristiansand-centered.
    public const double SouthLatitude = 58.06166;
    public const double NorthLatitude = 58.20834;
    public const double WestLongitude = 7.8254;
    public const double EastLongitude = 8.2346;
    public const double MeanLatitudeDegrees = (SouthLatitude + NorthLatitude) / 2.0;
    public const double MetersPerDegreeLatitude = 111_132.0;
    public const double MetersPerDegreeLongitudeAtEquator = 111_320.0;

    public static float EastMeters { get; } = (float)(
        (EastLongitude - WestLongitude)
        * MetersPerDegreeLongitudeAtEquator
        * Math.Cos(MeanLatitudeDegrees * Math.PI / 180.0));

    public static float NorthMeters { get; } = (float)((NorthLatitude - SouthLatitude) * MetersPerDegreeLatitude);

    public static float MapExtentMeters { get; } = MathF.Max(EastMeters, NorthMeters);

    public float SampleHeight(float x, float z)
    {
        if (KristiansandLandSeaMask.IsWater(x, z))
            return 0f;

        var cells = KristiansandElevation.Count - 1;
        var u = Math.Clamp(x / EastMeters, 0f, 1f) * cells;
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

    public static Vector2 ToWorldMeters(double latitudeDegrees, double longitudeDegrees)
    {
        var x = (float)((longitudeDegrees - WestLongitude) / (EastLongitude - WestLongitude) * EastMeters);
        var z = (float)((latitudeDegrees - SouthLatitude) / (NorthLatitude - SouthLatitude) * NorthMeters);
        return new Vector2(x, z);
    }
}
