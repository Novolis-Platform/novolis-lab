using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CalypsoCad.Generation.Blueprints;

/// <summary>
/// Type C0n survival cells abreast (port→stbd) on DK0 (CREW_n) and DK+1 (PAX_n).
/// Module pitch athwart = 2.0 m including walls → clear 1.92 m. Clear F–A = 7.2 m.
/// Leftover of the former 12 m crew band extends INFIRMARY / GALLEY (DK0) and STORE_P1 (DK+1).
/// Mid stack ±5 m = exactly 5 × 2.0 m so spine corridors keep their clear width.
/// </summary>
internal static class InternalsLockCabinPatch
{
    public const int CabinCount = 5;

    /// <summary>Athwart module pitch including walls (Type C0n “with walls”).</summary>
    public const double ModuleW = 2.0;

    /// <summary>Clear athwart inside finishes (Type C0n). ModuleW − ClearW = wall stack.</summary>
    public const double ClearW = 1.92;

    /// <summary>Clear fore–aft (Type C0n).</summary>
    public const double ClearD = 7.2;

    /// <summary>Partition thickness implied by 2.0 module / 1.92 clear.</summary>
    public const double WallT = ModuleW - ClearW; // 0.08

    /// <summary>|y| of mid-stack outer face = CORR_INNER. 5 × 2.0 = 10 m.</summary>
    public const double Stack = CabinCount * ModuleW * 0.5; // 5.0

    public const double ZCrewFore = 23.0;
    public const double ZMedAft = 41.0;
    public const double DoorPass = 1.0;
    public const double DoorHPass = 2.1;
    public const double Dk0 = 4.0;
    public const double Dk1 = 8.0;
    public const double RoomH = 3.2;

    public static double CabinClearZ0 => ZCrewFore + WallT / 2;
    public static double CabinClearZ1 => CabinClearZ0 + ClearD;
    public static double CabinAftOuter => CabinClearZ1 + WallT / 2;
    public static double MedClearZ0 => CabinAftOuter + WallT / 2;
    public static double MedClearZ1 => ZMedAft - WallT / 2;

    public static void Apply(string lockJsonPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(lockJsonPath))
                   ?? throw new InvalidOperationException("Failed to parse lock JSON");

        var comps = root["compartments"]!.AsArray();
        RemoveIds(comps, id =>
            id is "CREW"
            || id.StartsWith("CABIN_", StringComparison.Ordinal)
            || id.StartsWith("CREW_", StringComparison.Ordinal)
            || id.StartsWith("PAX_", StringComparison.Ordinal)
            || id.StartsWith("VEST_CREW_", StringComparison.Ordinal)
            || id.StartsWith("VEST_PAX_", StringComparison.Ordinal));

        var modules = BuildModules();
        var insertAt = IndexAfter(comps, "CORR_S");
        if (insertAt < 0)
            insertAt = comps.Count;

        foreach (var node in modules)
            comps.Insert(insertAt++, node);

        ExtendServiceSpaces(comps);

        var hatches = root["hatches"]!.AsArray();
        RemoveIds(hatches, id =>
            id is "CAB-P" or "CAB-S" or "CAB-P1-P" or "CAB-P1-S"
            || id.StartsWith("CAB", StringComparison.Ordinal) && id.Contains('-')
            || id.StartsWith("CREW", StringComparison.Ordinal) && id.Contains('-')
            || id.StartsWith("PAX", StringComparison.Ordinal) && id.Contains('-')
            || id.StartsWith("VEST", StringComparison.Ordinal));

        foreach (var h in BuildHatches())
            hatches.Add(h);

        RepositionServiceHatches(hatches);

        if (root["stations"] is JsonObject st)
        {
            st["CREW_CABIN_COUNT"] = CabinCount;
            st["Z_CREW_FORE"] = ZCrewFore;
            st["Z_CREW_AFT"] = Math.Round(CabinAftOuter, 4);
            st["Z_MED_FORE"] = Math.Round(CabinAftOuter, 4);
            st["Z_MED_AFT"] = ZMedAft;
            st["CABIN_MODULE_W"] = ModuleW;
            st["CABIN_CLEAR_W"] = ClearW;
            st["CABIN_CLEAR_D"] = ClearD;
            st["CABIN_WALL_T"] = Math.Round(WallT, 4);
        }

        root["rev"] = "F";
        File.WriteAllText(lockJsonPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Port→stbd clear slots; each cabin clear = ClearW × ClearD.</summary>
    private static List<(int N, double Y0, double Y1, double Z0, double Z1)> Slots()
    {
        var z0 = CabinClearZ0;
        var z1 = CabinClearZ1;
        var list = new List<(int, double, double, double, double)>(CabinCount);
        for (var i = 0; i < CabinCount; i++)
        {
            var outer0 = -Stack + i * ModuleW;
            var outer1 = outer0 + ModuleW;
            var y0 = outer0 + WallT / 2;
            var y1 = outer1 - WallT / 2;
            list.Add((i + 1, y0, y1, z0, z1));
        }

        return list;
    }

    private static List<JsonObject> BuildModules()
    {
        var list = new List<JsonObject>();
        foreach (var (n, y0, y1, z0, z1) in Slots())
        {
            list.Add(Room($"CREW_{n}", 0, z0, z1, y0, y1, Dk0, Dk0 + RoomH,
                FormattableString.Invariant(
                    $"C0n crew cell {n}/{CabinCount}: clear {ClearW}×{ClearD} m; module {ModuleW} m with walls; entrance fore")));
            list.Add(Room($"PAX_{n}", 1, z0, z1, y0, y1, Dk1, Dk1 + RoomH,
                FormattableString.Invariant(
                    $"C0n pax cell {n}/{CabinCount} — same footprint as CREW_{n}; denser bunks")));
        }

        return list;
    }

    private static void ExtendServiceSpaces(JsonArray comps)
    {
        var z0 = MedClearZ0;
        var z1 = MedClearZ1;
        var yPort0 = -Stack + WallT / 2;
        var yPort1 = -WallT / 2;
        var yStbd0 = WallT / 2;
        var yStbd1 = Stack - WallT / 2;
        var fa = (z1 - z0).ToString("0.###", CultureInfo.InvariantCulture);

        foreach (var c in comps)
        {
            var id = c?["id"]?.GetValue<string>();
            if (id is "INFIRMARY")
            {
                c!["z0"] = Math.Round(z0, 4);
                c["z1"] = Math.Round(z1, 4);
                c["y0"] = Math.Round(yPort0, 4);
                c["y1"] = Math.Round(yPort1, 4);
                c["note"] =
                    $"infirmary — fore edge advanced into former cabin leftover; clear F–A {fa} m";
                c["planRing"] = PlanRing(yPort0, yPort1, z0, z1);
            }
            else if (id is "GALLEY")
            {
                c!["z0"] = Math.Round(z0, 4);
                c["z1"] = Math.Round(z1, 4);
                c["y0"] = Math.Round(yStbd0, 4);
                c["y1"] = Math.Round(yStbd1, 4);
                c["note"] =
                    $"galley — fore edge advanced into former cabin leftover; clear F–A {fa} m";
                c["planRing"] = PlanRing(yStbd0, yStbd1, z0, z1);
            }
            else if (id is "STORE_P1")
            {
                c!["z0"] = Math.Round(z0, 4);
                c["z1"] = Math.Round(z1, 4);
                c["y0"] = Math.Round(yPort0, 4);
                c["y1"] = Math.Round(yStbd1, 4);
                c["note"] =
                    $"deck +1 stores — same F–A as infirmary/galley pack; clear F–A {fa} m";
                c["planRing"] = PlanRing(yPort0, yStbd1, z0, z1);
            }
        }
    }

    private static void RepositionServiceHatches(JsonArray hatches)
    {
        var zMid = (MedClearZ0 + MedClearZ1) * 0.5;
        foreach (var h in hatches)
        {
            var id = h?["id"]?.GetValue<string>();
            if (id is "INF-P" or "GAL-S")
                h!["z"] = Math.Round(zMid, 4);
        }
    }

    private static List<JsonObject> BuildHatches()
    {
        var list = new List<JsonObject>();
        foreach (var (n, y0, y1, z0, _) in Slots())
        {
            var yMid = (y0 + y1) * 0.5;
            list.Add(Hatch($"CREW{n}-F", 0, yMid, z0, Dk0 + DoorHPass / 2,
                "-Zfore", "CROSSING", $"CREW_{n}", "fore"));
            list.Add(Hatch($"PAX{n}-F", 1, yMid, z0, Dk1 + DoorHPass / 2,
                "-Zfore", "CROSSING", $"PAX_{n}", "fore"));
        }

        return list;
    }

    private static JsonObject Room(string id, int deck, double z0, double z1, double y0, double y1,
        double up0, double up1, string note) => new()
    {
        ["id"] = id,
        ["deck"] = deck,
        ["z0"] = Math.Round(z0, 4),
        ["z1"] = Math.Round(z1, 4),
        ["y0"] = Math.Round(y0, 4),
        ["y1"] = Math.Round(y1, 4),
        ["up0"] = up0,
        ["up1"] = up1,
        ["note"] = note,
        ["planRing"] = PlanRing(y0, y1, z0, z1),
    };

    private static JsonObject Hatch(string id, int deck, double y, double z, double up, string normal,
        string from, string to, string faces) => new()
    {
        ["id"] = id,
        ["deck"] = deck,
        ["clearW"] = DoorPass,
        ["clearH"] = DoorHPass,
        ["y"] = Math.Round(y, 4),
        ["z"] = Math.Round(z, 4),
        ["up"] = up,
        ["normal"] = normal,
        ["from"] = from,
        ["to"] = to,
        ["faces"] = faces,
    };

    private static JsonArray PlanRing(double y0, double y1, double z0, double z1, int samples = 8)
    {
        var ry0 = Math.Round(y0, 4);
        var ry1 = Math.Round(y1, 4);
        var ring = new JsonArray();
        for (var i = 0; i <= samples; i++)
        {
            var z = z0 + (z1 - z0) * i / samples;
            ring.Add(new JsonArray { ry1, Math.Round(z, 4) });
        }

        for (var i = samples; i >= 0; i--)
        {
            var z = z0 + (z1 - z0) * i / samples;
            ring.Add(new JsonArray { ry0, Math.Round(z, 4) });
        }

        return ring;
    }

    private static void RemoveIds(JsonArray arr, Func<string, bool> pred)
    {
        for (var i = arr.Count - 1; i >= 0; i--)
        {
            var id = arr[i]?["id"]?.GetValue<string>();
            if (id is not null && pred(id))
                arr.RemoveAt(i);
        }
    }

    private static int IndexAfter(JsonArray arr, string id)
    {
        for (var i = 0; i < arr.Count; i++)
        {
            if (arr[i]?["id"]?.GetValue<string>() == id)
                return i + 1;
        }

        return -1;
    }
}
