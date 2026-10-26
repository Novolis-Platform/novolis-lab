using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CalypsoCad.Generation.Blueprints;

/// <summary>
/// Writes the manufacturer HTML sheets from CAL-INT-GA-001.json.
/// Point clouds stay in CAL-HULL-CAD-001.json and CAL-HULL-CAD-001.obj.
/// </summary>
internal static class ManufacturerSheetEmitter
{
    public static string Emit(string? lockJsonPath = null, string? outputDirectory = null)
    {
        var jsonPath = lockJsonPath ?? ResolveLockPath();
        var internDir = Path.GetDirectoryName(jsonPath) ?? throw new InvalidOperationException("No lock directory");
        var outDir = outputDirectory ?? Path.GetFullPath(Path.Combine(internDir, "..", "manufacturer"));
        Directory.CreateDirectory(outDir);

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var env = root.GetProperty("envelope");
        var hold = root.GetProperty("hold");
        var stations = root.GetProperty("hullLoft").GetProperty("stations");

        Write(outDir, "CAL-HULL-GA-001.html", GeneralArrangement(env, hold, stations));
        Write(outDir, "CAL-HULL-MESH-001.html", MeshSheet(env, stations));
        Write(outDir, "CAL-HULL-CAD-001.html", CadSheet(env, hold, stations));
        return outDir;
    }

    private static void Write(string dir, string name, string html) =>
        File.WriteAllText(Path.Combine(dir, name), html, Encoding.UTF8);

    private static string GeneralArrangement(JsonElement env, JsonElement hold, JsonElement stations)
    {
        var body = new StringBuilder();
        body.Append(Head("CAL-HULL-GA-001 — general arrangement"));
        body.Append("<h1>CAL-HULL-GA-001 — general arrangement</h1>");
        body.Append(Regen("CAL-HULL-GA-001"));
        body.Append(EnvelopeTable(env));
        body.Append(StationTable(stations));
        body.Append(HoldTable(hold));
        body.Append("<p>Flat-plate pepakura. No nacelle, blister, or side-pod solids. Frames and the aft ramp are CAD entities from the lock, not a second hull.</p>");
        body.Append("</body></html>");
        return body.ToString();
    }

    private static string MeshSheet(JsonElement env, JsonElement stations)
    {
        var body = new StringBuilder();
        body.Append(Head("CAL-HULL-MESH-001 — mesh"));
        body.Append("<h1>CAL-HULL-MESH-001 — mesh</h1>");
        body.Append(Regen("CAL-HULL-MESH-001"));
        body.Append("<p>Machine mesh: <code>CAL-HULL-CAD-001.json</code> and <code>CAL-HULL-CAD-001.obj</code>. This sheet does not rewrite those point clouds.</p>");
        body.Append(EnvelopeTable(env));
        body.Append(StationTable(stations));
        body.Append("</body></html>");
        return body.ToString();
    }

    private static string CadSheet(JsonElement env, JsonElement hold, JsonElement stations)
    {
        var body = new StringBuilder();
        body.Append(Head("CAL-HULL-CAD-001 — material"));
        body.Append("<h1>CAL-HULL-CAD-001 — material and mold lines</h1>");
        body.Append(Regen("CAL-HULL-CAD-001"));
        body.Append("<p>AISI 316L, t = 8 mm, flat plate, no press-brake bends. Skin mass stays on the OML area in the hull JSON.</p>");
        body.Append(EnvelopeTable(env));
        body.Append(HoldTable(hold));
        body.Append(StationTable(stations));
        body.Append("<h2>Later revision</h2><ol><li>Unfolded DXF nests</li><li>Flight allowables and GD&amp;T</li><li>STEP AP242</li></ol>");
        body.Append("</body></html>");
        return body.ToString();
    }

    private static string Regen(string drawing) =>
        $"<p class=\"meta\">{drawing}. Regenerate: <code>dotnet run --project d:\\novolis\\novolis-lab\\labs\\cad\\CalypsoCad\\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --blueprints</code>.</p>";

    private static string EnvelopeTable(JsonElement env)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Envelope</h2><table><tr><th>Item</th><th>Value</th></tr>");
        foreach (var name in new[] { "LOA", "BEAM", "OAH", "L_FORE", "L_MID", "L_AFT", "CHAMFER", "T_SHELL", "MATERIAL" })
        {
            if (!env.TryGetProperty(name, out var value))
                continue;
            sb.Append("<tr><td>").Append(name).Append("</td><td>").Append(value).Append("</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string HoldTable(JsonElement hold)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Hold door</h2><table><tr><th>Item</th><th>m</th></tr>");
        foreach (var name in new[] { "DOOR_W", "DOOR_H", "SILL", "APRON_D", "HOLD_L" })
        {
            if (!hold.TryGetProperty(name, out var value))
                continue;
            sb.Append("<tr><td>").Append(name).Append("</td><td>").Append(Fmt(value)).Append("</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string StationTable(JsonElement stations)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Stations from the stem</h2><table><tr><th>Id</th><th>z</th><th>half beam</th><th>half height</th></tr>");
        foreach (var station in stations.EnumerateArray())
        {
            sb.Append("<tr><td>").Append(station.GetProperty("id").GetString())
                .Append("</td><td>").Append(Fmt(station.GetProperty("zFromStem")))
                .Append("</td><td>").Append(Fmt(station.GetProperty("halfBeam")))
                .Append("</td><td>").Append(Fmt(station.GetProperty("halfHeight")))
                .Append("</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string Fmt(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number
            ? value.GetDouble().ToString("0.###", CultureInfo.InvariantCulture)
            : value.ToString();

    private static string Head(string title) =>
        $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/><title>{title}</title></head><body>";

    private static string ResolveLockPath()
    {
        var candidates = new[]
        {
            @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json",
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs", "internals", "CAL-INT-GA-001.json")),
            Path.Combine(AppContext.BaseDirectory, "lock", "CAL-INT-GA-001.json"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("CAL-INT-GA-001.json was not found.");
    }
}
