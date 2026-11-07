#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property OutputType=Exe
#:property ImplicitUsings=enable
#:property Nullable=enable

// Realign Calypso lock decks to 100 mm sandwich plates + DK0 centered on OAH/2.
// Authority: CAL-INT-GA-001.json — m1/d0/d1 are finished-floor = TOP of plate.

using System.Text.Json;
using System.Text.Json.Nodes;

var lockPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "docs", "internals", "CAL-INT-GA-001.json"));
if (!File.Exists(lockPath))
{
    // File-based app bin is under tools/; prefer explicit sibling path.
    lockPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json";
}

var root = JsonNode.Parse(File.ReadAllText(lockPath))
    ?? throw new InvalidOperationException("lock parse failed");

var env = root["envelope"]!;
var oah = env["OAH"]!.GetValue<double>();
var decks = root["decks"]!.AsObject();
var roomH = decks["roomH"]!.GetValue<double>();
const double tPlate = 0.1; // 100 mm sandwich (≈10 mm steel faces + hollow core)

// Finished floors = plate tops. DK0 room clear centered on model mid-height.
var m1 = 0.5;                      // TOP of DK−1 plate (= HOLD floor)
var d0 = Math.Round(oah * 0.5 - roomH * 0.5, 3);  // 6 − 1.6 = 4.4
var d1 = decks["d1"]!.GetValue<double>(); // keep +1 plate top at 8.0
var d0Under = Math.Round(d0 - tPlate, 3);         // underside of DK0 plate
var d0Ceil = Math.Round(d0 + roomH, 3);

decks["m1"] = m1;
decks["d0"] = d0;
decks["d1"] = d1;
decks["tPlate"] = tPlate;
decks["note"] =
    "m1/d0/d1 are finished-floor elevations = TOP of sandwich deckplates. " +
    $"tPlate={tPlate:0.###} m (100 mm: ~10 mm steel faces over hollow core). " +
    "DK0 compartment clear is centered on OAH/2 with the DK0 plate beneath the floor. " +
    "HOLD volume bottom = top of DK−1 plate.";

static void SetUp(JsonObject o, double up0, double up1)
{
    o["up0"] = up0;
    o["up1"] = up1;
}

foreach (var node in root["compartments"]!.AsArray())
{
    var c = node!.AsObject();
    var id = c["id"]!.GetValue<string>();
    var up0 = c["up0"]!.GetValue<double>();
    var up1 = c["up1"]!.GetValue<double>();

    if (id == "HOLD")
    {
        // Cargo void sits on DK−1 plate top.
        SetUp(c, m1, up1);
        continue;
    }

    // Already-patched or legacy DK0 band → centered clear on plate top.
    if ((Math.Abs(up0 - 4.0) < 1e-6 && Math.Abs(up1 - 7.2) < 1e-6)
        || (Math.Abs(up0 - d0) < 1e-3 && Math.Abs(up1 - (d0 + roomH)) < 1e-2))
    {
        SetUp(c, d0, d0Ceil);
        continue;
    }

    if ((Math.Abs(up0 - 0.5) < 1e-6 && Math.Abs(up1 - 3.7) < 1e-6)
        || (Math.Abs(up0 - m1) < 1e-6 && Math.Abs(up1 - d0Under) < 1e-2))
    {
        // DK−1 rooms under DK0 plate.
        SetUp(c, m1, d0Under);
        continue;
    }

    if (Math.Abs(up0 - 1.0) < 1e-6 || (id == "HOLD" && Math.Abs(up0 - m1) > 1e-6))
        c["up0"] = m1;
}

if (root["airlocks"] is JsonArray airlocks)
{
    foreach (var node in airlocks)
    {
        var a = node!.AsObject();
        var up0 = a["up0"]!.GetValue<double>();
        var up1 = a["up1"]!.GetValue<double>();
        if ((Math.Abs(up0 - 4.0) < 1e-6 && Math.Abs(up1 - 7.2) < 1e-6)
            || (Math.Abs(up0 - d0) < 1e-3 && Math.Abs(up1 - (d0 + roomH)) < 1e-2))
            SetUp(a, d0, d0Ceil);
    }
}

// Hatch centers that sat in the old DK0 / DK+1 mid-bands shift with the floor.
if (root["hatches"] is JsonArray hatches)
{
    foreach (var node in hatches)
    {
        var h = node!.AsObject();
        if (h["up"] is null)
            continue;
        var up = h["up"]!.GetValue<double>();
        // Old DK0 mid ~5.05 / 5.1 → new mid at OAH/2 = 6
        if (Math.Abs(up - 5.05) < 0.02 || Math.Abs(up - 5.1) < 0.02)
            h["up"] = oah * 0.5;
        // Old DK0 band shifted by +0.4 with the floor
        else if (up >= 4.0 - 0.05 && up <= 7.2 + 0.05 && Math.Abs(up - 1.6) > 0.05)
        {
            // only nudge classic floor-relative values near old mid
            if (up is >= 4.8 and <= 5.4)
                h["up"] = oah * 0.5;
        }
    }
}

var opts = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(lockPath, root.ToJsonString(opts) + Environment.NewLine);
Console.WriteLine($"Patched deck plating → {lockPath}");
Console.WriteLine($"  m1={m1} (DK−1 plate top / HOLD floor)");
Console.WriteLine($"  d0={d0} (DK0 plate top; room {d0}..{d0Ceil}, center {oah * 0.5})");
Console.WriteLine($"  d1={d1}  tPlate={tPlate}  d0 underside={d0Under}");
