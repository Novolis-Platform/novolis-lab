namespace ArtillerySimulator.MapPipeline;

/// <summary>
/// ETRS89 / WGS84 ↔ UTM zone 33N (EPSG:25833). Sufficient for Kristiansand map georef.
/// </summary>
public static class Etrs89Utm33
{
    private const double A = 6378137.0;
    private const double F = 1.0 / 298.257222101; // GRS80
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

    /// <summary>
    /// SW corner of a UTM-axis-aligned map frame centered on the WGS84 box, sized to
    /// <paramref name="eastMeters"/> × <paramref name="northMeters"/> (PDF footer extents).
    /// </summary>
    public static (double OriginEasting, double OriginNorthing) FrameOriginFromWgs84Box(
        double southLat,
        double westLon,
        double northLat,
        double eastLon,
        double eastMeters,
        double northMeters)
    {
        var center = ToEastingNorthing((southLat + northLat) * 0.5, (westLon + eastLon) * 0.5);
        return (center.Easting - eastMeters * 0.5, center.Northing - northMeters * 0.5);
    }

    /// <summary>
    /// Axis-aligned UTM envelope of a WGS84 rectangle — matches ArcGIS exportImage when
    /// <c>bboxSR=4326</c> and <c>imageSR=25833</c>.
    /// </summary>
    public static (double MinE, double MinN, double MaxE, double MaxN) AabbOfWgs84Box(
        double southLat,
        double westLon,
        double northLat,
        double eastLon)
    {
        var sw = ToEastingNorthing(southLat, westLon);
        var se = ToEastingNorthing(southLat, eastLon);
        var nw = ToEastingNorthing(northLat, westLon);
        var ne = ToEastingNorthing(northLat, eastLon);
        var minE = Math.Min(Math.Min(sw.Easting, se.Easting), Math.Min(nw.Easting, ne.Easting));
        var maxE = Math.Max(Math.Max(sw.Easting, se.Easting), Math.Max(nw.Easting, ne.Easting));
        var minN = Math.Min(Math.Min(sw.Northing, se.Northing), Math.Min(nw.Northing, ne.Northing));
        var maxN = Math.Max(Math.Max(sw.Northing, se.Northing), Math.Max(nw.Northing, ne.Northing));
        return (minE, minN, maxE, maxN);
    }

    /// <summary>
    /// Pixel in a north-up UTM AABB TIFF (row 0 = north) for a WGS84 point.
    /// </summary>
    public static (double PixelX, double PixelY) LonLatToUtmAabbPixel(
        double latitudeDegrees,
        double longitudeDegrees,
        double minE,
        double minN,
        double maxE,
        double maxN,
        int pixelWidth,
        int pixelHeight)
    {
        var (e, n) = ToEastingNorthing(latitudeDegrees, longitudeDegrees);
        var px = (e - minE) / Math.Max(1e-9, maxE - minE) * (pixelWidth - 1);
        var py = (maxN - n) / Math.Max(1e-9, maxN - minN) * (pixelHeight - 1);
        return (px, py);
    }
}
