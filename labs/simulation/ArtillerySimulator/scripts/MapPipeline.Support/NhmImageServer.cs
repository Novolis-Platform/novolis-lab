using System.Globalization;

namespace ArtillerySimulator.MapPipeline;

/// <summary>Kartverket hoydedata.no ArcGIS ImageServer float TIFF export.</summary>
public static class NhmImageServer
{
    public const string Dtm25833 = "NHM_DTM_25833";
    public const string Dom25833 = "NHM_DOM_25833";
    public const string Topobathy25833 = "NHM_DTM_TOPOBATHY_25833";

    /// <summary>
    /// Export a UTM33-axis-aligned rectangle (matches Norgeskart PDF frame / scenario meters).
    /// Prefer this over WGS84 bbox — ArcGIS AABB of a lon/lat box warps content vs the PDF.
    /// </summary>
    public static Task<byte[]> FetchFloatTiffUtmAsync(
        HttpClient http,
        string service,
        double minEasting,
        double minNorthing,
        double maxEasting,
        double maxNorthing,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var bbox = string.Create(
            CultureInfo.InvariantCulture,
            $"{minEasting},{minNorthing},{maxEasting},{maxNorthing}");
        return FetchAsync(http, service, bbox, bboxSr: 25833, imageSr: 25833, width, height, cancellationToken);
    }

    /// <summary>Legacy WGS84 bbox → imageSR 25833 (AABB). Prefer <see cref="FetchFloatTiffUtmAsync"/>.</summary>
    public static Task<byte[]> FetchFloatTiffAsync(
        HttpClient http,
        string service,
        double westLon,
        double southLat,
        double eastLon,
        double northLat,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var bbox = string.Create(CultureInfo.InvariantCulture, $"{westLon},{southLat},{eastLon},{northLat}");
        return FetchAsync(http, service, bbox, bboxSr: 4326, imageSr: 25833, width, height, cancellationToken);
    }

    private static async Task<byte[]> FetchAsync(
        HttpClient http,
        string service,
        string bbox,
        int bboxSr,
        int imageSr,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://hoydedata.no/arcgis/rest/services/{service}/ImageServer/exportImage" +
            $"?bbox={bbox}&bboxSR={bboxSr}&imageSR={imageSr}&size={width},{height}" +
            "&format=tiff&pixelType=F32&noDataInterpretation=esriNoDataMatchAny" +
            "&interpolation=RSP_BilinearInterpolation&f=image";
        Console.WriteLine($"GET {service} @ {width}×{height} (SR {bboxSr}→{imageSr})…");
        var bytes = await http.GetByteArrayAsync(url, cancellationToken);
        if (bytes.Length < 8 || bytes[0] != (byte)'I' || bytes[1] != (byte)'I')
            throw new InvalidDataException($"{service}: not little-endian TIFF ({bytes.Length} bytes)");
        Console.WriteLine($"  {bytes.Length:N0} bytes");
        return bytes;
    }
}
