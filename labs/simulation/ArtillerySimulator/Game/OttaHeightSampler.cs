using Novolis.Physics.Abstractions;

namespace ArtillerySimulator.Game;

/// <summary>
/// Bilinear sampler over <see cref="OttaElevation"/>. World X is east, world Z is north, origin at the southwest corner.
/// Longitude uses a spherical scale at the mean latitude. The square range pads the shorter east-west side by clamping.
/// </summary>
internal sealed class OttaHeightSampler : IHeightSampler
{
    public const double SouthLatitude = 61.68;
    public const double NorthLatitude = 61.86;
    public const double WestLongitude = 9.38;
    public const double EastLongitude = 9.70;
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
        var cells = OttaElevation.Count - 1;
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

    private static float At(int i, int j) => OttaElevation.Meters[j * OttaElevation.Count + i];
}
