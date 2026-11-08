// Fail-closed: every bh-* group centroid pierces the ship spine.
// Spine = mid athwart (X≈0) × mid height (SectionAt.mid = HullHeight/2) × stem→stern.
//   dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\scripts\verify-calypso-bh-spine.cs
#:package Novolis.Cad.Primitives@2026.1.*
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Globalization;
using System.Text.Json;
using Novolis.Cad.Primitives;

const float Loa = 69f;
const float TolX = 0.05f;
const float TolUp = 0.15f;

var generated = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Novolis", "CalypsoCad", "generated");
var cadPath = Path.Combine(generated, "calypso.cadjson");
if (!File.Exists(cadPath))
{
    Console.Error.WriteLine($"Missing {cadPath} — run CalypsoCad --generate-only first.");
    Environment.Exit(1);
}

// Prefer LLM audit artifact when present (same gates); still recompute from cadjson.
var auditPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\renders\audit\structure-audit.json";

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var doc = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath), jsonOpts)!;
var oahMid = LoadOahMid(); // loft verts / BH spine = OAH/2, not height/2 from keel

var groups = new Dictionary<string, List<CadEntity>>(StringComparer.OrdinalIgnoreCase);
foreach (var e in doc.Entities)
{
    // Heavy BH plates are Kind=mesh; deck seats remain boxes. Both share the BH assembly key.
    if ((e.Kind is not ("box" or "mesh")) || e.Name is null || e.Points is not { Count: >= 2 })
        continue;
    var key = BhGroupKey(e.Name);
    if (key is null)
        continue;
    if (!groups.TryGetValue(key, out var list))
    {
        list = [];
        groups[key] = list;
    }

    list.Add(e);
}

Console.WriteLine("Calypso BH spine pierce (|X|<0.05, |Δup|<0.15 vs SectionAt.mid)");
Console.WriteLine($"{"id",-22} {"n",4} {"X",8} {"Y",8} {"mid",8} {"Δup",8} {"zStem",8} status");

var failures = 0;
var required = new[]
{
    "bh-BOW-COLLISION",
    "bh-CROSS-FORE",
    "bh-CROSS-AFT",
    "bh-CREW-AFT",
    "bh-ENG-FORE",
    "bh-ENG-AFT",
};
foreach (var req in required)
{
    if (!groups.ContainsKey(req))
    {
        Console.WriteLine($"  FAIL missing {req}");
        failures++;
    }
}

foreach (var (key, cells) in groups.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
{
    double sumV = 0, cx = 0, cy = 0, cz = 0;
    foreach (var e in cells)
    {
        var c = e.Points![0];
        var he = e.Points[1];
        var hx = Math.Abs((double)he[0]);
        var hy = Math.Abs((double)he[1]);
        var hz = Math.Abs((double)he[2]);
        var v = Math.Max(1e-6, 8.0 * hx * hy * hz);
        sumV += v;
        cx += c[0] * v;
        cy += c[1] * v;
        cz += c[2] * v;
    }

    cx /= sumV;
    cy /= sumV;
    cz /= sumV;
    var zStem = Loa * 0.5 - cz;
    var mid = oahMid;
    var absX = Math.Abs(cx);
    var deltaUp = cy - mid;
    var pass = absX <= TolX && Math.Abs(deltaUp) <= TolUp;
    if (!pass)
        failures++;

    Console.WriteLine(
        $"{key,-22} {cells.Count,4} {cx,8:0.000} {cy,8:0.000} {mid,8:0.000} {deltaUp,8:0.000} {zStem,8:0.00} {(pass ? "OK" : "FAIL")}");
}

if (File.Exists(auditPath))
    Console.WriteLine($"Audit artifact: {auditPath}");
else
    Console.WriteLine($"Note: missing audit artifact at {auditPath} (run --structure-audit)");

Environment.Exit(failures == 0 ? 0 : 1);

static string? BhGroupKey(string name)
{
    if (!name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
        return null;
    var rest = name["bh-".Length..];
    var cut = rest.IndexOf("-plate", StringComparison.OrdinalIgnoreCase);
    if (cut < 0)
        cut = rest.IndexOf("-deckseat", StringComparison.OrdinalIgnoreCase);
    if (cut < 0)
        cut = rest.IndexOf("-web-", StringComparison.OrdinalIgnoreCase);
    if (cut < 0)
        cut = rest.IndexOf("-webplate-", StringComparison.OrdinalIgnoreCase);
    return cut > 0 ? "bh-" + rest[..cut] : "bh-" + rest;
}

static float LoadOahMid()
{
    var lockPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json";
    if (!File.Exists(lockPath))
        return 6f;

    using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
    if (doc.RootElement.TryGetProperty("envelope", out var env)
        && env.TryGetProperty("OAH", out var oah)
        && oah.ValueKind == JsonValueKind.Number)
        return oah.GetSingle() * 0.5f;

    return 6.2f;
}
