namespace ArtillerySimulator.MapPipeline;

/// <summary>
/// NVDB vegsystem.vegkategori: E Europavei, R Riksvei, S Stamvei, F Fylkesvei = main;
/// K Kommunevei, P privat / remaining = local-local (design).
/// </summary>
public static class NvdbRoadHierarchy
{
    public static bool IsMain(string? vegkategori) =>
        vegkategori is "E" or "R" or "S" or "F";

    public static byte EncodeKategori(string? vegkategori)
    {
        if (string.IsNullOrEmpty(vegkategori) || vegkategori.Length != 1)
            return (byte)'?';
        var c = char.ToUpperInvariant(vegkategori[0]);
        return c is 'E' or 'R' or 'S' or 'F' or 'K' or 'P' ? (byte)c : (byte)'?';
    }

    public static string DecodeKategori(byte code) =>
        code is (byte)'E' or (byte)'R' or (byte)'S' or (byte)'F' or (byte)'K' or (byte)'P'
            ? ((char)code).ToString()
            : "?";

    /// <summary>
    /// WGS84 → scenario meters on the shared hoydedata / EPSG:25833 frame
    /// (SW origin = UTM easting/northing of the map rectangle, X east, Z north).
    /// Matches NHM DTM ImageServer when fetched with bboxSR=imageSR=25833.
    /// </summary>
    public static (float X, float Z) LonLatToWorldMeters(
        double latitudeDegrees,
        double longitudeDegrees,
        double utmOriginEasting,
        double utmOriginNorthing)
    {
        var (easting, northing) = Etrs89Utm33.ToEastingNorthing(latitudeDegrees, longitudeDegrees);
        return (
            (float)(easting - utmOriginEasting),
            (float)(northing - utmOriginNorthing));
    }
}
