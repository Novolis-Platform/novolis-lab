// Fail-closed: Calypso detail/GA blueprints must draft from lock loft, not invent scales/geometry.
//   dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\scripts\verify-calypso-blueprints.cs
// Optional: skip re-emit with --skip-emit (assumes --blueprints already ran).
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

var skipEmit = args.Any(a => string.Equals(a, "--skip-emit", StringComparison.OrdinalIgnoreCase));
var lockPath = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json";
var detailsDir = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\details";
var project = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj";

const double ExpectM1 = 0.5;
const double ExpectD0 = 4.4;
const double ExpectD1 = 8.0;
const double ExpectTPlate = 0.1;
const double ExpectRoomH = 3.2;
const double LoaExpect = 69.0;
const double BeamExpect = 20.8;
const double PlanPadL = 22, PlanPadR = 36, PlanPadTop = 16, PlanPadBot = 28, PlanFit = 0.92;
const double PlanW = 380, PlanH = 200;

var failures = 0;
void Fail(string msg)
{
    failures++;
    Console.WriteLine($"  FAIL {msg}");
}

void Ok(string msg) => Console.WriteLine($"  OK  {msg}");

if (!File.Exists(lockPath))
{
    Console.Error.WriteLine($"Missing lock {lockPath}");
    Environment.Exit(1);
}

var hashBefore = Sha256Hex(lockPath);
if (!skipEmit)
{
    Console.WriteLine("Re-emitting --blueprints to prove lock is byte-identical…");
    var psi = new ProcessStartInfo("dotnet")
    {
        Arguments = $"run --project \"{project}\" -p:NovolisUseProjectReferences=true -- --blueprints",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start CalypsoCad");
    var stdout = proc.StandardOutput.ReadToEnd();
    var stderr = proc.StandardError.ReadToEnd();
    proc.WaitForExit();
    if (proc.ExitCode != 0)
    {
        Console.Error.WriteLine(stderr);
        Console.Error.WriteLine(stdout);
        Environment.Exit(proc.ExitCode);
    }
}

var hashAfter = Sha256Hex(lockPath);
if (!string.Equals(hashBefore, hashAfter, StringComparison.OrdinalIgnoreCase))
    Fail($"lock mutated by --blueprints ({hashBefore[..12]}… → {hashAfter[..12]}…)");
else
    Ok($"lock hash unchanged ({hashBefore[..12]}…)");

using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
var root = doc.RootElement;
var env = root.GetProperty("envelope");
var decks = root.GetProperty("decks");
var loa = env.GetProperty("LOA").GetDouble();
var beam = env.GetProperty("BEAM").GetDouble();
var oah = env.GetProperty("OAH").GetDouble();
var m1 = decks.GetProperty("m1").GetDouble();
var d0 = decks.GetProperty("d0").GetDouble();
var d1 = decks.GetProperty("d1").GetDouble();
var tPlate = decks.TryGetProperty("tPlate", out var tp) ? tp.GetDouble() : 0.1;
var roomH = decks.TryGetProperty("roomH", out var rh) ? rh.GetDouble() : 3.2;

if (Math.Abs(loa - LoaExpect) > 1e-6 || Math.Abs(beam - BeamExpect) > 1e-6)
    Fail($"envelope LOA/BEAM {loa}/{beam} ≠ {LoaExpect}/{BeamExpect}");
else
    Ok($"envelope LOA={loa} BEAM={beam} OAH={oah}");

if (Math.Abs(m1 - ExpectM1) > 1e-6 || Math.Abs(d0 - ExpectD0) > 1e-6 || Math.Abs(d1 - ExpectD1) > 1e-6)
    Fail($"decks m1/d0/d1 = {m1}/{d0}/{d1} ≠ {ExpectM1}/{ExpectD0}/{ExpectD1}");
else
    Ok($"decks plate tops m1={m1} d0={d0} d1={d1}");

if (Math.Abs(tPlate - ExpectTPlate) > 1e-6 || Math.Abs(roomH - ExpectRoomH) > 1e-6)
    Fail($"tPlate/roomH = {tPlate}/{roomH} ≠ {ExpectTPlate}/{ExpectRoomH}");
else
    Ok($"tPlate={tPlate} roomH={roomH}");

foreach (var c in root.GetProperty("compartments").EnumerateArray())
{
    var id = c.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "?" : "?";
    var deck = c.GetProperty("deck");
    if (deck.ValueKind != JsonValueKind.Number)
        continue;
    var deckNum = deck.GetInt32();
    var expectTop = deckNum switch { -1 => m1, 1 => d1, _ => d0 };
    var up0 = c.GetProperty("up0").GetDouble();
    if (Math.Abs(up0 - expectTop) > 0.001)
        Fail($"{id} up0={up0} ≠ deck plate top {expectTop} (±1 mm)");
}

Ok("compartment up0 ↔ deck plate top checked");

var cadPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Novolis", "CalypsoCad", "generated", "calypso.cadjson");
if (!File.Exists(cadPath))
{
    Console.WriteLine("Regenerating calypso.cadjson for sandwich/HOLD gates…");
    var genPsi = new ProcessStartInfo("dotnet")
    {
        Arguments = $"run --project \"{project}\" -p:NovolisUseProjectReferences=true -- --generate-only --json-only",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    using (var gen = Process.Start(genPsi) ?? throw new InvalidOperationException("Failed to start generate"))
    {
        gen.WaitForExit();
        if (gen.ExitCode != 0)
        {
            Console.Error.WriteLine(gen.StandardError.ReadToEnd());
            Fail("generate-only failed before cadjson gates");
        }
    }
}

if (!File.Exists(cadPath))
    Fail($"missing calypso.cadjson at {cadPath}");
else
{
    using var cadDoc = JsonDocument.Parse(File.ReadAllText(cadPath));
    var ents = cadDoc.RootElement.TryGetProperty("entities", out var eArr) ? eArr : default;
    var sandwich = 0;
    var holdWebs = 0;
    var tops = new Dictionary<int, List<double>>();
    if (ents.ValueKind == JsonValueKind.Array)
    {
        foreach (var ent in ents.EnumerateArray())
        {
            var name = ent.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (name.StartsWith("deck-sandwich-dk", StringComparison.OrdinalIgnoreCase)
                && TryEntityBox(ent, out var cx, out var cy, out var cz, out _, out var hy, out _))
            {
                sandwich++;
                var deckNum = name.Contains("dkm1", StringComparison.OrdinalIgnoreCase)
                              || name.Contains("dk-1", StringComparison.OrdinalIgnoreCase)
                    ? -1
                    : name.Contains("dk1-", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                var plateTop = cy + Math.Abs(hy);
                if (!tops.TryGetValue(deckNum, out var list))
                    tops[deckNum] = list = [];
                list.Add(plateTop);
                _ = (cx, cz);
            }

            if (name.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase)
                && name.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase)
                && TryEntityBox(ent, out _, out _, out var fz, out _, out _, out _))
            {
                var zStem = loa * 0.5 - fz;
                if (zStem > 49.3 && zStem < 67.5)
                    holdWebs++;
            }
        }
    }

    if (sandwich == 0)
        Fail("cadjson has zero deck-sandwich-* entities");
    else
        Ok($"cadjson sandwich panels={sandwich}");

    if (holdWebs != 0)
        Fail($"cadjson has {holdWebs} frame web solids in open HOLD (expect 0)");
    else
        Ok("cadjson open HOLD has zero transverse web solids");

    foreach (var (deckNum, expect) in new[] { (-1, m1), (0, d0), (1, d1) })
    {
        if (!tops.TryGetValue(deckNum, out var list) || list.Count == 0)
        {
            Fail($"no sandwich plate tops for deck {deckNum}");
            continue;
        }

        var avg = list.Average();
        if (Math.Abs(avg - expect) > 0.001)
            Fail($"deck {deckNum} sandwich plate top avg={avg:0.####} ≠ lock {expect} (±1 mm)");
        else
            Ok($"deck {deckNum} sandwich plate top ≈ {expect}");
    }
}

var loft = ParseLoft(root);
if (loft.Count == 0)
    Fail("hullLoft.stations missing");
else
{
    var loftFails = 0;
    foreach (var st in loft)
    {
        if (st.Verts.Count < 3)
        {
            loftFails++;
            Fail($"loft {st.Id ?? st.Z.ToString(CultureInfo.InvariantCulture)} has <3 verts");
            continue;
        }

        var maxAbsY = st.Verts.Max(v => Math.Abs(v.Y));
        if (Math.Abs(maxAbsY - st.HalfBeam) > 0.05)
        {
            loftFails++;
            Fail($"loft z={st.Z:0.##} verts max|y|={maxAbsY:0.###} vs halfBeam={st.HalfBeam:0.###}");
        }
    }

    if (loftFails == 0)
        Ok($"loft verts ↔ halfBeam @ {loft.Count} stations (±0.05 m)");
}

if (!Directory.Exists(detailsDir))
{
    Fail($"missing details dir {detailsDir}");
}
else
{
    var sheets = Directory.EnumerateFiles(detailsDir, "*.html")
        .Where(p => !string.Equals(Path.GetFileName(p), "index.html", StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
        .ToList();
    if (sheets.Count == 0)
        Fail("no detail sheets under docs/internals/details");

    var planScaleRx = new Regex(
        @"(?:PLAN|PROFILE|DECK-BY-DECK)[^<]{0,120}?(\d+(?:\.\d+)?)\s*px/m",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    var transverseScaleRx = new Regex(
        @"halfB=[^<]*?(\d+(?:\.\d+)?)\s*px/m",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    var loaDimRx = new Regex(@"LOA\s+69", RegexOptions.CultureInvariant);

    foreach (var sheet in sheets)
    {
        var name = Path.GetFileName(sheet);
        var html = File.ReadAllText(sheet);
        var planFamily = planScaleRx.Matches(html)
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToList();
        if (planFamily.Count < 2)
        {
            Fail($"{name}: need ≥2 PLAN/PROFILE/DECK px/m notes, found {planFamily.Count}");
            continue;
        }

        if (planFamily.Max() - planFamily.Min() > 0.5 / loa)
            Fail($"{name}: plan-family px/m diverge {planFamily.Min():0.###}…{planFamily.Max():0.###}");

        // SPACE/BH sheets use 380×200 plan; FRAME uses 360×200 — both must be isotropic LOA×BEAM.
        var sheetPlanW = name.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase) ? 360.0 : PlanW;
        var sheetExpect = Math.Min((sheetPlanW - PlanPadL - PlanPadR) / loa, (PlanH - PlanPadTop - PlanPadBot) / beam) * PlanFit;
        if (Math.Abs(planFamily[0] - sheetExpect) > 0.02)
            Fail($"{name}: printed plan scale {planFamily[0]:0.###} ≠ SharedPlanScale {sheetExpect:0.###}");

        if (!transverseScaleRx.IsMatch(html))
            Fail($"{name}: TRANSVERSE missing printed px/m (halfB=… · N px/m)");

        var sheetTruePlatePx = tPlate * planFamily[0];
        if (sheetTruePlatePx < 1.0 && !html.Contains("(not to scale)", StringComparison.Ordinal))
            Fail($"{name}: plate band exaggerated at {sheetTruePlatePx:0.###} px — require '(not to scale)'");

        if (!loaDimRx.IsMatch(html))
            Fail($"{name}: missing LOA 69 dimension text");

        // Up-overflow: at each BH/space mid, no compartment up-range may stick outside loft after clip.
        var focusZ = GuessFocusZ(name, root, loft);
        if (focusZ is double zFocus)
        {
            SampleLoftUpRange(loft, zFocus, oah, out var upMin, out var upMax);
            foreach (var c in root.GetProperty("compartments").EnumerateArray())
            {
                var z0 = c.GetProperty("z0").GetDouble();
                var z1 = c.GetProperty("z1").GetDouble();
                if (Math.Min(z0, z1) - 0.02 > zFocus || Math.Max(z0, z1) + 0.02 < zFocus)
                    continue;
                var up0 = c.GetProperty("up0").GetDouble();
                var up1 = c.GetProperty("up1").GetDouble();
                var upLo = Math.Max(Math.Min(up0, up1), upMin);
                var upHi = Math.Min(Math.Max(up0, up1), upMax);
                // After clip, remaining band must lie inside loft (gate = clip math used by emitter).
                if (upHi - upLo >= 0.15 && (upLo < upMin - 1e-6 || upHi > upMax + 1e-6))
                {
                    var id = c.TryGetProperty("id", out var idEl) ? idEl.GetString() : "?";
                    Fail($"{name}: {id} up [{upLo:0.##},{upHi:0.##}] outside loft [{upMin:0.##},{upMax:0.##}] @ z={zFocus:0.##}");
                }
            }
        }
    }

    Ok($"detail sheets scanned: {sheets.Count}");
}

Console.WriteLine(failures == 0
    ? "verify-calypso-blueprints: PASS"
    : $"verify-calypso-blueprints: FAIL ({failures})");
Environment.Exit(failures == 0 ? 0 : 1);

static bool TryEntityBox(
    JsonElement ent,
    out double cx, out double cy, out double cz,
    out double hx, out double hy, out double hz)
{
    cx = cy = cz = hx = hy = hz = 0;
    if (ent.TryGetProperty("center", out var c)
        && ent.TryGetProperty("halfExtents", out var he)
        && c.GetArrayLength() >= 3
        && he.GetArrayLength() >= 3)
    {
        cx = c[0].GetDouble();
        cy = c[1].GetDouble();
        cz = c[2].GetDouble();
        hx = he[0].GetDouble();
        hy = he[1].GetDouble();
        hz = he[2].GetDouble();
        return true;
    }

    if (ent.TryGetProperty("points", out var pts) && pts.GetArrayLength() >= 2)
    {
        var p0 = pts[0];
        var p1 = pts[1];
        if (p0.GetArrayLength() >= 3 && p1.GetArrayLength() >= 3)
        {
            cx = p0[0].GetDouble();
            cy = p0[1].GetDouble();
            cz = p0[2].GetDouble();
            hx = p1[0].GetDouble();
            hy = p1[1].GetDouble();
            hz = p1[2].GetDouble();
            return true;
        }
    }

    return false;
}

static string Sha256Hex(string path)
{
    using var fs = File.OpenRead(path);
    var hash = SHA256.HashData(fs);
    return Convert.ToHexString(hash);
}

static double? GuessFocusZ(string fileName, JsonElement root, List<LoftStation> loft)
{
    var baseName = Path.GetFileNameWithoutExtension(fileName);
    if (baseName.StartsWith("SPACE-", StringComparison.OrdinalIgnoreCase))
    {
        var id = baseName["SPACE-".Length..];
        foreach (var c in root.GetProperty("compartments").EnumerateArray())
        {
            if (!string.Equals(c.GetProperty("id").GetString(), id, StringComparison.OrdinalIgnoreCase))
                continue;
            return (c.GetProperty("z0").GetDouble() + c.GetProperty("z1").GetDouble()) * 0.5;
        }
    }

    if (baseName.StartsWith("FRAME-z", StringComparison.OrdinalIgnoreCase)
        && double.TryParse(baseName["FRAME-z".Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var zFrame))
        return zFrame;

    if (baseName.StartsWith("BH-", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("stations", out var st))
    {
        // Prefer schedule midpoints from stations keys used by CalypsoBulkheads.
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in st.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Number)
                map[p.Name] = p.Value.GetDouble();
        }

        var id = baseName["BH-".Length..];
        return id.ToUpperInvariant() switch
        {
            "BOW-COLLISION" => map.GetValueOrDefault("Z_BRIDGE_AFT", 13),
            "AIRLOCK-CROSS" => (map.GetValueOrDefault("Z_CROSS_FORE", 19) + map.GetValueOrDefault("Z_CROSS_AFT", 23)) * 0.5,
            "ENG-FORE" => map.GetValueOrDefault("Z_ENG_FORE", 41),
            "ENG-AFT" => map.GetValueOrDefault("Z_ENG_AFT", 49.25),
            _ => loft.Count > 0 ? loft[loft.Count / 2].Z : null,
        };
    }

    return null;
}

static List<LoftStation> ParseLoft(JsonElement root)
{
    var loft = new List<LoftStation>();
    if (!root.TryGetProperty("hullLoft", out var hl)
        || !hl.TryGetProperty("stations", out var sts)
        || sts.ValueKind != JsonValueKind.Array)
        return loft;

    foreach (var st in sts.EnumerateArray())
    {
        var verts = new List<(double Y, double Up)>();
        if (st.TryGetProperty("verts", out var vs) && vs.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vs.EnumerateArray())
                verts.Add((v.GetProperty("y").GetDouble(), v.GetProperty("up").GetDouble()));
        }

        loft.Add(new LoftStation(
            st.TryGetProperty("id", out var id) ? id.GetString() : null,
            st.GetProperty("zFromStem").GetDouble(),
            st.GetProperty("halfBeam").GetDouble(),
            verts));
    }

    loft.Sort((a, b) => a.Z.CompareTo(b.Z));
    return loft;
}

static void SampleLoftUpRange(
    IReadOnlyList<LoftStation> loft, double z, double fallbackOah,
    out double upMin, out double upMax)
{
    if (loft.Count == 0)
    {
        upMin = 0;
        upMax = fallbackOah;
        return;
    }

    LoftStation a, b;
    double t;
    if (z <= loft[0].Z)
    {
        a = b = loft[0];
        t = 0;
    }
    else if (z >= loft[^1].Z)
    {
        a = b = loft[^1];
        t = 0;
    }
    else
    {
        a = loft[0];
        b = loft[^1];
        t = 0;
        for (var i = 0; i < loft.Count - 1; i++)
        {
            if (z < loft[i].Z || z > loft[i + 1].Z)
                continue;
            a = loft[i];
            b = loft[i + 1];
            t = (z - a.Z) / Math.Max(1e-9, b.Z - a.Z);
            break;
        }
    }

    var aMin = a.Verts.Count == 0 ? 0 : a.Verts.Min(v => v.Up);
    var aMax = a.Verts.Count == 0 ? fallbackOah : a.Verts.Max(v => v.Up);
    var bMin = b.Verts.Count == 0 ? 0 : b.Verts.Min(v => v.Up);
    var bMax = b.Verts.Count == 0 ? fallbackOah : b.Verts.Max(v => v.Up);
    upMin = aMin + t * (bMin - aMin);
    upMax = aMax + t * (bMax - aMax);
}

sealed record LoftStation(string? Id, double Z, double HalfBeam, List<(double Y, double Up)> Verts);
