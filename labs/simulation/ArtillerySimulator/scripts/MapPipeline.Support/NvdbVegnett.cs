using System.Globalization;
using System.Text.Json;

namespace ArtillerySimulator.MapPipeline;

/// <summary>NVDB vegnett segmentert API — car traffic (trafikantgruppe=K).</summary>
public static class NvdbVegnett
{
    public readonly record struct RawSegment(string Vegkategori, string Wkt);

    public static async Task<List<RawSegment>> FetchCarSegmentsAsync(
        HttpClient http,
        double westLon,
        double southLat,
        double eastLon,
        double northLat,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("GET NVDB vegnett segmentert (trafikantgruppe=K)…");
        var list = new List<RawSegment>(28_000);
        string? start = null;
        var pages = 0;
        while (true)
        {
            var url =
                "https://nvdbapiles.atlas.vegvesen.no/vegnett/api/v4/veglenkesekvenser/segmentert" +
                string.Create(CultureInfo.InvariantCulture,
                    $"?kartutsnitt={westLon},{southLat},{eastLon},{northLat}&srid=4326&antall=1000&trafikantgruppe=K");
            if (start is not null)
                url += "&start=" + Uri.EscapeDataString(start);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.vegvesen.nvdb-v3-rev2+json");
            req.Headers.TryAddWithoutValidation("X-Client", "Novolis-ArtillerySimulator");
            using var resp = await http.SendAsync(req, cancellationToken);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            if (!root.TryGetProperty("objekter", out var objekter))
                break;

            foreach (var obj in objekter.EnumerateArray())
            {
                if (!obj.TryGetProperty("geometri", out var geom)
                    || !geom.TryGetProperty("wkt", out var wktEl))
                    continue;
                var wkt = wktEl.GetString();
                if (string.IsNullOrEmpty(wkt))
                    continue;
                list.Add(new RawSegment(ReadVegkategori(obj), wkt));
            }

            pages++;
            start = null;
            if (root.TryGetProperty("metadata", out var meta)
                && meta.TryGetProperty("neste", out var neste)
                && neste.ValueKind == JsonValueKind.Object
                && neste.TryGetProperty("start", out var startEl)
                && startEl.ValueKind == JsonValueKind.String)
            {
                start = startEl.GetString();
            }

            if (string.IsNullOrEmpty(start) || objekter.GetArrayLength() == 0)
                break;

            if (pages % 5 == 0)
                Console.WriteLine($"  … {list.Count:N0} segments ({pages} pages)");
        }

        Console.WriteLine($"  {list.Count:N0} car segments ({pages} pages)");
        return list;
    }

    public static string ReadVegkategori(JsonElement obj)
    {
        if (obj.TryGetProperty("vegsystemreferanse", out var vsr)
            && vsr.TryGetProperty("vegsystem", out var vs)
            && vs.TryGetProperty("vegkategori", out var kat)
            && kat.ValueKind == JsonValueKind.String)
        {
            var s = kat.GetString();
            if (!string.IsNullOrEmpty(s))
                return s;
        }

        return "?";
    }

    /// <summary>NVDB srid=4326 LINESTRING Z is lat lon z (observed).</summary>
    public static bool TryParseLineStringZ(string wkt, List<(double Lat, double Lon)> dst)
    {
        dst.Clear();
        const string prefixZ = "LINESTRING Z (";
        var idx = wkt.IndexOf(prefixZ, StringComparison.OrdinalIgnoreCase);
        var prefixLen = prefixZ.Length;
        if (idx < 0)
        {
            idx = wkt.IndexOf("LINESTRING (", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;
            prefixLen = "LINESTRING (".Length;
        }

        idx += prefixLen;
        var end = wkt.LastIndexOf(')');
        if (end <= idx)
            return false;

        var body = wkt.AsSpan(idx, end - idx);
        foreach (var part in body.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length < 2)
                continue;
            if (!double.TryParse(bits[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                continue;
            if (!double.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                continue;
            dst.Add((lat, lon));
        }

        return dst.Count >= 2;
    }

    public static List<NvdbRoadsBinary.Segment> ToScenarioSegments(
        IEnumerable<RawSegment> raw,
        double utmOriginEasting,
        double utmOriginNorthing)
    {
        var scratch = new List<(double Lat, double Lon)>(64);
        var result = new List<NvdbRoadsBinary.Segment>(28_000);
        foreach (var seg in raw)
        {
            if (!TryParseLineStringZ(seg.Wkt, scratch))
                continue;
            var pts = new (float X, float Z)[scratch.Count];
            for (var i = 0; i < scratch.Count; i++)
            {
                pts[i] = NvdbRoadHierarchy.LonLatToWorldMeters(
                    scratch[i].Lat,
                    scratch[i].Lon,
                    utmOriginEasting,
                    utmOriginNorthing);
            }

            var kat = NvdbRoadHierarchy.EncodeKategori(seg.Vegkategori);
            result.Add(new NvdbRoadsBinary.Segment(
                kat,
                NvdbRoadHierarchy.IsMain(NvdbRoadHierarchy.DecodeKategori(kat)),
                pts));
        }

        return result;
    }
}
