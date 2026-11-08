using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Drøbak Sound / Oscarsborg frame for the 9 April 1940 educational scenario.
/// EPSG:25833 WGS84 box matched by <c>scripts/build-oslofjorden-map.cs</c> (Kartverket NHM_DTM_25833 via hoydedata.no).
/// Frame is south-heavy: long outer-fjord run-in, short northern pocket past the objective.
/// </summary>
internal static class OslofjordenGeodesy
{
    // Oscarsborg ≈ 59.674 sits ~¾ of the way north in this box so the overview shows
    // a long southern approach and only a short gate toward Oslo.
    public const double SouthLatitude = 59.40;
    public const double NorthLatitude = 59.765;
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

    /// <summary>
    /// Fractional northing of Oscarsborg in this box (0 = south edge, 1 = north edge).
    /// Drøbak pinch / fairway math must track this — never hardcode 0.5.
    /// </summary>
    public static float OscarsborgNorthFraction
    {
        get
        {
            var z = ToWorldMeters(OscarsborgLatitude, OscarsborgLongitude).Y;
            return Math.Clamp(z / MathF.Max(1f, NorthMeters), 0.05f, 0.95f);
        }
    }

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
