using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Drøbak Sound / Oscarsborg frame for the 9 April 1940 educational scenario.
/// Approximate EPSG:25833 box covering the narrows — not a full Kartverket bake yet.
/// Southern edge is stretched ~20% beyond the original cutout so the fleet has a longer run-in.
/// </summary>
internal static class OslofjordenGeodesy
{
    // Original educational box was 59.58–59.78 (0.20°). Keep the north gate; push south to 0.24° span (+20%).
    public const double SouthLatitude = 59.54;
    public const double NorthLatitude = 59.78;
    public const double WestLongitude = 10.48;
    public const double EastLongitude = 10.78;
    public const double MeanLatitudeDegrees = (SouthLatitude + NorthLatitude) / 2.0;

    /// <summary>Oscarsborg main island (Kaholmen) — coastal battery site.</summary>
    public const double OscarsborgLatitude = 59.6736;
    public const double OscarsborgLongitude = 10.6072;

    /// <summary>Northern breakthrough objective (Oslo approaches / Bunnefjorden gate).</summary>
    public const double ObjectiveLatitude = 59.755;
    public const double ObjectiveLongitude = 10.65;

    public static float EastMeters { get; } = ComputeEastMeters();
    public static float NorthMeters { get; } = ComputeNorthMeters();
    public static float MapExtentMeters => MathF.Max(EastMeters, NorthMeters);

    public static double UtmOriginEasting { get; } =
        Etrs89Utm33N.ToEastingNorthing(SouthLatitude, WestLongitude).Easting;

    public static double UtmOriginNorthing { get; } =
        Etrs89Utm33N.ToEastingNorthing(SouthLatitude, WestLongitude).Northing;

    public static Vector2 ToWorldMeters(double latitudeDegrees, double longitudeDegrees)
    {
        var (easting, northing) = Etrs89Utm33N.ToEastingNorthing(latitudeDegrees, longitudeDegrees);
        return new(
            (float)(easting - UtmOriginEasting),
            (float)(northing - UtmOriginNorthing));
    }

    public static (double Latitude, double Longitude) ToLonLat(float xMeters, float zMeters)
    {
        var (lat, lon) = Etrs89Utm33N.FromEastingNorthing(
            UtmOriginEasting + xMeters,
            UtmOriginNorthing + zMeters);
        return (lat, lon);
    }

    private static float ComputeEastMeters()
    {
        var sw = Etrs89Utm33N.ToEastingNorthing(SouthLatitude, WestLongitude);
        var se = Etrs89Utm33N.ToEastingNorthing(SouthLatitude, EastLongitude);
        return (float)(se.Easting - sw.Easting);
    }

    private static float ComputeNorthMeters()
    {
        var sw = Etrs89Utm33N.ToEastingNorthing(SouthLatitude, WestLongitude);
        var nw = Etrs89Utm33N.ToEastingNorthing(NorthLatitude, WestLongitude);
        return (float)(nw.Northing - sw.Northing);
    }
}
