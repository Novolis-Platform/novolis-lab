namespace ArtillerySimulator;

/// <summary>ETRS89 / WGS84 ↔ UTM zone 33N (EPSG:25833) — runtime twin of MapPipeline Etrs89Utm33.</summary>
internal static class Etrs89Utm33N
{
    private const double A = 6378137.0;
    private const double F = 1.0 / 298.257222101;
    private const double K0 = 0.9996;
    private const double Lon0 = 15.0 * Math.PI / 180.0;
    private const double FalseEasting = 500_000.0;

    public static (double Easting, double Northing) ToEastingNorthing(double latitudeDegrees, double longitudeDegrees)
    {
        var e2 = F * (2.0 - F);
        var ep2 = e2 / (1.0 - e2);
        var lat = latitudeDegrees * Math.PI / 180.0;
        var lon = longitudeDegrees * Math.PI / 180.0;
        var sin = Math.Sin(lat);
        var cos = Math.Cos(lat);
        var tan = Math.Tan(lat);
        var n = A / Math.Sqrt(1.0 - e2 * sin * sin);
        var t = tan * tan;
        var c = ep2 * cos * cos;
        var aa = cos * (lon - Lon0);
        var m = A * ((1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0) * lat
            - (3.0 * e2 / 8.0 + 3.0 * e2 * e2 / 32.0 + 45.0 * e2 * e2 * e2 / 1024.0) * Math.Sin(2.0 * lat)
            + (15.0 * e2 * e2 / 256.0 + 45.0 * e2 * e2 * e2 / 1024.0) * Math.Sin(4.0 * lat)
            - (35.0 * e2 * e2 * e2 / 3072.0) * Math.Sin(6.0 * lat));
        var easting = FalseEasting + K0 * n * (aa
            + (1.0 - t + c) * aa * aa * aa / 6.0
            + (5.0 - 18.0 * t + t * t + 72.0 * c - 58.0 * ep2) * aa * aa * aa * aa * aa / 120.0);
        var northing = K0 * (m + n * tan * (aa * aa / 2.0
            + (5.0 - t + 9.0 * c + 4.0 * c * c) * aa * aa * aa * aa / 24.0
            + (61.0 - 58.0 * t + t * t + 600.0 * c - 330.0 * ep2) * aa * aa * aa * aa * aa * aa / 720.0));
        return (easting, northing);
    }

    public static (double LatitudeDegrees, double LongitudeDegrees) FromEastingNorthing(double easting, double northing)
    {
        var e2 = F * (2.0 - F);
        var ep2 = e2 / (1.0 - e2);
        var e1 = (1.0 - Math.Sqrt(1.0 - e2)) / (1.0 + Math.Sqrt(1.0 - e2));
        var m = northing / K0;
        var mu = m / (A * (1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0));
        var phi1 = mu
            + (3.0 * e1 / 2.0 - 27.0 * e1 * e1 * e1 / 32.0) * Math.Sin(2.0 * mu)
            + (21.0 * e1 * e1 / 16.0 - 55.0 * e1 * e1 * e1 * e1 / 32.0) * Math.Sin(4.0 * mu)
            + (151.0 * e1 * e1 * e1 / 96.0) * Math.Sin(6.0 * mu);
        var sin = Math.Sin(phi1);
        var cos = Math.Cos(phi1);
        var tan = Math.Tan(phi1);
        var n1 = A / Math.Sqrt(1.0 - e2 * sin * sin);
        var t1 = tan * tan;
        var c1 = ep2 * cos * cos;
        var r1 = A * (1.0 - e2) / Math.Pow(1.0 - e2 * sin * sin, 1.5);
        var d = (easting - FalseEasting) / (n1 * K0);
        var lat = phi1 - (n1 * tan / r1) * (d * d / 2.0
            - (5.0 + 3.0 * t1 + 10.0 * c1 - 4.0 * c1 * c1 - 9.0 * ep2) * d * d * d * d / 24.0
            + (61.0 + 90.0 * t1 + 298.0 * c1 + 45.0 * t1 * t1 - 252.0 * ep2 - 3.0 * c1 * c1)
            * d * d * d * d * d * d / 720.0);
        var lon = Lon0 + (d
            - (1.0 + 2.0 * t1 + c1) * d * d * d / 6.0
            + (5.0 - 2.0 * c1 + 28.0 * t1 - 3.0 * c1 * c1 + 8.0 * ep2 + 24.0 * t1 * t1)
            * d * d * d * d * d / 120.0) / cos;
        return (lat * 180.0 / Math.PI, lon * 180.0 / Math.PI);
    }
}
