#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property OutputType=Exe
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property PublishAot=false
#:property PublishTrimmed=false
#:property IsAotCompatible=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

// Calypso lock: corridor clear parity + CROSS clip to CORR face + outer hull grow.
// Deck plate tops and cabin/ENG/HOLD clears stay absolute (unaltered).
//   dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\tools\Patch-CorrHullBh.cs

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

var lockPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json";
var hullPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\manufacturer\CAL-HULL-CAD-001.json";

const double CorrW = 3.0;
const double CorrInner = 5.0;
const double CorrOuter = 8.0;
const double CrossZ0 = 19.5; // 3 m clear F–A centered in 19…23 BH band
const double CrossZ1 = 22.5;
const double NewBeam = 20.8; // +0.4 m each side past prior AIR_OUTER flush
const double NewOah = 12.4;  // +0.2 m keel/crown room; decks stay absolute

var root = JsonNode.Parse(File.ReadAllText(lockPath))
    ?? throw new InvalidOperationException("lock parse failed");

var env = root["envelope"]!.AsObject();
var oldBeam = env["BEAM"]!.GetValue<double>();
var oldOah = env["OAH"]!.GetValue<double>();
env["BEAM"] = NewBeam;
env["OAH"] = NewOah;
env["note"] = "Outer hull grown vs interiors: BEAM/OAH shell only; decks + compartments absolute.";

var circ = root["circulation"]!.AsObject();
circ["CORR_W"] = CorrW;
circ["CORR_INNER"] = CorrInner;
circ["CORR_OUTER"] = CorrOuter;
circ["ACCESS_W"] = CorrW; // same clear width as CORR_P/S
circ["L_CROSS"] = 4; // station band Z_CROSS_FORE…AFT; clear F–A of CROSSING = CORR_W

var accessHalf = CorrW * 0.5; // ±1.5
PatchRect(root, "ACCESS", -accessHalf, accessHalf, keepZ: true, z0: null, z1: null);
PatchRect(root, "STAIRS_P", y1: -accessHalf, keepZ: true);
PatchRect(root, "ELEV_S", y0: accessHalf, keepZ: true);
PatchRect(root, "CROSSING", -CorrOuter, CorrOuter, CrossZ0, CrossZ1);

GrowLoft(root["hullLoft"], oldBeam, NewBeam, oldOah, NewOah);

var opts = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(lockPath, root.ToJsonString(opts) + "\n");
Console.WriteLine($"Patched lock → {lockPath}");
Console.WriteLine($"  ACCESS/CROSS clearW={CorrW}; CROSS y=±{CorrOuter}; z={CrossZ0}…{CrossZ1}");
Console.WriteLine($"  envelope BEAM {oldBeam}→{NewBeam} OAH {oldOah}→{NewOah} (decks/comps absolute)");

if (File.Exists(hullPath))
{
    var hull = JsonNode.Parse(File.ReadAllText(hullPath))
        ?? throw new InvalidOperationException("hull parse failed");
    var hEnv = hull["envelope"]!.AsObject();
    var hOldBeam = hEnv["BEAM"]!.GetValue<double>();
    var hOldOah = hEnv["OAH"]!.GetValue<double>();
    hEnv["BEAM"] = NewBeam;
    hEnv["OAH"] = NewOah;
    GrowHullRings(hull["rings"], hOldBeam, NewBeam, hOldOah, NewOah);
    GrowHullPoints(hull["points"], hOldBeam, NewBeam, hOldOah, NewOah);
    File.WriteAllText(hullPath, hull.ToJsonString(opts) + "\n");
    Console.WriteLine($"Patched manufacturer hull envelope {hOldBeam}×{hOldOah} → {NewBeam}×{NewOah}");
}

static void PatchRect(
    JsonNode root,
    string id,
    double? y0 = null,
    double? y1 = null,
    double? z0 = null,
    double? z1 = null,
    bool keepZ = false)
{
    foreach (var listName in new[] { "compartments", "airlocks" })
    {
        if (root[listName] is not JsonArray arr)
            continue;
        foreach (var node in arr)
        {
            if (node is not JsonObject c)
                continue;
            if (!string.Equals(c["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
                continue;

            var ny0 = y0 ?? c["y0"]!.GetValue<double>();
            var ny1 = y1 ?? c["y1"]!.GetValue<double>();
            var nz0 = keepZ ? c["z0"]!.GetValue<double>() : (z0 ?? c["z0"]!.GetValue<double>());
            var nz1 = keepZ ? c["z1"]!.GetValue<double>() : (z1 ?? c["z1"]!.GetValue<double>());
            c["y0"] = ny0;
            c["y1"] = ny1;
            c["z0"] = nz0;
            c["z1"] = nz1;
            if (id == "CROSSING")
                c["note"] = "athwartships clear F–A = CORR_W; y clipped to CORR_OUTER; airlocks outboard DK0 only";
            if (id == "ACCESS")
                c["note"] = "accessway clear W = CORR_W (same as CORR_P/S)";
            c["planRing"] = BuildPlanRing(ny0, ny1, nz0, nz1);
            Console.WriteLine($"  {id}: y={F(ny0)}…{F(ny1)} z={F(nz0)}…{F(nz1)}");
            return;
        }
    }

    throw new InvalidOperationException($"compartment {id} not found");
}

static JsonNode BuildPlanRing(double y0, double y1, double z0, double z1, int samples = 16)
{
    var pts = new List<double[]>(samples * 2 + 2);
    for (var i = 0; i <= samples; i++)
        pts.Add([y1, z0 + (z1 - z0) * i / samples]);
    for (var i = samples; i >= 0; i--)
        pts.Add([y0, z0 + (z1 - z0) * i / samples]);
    return JsonNode.Parse(JsonSerializer.Serialize(pts))
           ?? throw new InvalidOperationException("planRing serialize failed");
}

static void GrowLoft(JsonNode? loftNode, double oldBeam, double newBeam, double oldOah, double newOah)
{
    if (loftNode?["stations"] is not JsonArray sts)
        return;
    var sx = newBeam / oldBeam;
    var sy = newOah / oldOah;
    foreach (var st in sts.OfType<JsonObject>())
    {
        st["halfBeam"] = st["halfBeam"]!.GetValue<double>() * sx;
        st["halfHeight"] = st["halfHeight"]!.GetValue<double>() * sy;
        if (st["verts"] is not JsonArray verts)
            continue;
        foreach (var v in verts.OfType<JsonObject>())
        {
            v["y"] = v["y"]!.GetValue<double>() * sx;
            v["up"] = v["up"]!.GetValue<double>() * sy;
        }
    }
}

static void GrowHullRings(JsonNode? rings, double oldBeam, double newBeam, double oldOah, double newOah)
{
    if (rings is not JsonObject ro)
        return;
    var sx = newBeam / oldBeam;
    var sy = newOah / oldOah;
    foreach (var prop in ro)
    {
        if (prop.Value is not JsonArray arr)
            continue;
        foreach (var st in arr.OfType<JsonObject>())
        {
            if (st["halfBeam"] is not null)
                st["halfBeam"] = st["halfBeam"]!.GetValue<double>() * sx;
            if (st["halfHeight"] is not null)
                st["halfHeight"] = st["halfHeight"]!.GetValue<double>() * sy;
        }
    }
}

static void GrowHullPoints(JsonNode? points, double oldBeam, double newBeam, double oldOah, double newOah)
{
    if (points is not JsonObject po)
        return;
    var sx = newBeam / oldBeam;
    var sy = newOah / oldOah;
    // Manufacturer: X forward, Y starboard, Z up.
    foreach (var prop in po)
    {
        if (prop.Value is not JsonObject pt)
            continue;
        if (pt["y"] is not null)
            pt["y"] = pt["y"]!.GetValue<double>() * sx;
        if (pt["z"] is not null)
            pt["z"] = pt["z"]!.GetValue<double>() * sy;
    }
}

static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
