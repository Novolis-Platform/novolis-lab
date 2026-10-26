using System.Numerics;
using System.Text.Json;

namespace CalypsoCad.Generation.Fabrication;

internal sealed class ManufacturerHullDocument
{
    public required Dictionary<string, Vector3> Points { get; init; }

    public required List<HullFace> OmlFaces { get; init; }

    public float ThicknessM { get; init; }

    public float DensityKgM3 { get; init; }

    public float SheetWidthM { get; init; }

    public float SheetHeightM { get; init; }

    public string MaterialDesignation { get; init; } = "AISI 316L";

    public string WeldProcess { get; init; } = "GTAW";

    public string WeldFiller { get; init; } = "ER316L";

    public float GoldenSkinAreaM2 { get; init; }

    internal sealed record HullFace(string Id, string Group, string[] VertIds);

    public static ManufacturerHullDocument Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var points = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var p in root.GetProperty("points").EnumerateArray())
        {
            var id = p.GetProperty("id").GetString() ?? "";
            var x = (float)p.GetProperty("x").GetDouble();
            var y = (float)p.GetProperty("y").GetDouble();
            var z = (float)p.GetProperty("z").GetDouble();
            points[id] = new Vector3(x, y, z);
        }

        var faces = new List<HullFace>();
        foreach (var f in root.GetProperty("faces").EnumerateArray())
        {
            if (!string.Equals(f.GetProperty("shell").GetString(), "OML", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = f.GetProperty("id").GetString() ?? "";
            var group = f.TryGetProperty("group", out var g) ? g.GetString() ?? "" : "";
            var verts = f.GetProperty("verts").EnumerateArray().Select(v => v.GetString() ?? "").ToArray();
            if (verts.Length >= 4)
                faces.Add(new HullFace(id, group, verts));
        }

        var t = 0.008f;
        if (root.TryGetProperty("envelope", out var env) && env.TryGetProperty("T", out var te))
            t = (float)te.GetDouble();
        var density = 8000f;
        if (root.TryGetProperty("material", out var mat) && mat.TryGetProperty("density_kg_m3", out var d))
            density = (float)d.GetDouble();
        var matName = "AISI 316L";
        if (root.TryGetProperty("material", out mat) && mat.TryGetProperty("designation", out var des))
            matName = des.GetString() ?? matName;
        var sheetW = 2f;
        var sheetH = 6f;
        if (root.TryGetProperty("process", out var proc) && proc.TryGetProperty("stock_sheet_m", out var stock)
            && stock.GetArrayLength() >= 2)
        {
            sheetW = (float)stock[0].GetDouble();
            sheetH = (float)stock[1].GetDouble();
        }

        var golden = 0f;
        if (root.TryGetProperty("massProperties", out var mp) && mp.TryGetProperty("outerSkinArea_m2", out var a))
            golden = (float)a.GetDouble();

        var weldProc = "GTAW";
        var filler = "ER316L";
        if (root.TryGetProperty("material", out mat) && mat.TryGetProperty("weld", out var weld))
        {
            if (weld.TryGetProperty("process", out var wp))
                weldProc = wp.GetString() ?? weldProc;
            if (weld.TryGetProperty("filler", out var wf))
                filler = wf.GetString() ?? filler;
        }

        return new ManufacturerHullDocument
        {
            Points = points,
            OmlFaces = faces,
            ThicknessM = t,
            DensityKgM3 = density,
            SheetWidthM = sheetW,
            SheetHeightM = sheetH,
            MaterialDesignation = matName,
            WeldProcess = weldProc,
            WeldFiller = filler,
            GoldenSkinAreaM2 = golden,
        };
    }
}
