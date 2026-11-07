#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property OutputType=Exe
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property PublishAot=false
#:property PublishTrimmed=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

// Contiguous ACCESS↔CROSSING↔CORR_P/S (abut BH faces) + port/stbd airlocks
// outboard of CROSS, parallel to CORR, three hatches to vacuum further stern.
//   dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\tools\Patch-PassageContiguousAirlocks.cs

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

var lockPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json";

const double CorrW = 3.0;
const double CorrInner = 5.0;
const double CorrOuter = 8.0;
const double AccessHalf = CorrW * 0.5; // ±1.5
const double CrossZ0 = 19.0; // Z_CROSS_FORE — abut ACCESS
const double CrossZ1 = 23.0; // Z_CROSS_AFT / Z_CREW_FORE — abut CORR
const double CorrZ1 = 49.25;
const double AlClear = 1.6; // athwart clear of each airlock tunnel
const double AlOuter = CorrOuter + AlClear; // 9.6
const double AlZ0 = CrossZ0;
const double AlZMid = CrossZ1;
const double AlZ1 = 26.5; // further stern → vacuum hatch
const double Dk0 = 4.4;
const double RoomH = 3.2;
const double HatchUp = Dk0 + 1.6;

var root = JsonNode.Parse(File.ReadAllText(lockPath))
    ?? throw new InvalidOperationException("lock parse failed");

var circ = root["circulation"]!.AsObject();
circ["CORR_W"] = CorrW;
circ["CORR_INNER"] = CorrInner;
circ["CORR_OUTER"] = CorrOuter;
circ["ACCESS_W"] = CorrW;
circ["L_CROSS"] = CrossZ1 - CrossZ0;
circ["note"] =
    "ACCESS + CROSSING + CORR_P/S = one contiguous passageway; BH faces get hatches/cutouts. " +
    "Airlocks DK0 outboard of CROSS, parallel CORR, three hatches to SPACE further stern.";

PatchComp(root, "ACCESS", -AccessHalf, AccessHalf, 13, CrossZ0,
    "fore CL access — contiguous with CROSSING at z=Z_CROSS_FORE; clear W = CORR_W");
PatchComp(root, "CROSSING", -CorrOuter, CorrOuter, CrossZ0, CrossZ1,
    "athwart CROSS band — contiguous with ACCESS fore and CORR_P/S aft; one passageway");
PatchComp(root, "CORR_P", -CorrOuter, -CorrInner, CrossZ1, CorrZ1,
    "port spine corridor — contiguous with CROSSING; clear 3 m; same track −1/+1");
PatchComp(root, "CORR_S", CorrInner, CorrOuter, CrossZ1, CorrZ1,
    "starboard spine corridor — contiguous with CROSSING; clear 3 m; same track −1/+1");

PatchAirlock(root, "AIRLOCK_A_port", -AlOuter, -CorrOuter, AlZ0, AlZMid,
    "DK0 port vestibule outboard of CROSS, parallel CORR track");
PatchAirlock(root, "AIRLOCK_B_port", -AlOuter, -CorrOuter, AlZMid, AlZ1,
    "DK0 port outer chamber — opens further stern to SPACE");
PatchAirlock(root, "AIRLOCK_A_stbd", CorrOuter, AlOuter, AlZ0, AlZMid,
    "DK0 stbd vestibule outboard of CROSS, parallel CORR track");
PatchAirlock(root, "AIRLOCK_B_stbd", CorrOuter, AlOuter, AlZMid, AlZ1,
    "DK0 stbd outer chamber — opens further stern to SPACE");

UpsertHatch(root, "D1-P", deck: 0, clearW: 1, clearH: 2.1,
    y: -CorrOuter, z: (AlZ0 + AlZMid) * 0.5, up: HatchUp, normal: "-Y",
    from: "CROSSING", to: "AIRLOCK_A_port", faces: "into airlock from CROSS");
UpsertHatch(root, "D2-P", deck: 0, clearW: 1, clearH: 2.1,
    y: -(CorrOuter + AlClear * 0.5), z: AlZMid, up: HatchUp, normal: "+Zaft",
    from: "AIRLOCK_A_port", to: "AIRLOCK_B_port", faces: "intermediate airlock");
UpsertHatch(root, "D3-P", deck: 0, clearW: 1, clearH: 2.1,
    y: -(CorrOuter + AlClear * 0.5), z: AlZ1, up: HatchUp, normal: "+Zaft",
    from: "AIRLOCK_B_port", to: "SPACE", faces: "outer vacuum stern");

UpsertHatch(root, "D1-S", deck: 0, clearW: 1, clearH: 2.1,
    y: CorrOuter, z: (AlZ0 + AlZMid) * 0.5, up: HatchUp, normal: "+Y",
    from: "CROSSING", to: "AIRLOCK_A_stbd", faces: "into airlock from CROSS");
UpsertHatch(root, "D2-S", deck: 0, clearW: 1, clearH: 2.1,
    y: CorrOuter + AlClear * 0.5, z: AlZMid, up: HatchUp, normal: "+Zaft",
    from: "AIRLOCK_A_stbd", to: "AIRLOCK_B_stbd", faces: "intermediate airlock");
UpsertHatch(root, "D3-S", deck: 0, clearW: 1, clearH: 2.1,
    y: CorrOuter + AlClear * 0.5, z: AlZ1, up: HatchUp, normal: "+Zaft",
    from: "AIRLOCK_B_stbd", to: "SPACE", faces: "outer vacuum stern");

// Contiguous passageway — open hatches at BH faces (all decks).
foreach (var deck in new[] { -1, 0, 1 })
{
    var up = deck switch { -1 => 0.5 + 1.6, 1 => 8.0 + 1.6, _ => HatchUp };
    // Portal leaf ~2 m (shirt-sleeves); corridor clear remains CORR_W = 3 m.
    const double PassClearW = 2.0;
    UpsertHatch(root, $"PASS-ACCESS-CROSS-D{DeckTag(deck)}", deck, PassClearW, 2.15,
        y: 0, z: CrossZ0, up: up, normal: "+Zaft",
        from: "ACCESS", to: "CROSSING", faces: "passage BH cutout");
    UpsertHatch(root, $"PASS-CROSS-CORR_P-D{DeckTag(deck)}", deck, PassClearW, 2.15,
        y: -(CorrInner + CorrW * 0.5), z: CrossZ1, up: up, normal: "+Zaft",
        from: "CROSSING", to: "CORR_P", faces: "passage BH cutout");
    UpsertHatch(root, $"PASS-CROSS-CORR_S-D{DeckTag(deck)}", deck, PassClearW, 2.15,
        y: CorrInner + CorrW * 0.5, z: CrossZ1, up: up, normal: "+Zaft",
        from: "CROSSING", to: "CORR_S", faces: "passage BH cutout");
}

if (root["corridorCenterlines"] is JsonArray cls)
{
    foreach (var cl in cls)
    {
        var id = cl?["id"]?.GetValue<string>();
        if (id is "CORR_P_CL")
        {
            cl!["y"] = -(CorrInner + CorrW * 0.5);
            cl["z0"] = CrossZ1;
            cl["z1"] = CorrZ1;
        }
        else if (id is "CORR_S_CL")
        {
            cl!["y"] = CorrInner + CorrW * 0.5;
            cl["z0"] = CrossZ1;
            cl["z1"] = CorrZ1;
        }
        else if (id is "CROSS_CL")
        {
            cl!["y0"] = -CorrOuter;
            cl["y1"] = CorrOuter;
            cl["z"] = (CrossZ0 + CrossZ1) * 0.5;
        }
    }
}

if (root["exterior"]?["airlockBlisters"] is JsonArray blisters)
{
    foreach (var b in blisters)
    {
        var id = b?["id"]?.GetValue<string>() ?? "";
        if (id.Contains("port", StringComparison.OrdinalIgnoreCase))
        {
            b!["y"] = -(CorrOuter + AlClear * 0.5);
            b["zFromStem"] = AlZ1;
            b["halfY"] = AlClear * 0.5;
            b["halfZ"] = 1.1;
            b["halfUp"] = 1.05;
            b["up"] = Dk0 + RoomH * 0.5;
        }
        else if (id.Contains("stbd", StringComparison.OrdinalIgnoreCase))
        {
            b!["y"] = CorrOuter + AlClear * 0.5;
            b["zFromStem"] = AlZ1;
            b["halfY"] = AlClear * 0.5;
            b["halfZ"] = 1.1;
            b["halfUp"] = 1.05;
            b["up"] = Dk0 + RoomH * 0.5;
        }
    }
}

var stations = root["stations"]!.AsObject();
stations["Z_CROSS_FORE"] = CrossZ0;
stations["Z_CROSS_AFT"] = CrossZ1;
stations["Z_CREW_FORE"] = CrossZ1;

root["rev"] = "H";
root["drawing"] = "CAL-INT-GA-001";
root["note"] =
    "Rev H: contiguous ACCESS/CROSS/CORR passageway; airlocks outboard CROSS parallel CORR, D1/D2/D3 to stern vacuum.";

var opts = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(lockPath, root.ToJsonString(opts) + "\n");
Console.WriteLine($"Patched → {lockPath}");
Console.WriteLine($"  PASSAGE: ACCESS …{CrossZ0} | CROSS {CrossZ0}…{CrossZ1} | CORR {CrossZ1}…{CorrZ1}");
Console.WriteLine($"  AIRLOCK: y ±{CorrOuter}…±{AlOuter}, z {AlZ0}…{AlZMid}…{AlZ1} (D3 stern vacuum)");

static string DeckTag(int d) => d switch { -1 => "M1", 1 => "P1", _ => "0" };

static void PatchComp(JsonNode root, string id, double y0, double y1, double z0, double z1, string note)
{
    foreach (var node in root["compartments"]!.AsArray())
    {
        if (node is not JsonObject c)
            continue;
        if (!string.Equals(c["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
            continue;
        c["y0"] = y0;
        c["y1"] = y1;
        c["z0"] = z0;
        c["z1"] = z1;
        c["note"] = note;
        c["planRing"] = BuildPlanRing(y0, y1, z0, z1);
        Console.WriteLine($"  {id}: y={F(y0)}…{F(y1)} z={F(z0)}…{F(z1)}");
        return;
    }

    throw new InvalidOperationException($"compartment {id} not found");
}

static void PatchAirlock(JsonNode root, string id, double y0, double y1, double z0, double z1, string note)
{
    foreach (var node in root["airlocks"]!.AsArray())
    {
        if (node is not JsonObject c)
            continue;
        if (!string.Equals(c["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
            continue;
        c["y0"] = y0;
        c["y1"] = y1;
        c["z0"] = z0;
        c["z1"] = z1;
        c["up0"] = Dk0;
        c["up1"] = Dk0 + RoomH;
        c["note"] = note;
        c["planRing"] = BuildPlanRing(y0, y1, z0, z1);
        Console.WriteLine($"  {id}: y={F(y0)}…{F(y1)} z={F(z0)}…{F(z1)}");
        return;
    }

    throw new InvalidOperationException($"airlock {id} not found");
}

static void UpsertHatch(
    JsonNode root, string id, int deck, double clearW, double clearH,
    double y, double z, double up, string normal, string from, string to, string faces)
{
    var hatches = root["hatches"]!.AsArray();
    JsonObject? existing = null;
    foreach (var node in hatches)
    {
        if (node is JsonObject h
            && string.Equals(h["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
        {
            existing = h;
            break;
        }
    }

    if (existing is null)
    {
        existing = new JsonObject { ["id"] = id };
        hatches.Add(existing);
    }

    existing["deck"] = deck;
    existing["clearW"] = clearW;
    existing["clearH"] = clearH;
    existing["y"] = y;
    existing["z"] = z;
    existing["up"] = up;
    existing["normal"] = normal;
    existing["from"] = from;
    existing["to"] = to;
    existing["faces"] = faces;
    Console.WriteLine($"  hatch {id}: {from} → {to} @ z={F(z)} y={F(y)}");
}

static JsonArray BuildPlanRing(double y0, double y1, double z0, double z1, int samples = 16)
{
    var ring = new JsonArray();
    for (var i = 0; i <= samples; i++)
    {
        var z = z0 + (z1 - z0) * i / samples;
        ring.Add(new JsonArray { y1, Math.Round(z, 4) });
    }

    for (var i = samples; i >= 0; i--)
    {
        var z = z0 + (z1 - z0) * i / samples;
        ring.Add(new JsonArray { y0, Math.Round(z, 4) });
    }

    return ring;
}

static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
