using System.Text.Json;
using System.Text.Json.Nodes;

namespace CalypsoCad.Generation.Blueprints;

/// <summary>
/// Widens port/stbd spine corridors outboard and rebuilds affected plan rings / hatch stations in CAL-INT-GA-001.json.
/// </summary>
internal static class InternalsLockCorridorPatch
{
    /// <summary>Inner face of spine (center stack edge). Unchanged.</summary>
    public const double CorrInner = 5.0;

    /// <summary>Clear width of each spine passageway (was 2.0).</summary>
    public const double CorrW = 3.0;

    /// <summary>Outboard face — pushed toward shell (was 7.0).</summary>
    public const double CorrOuter = CorrInner + CorrW; // 8.0

    public static void Apply(string lockJsonPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(lockJsonPath))
                   ?? throw new InvalidOperationException("Failed to parse lock JSON");

        var circ = root["circulation"]?.AsObject()
                   ?? throw new InvalidOperationException("circulation missing");
        circ["CORR_INNER"] = CorrInner;
        circ["CORR_W"] = CorrW;
        circ["CORR_OUTER"] = CorrOuter;

        foreach (var c in root["compartments"]!.AsArray())
        {
            var id = c?["id"]?.GetValue<string>();
            if (id is "CORR_P")
            {
                c!["y0"] = -CorrOuter;
                c["y1"] = -CorrInner;
                c["note"] = $"port spine corridor clear {CorrW} m outboard; same track −1/+1";
                c["planRing"] = BuildPlanRing(-CorrOuter, -CorrInner, c["z0"]!.GetValue<double>(), c["z1"]!.GetValue<double>());
            }
            else if (id is "CORR_S")
            {
                c!["y0"] = CorrInner;
                c["y1"] = CorrOuter;
                c["note"] = $"starboard spine corridor clear {CorrW} m outboard; same track −1/+1";
                c["planRing"] = BuildPlanRing(CorrInner, CorrOuter, c["z0"]!.GetValue<double>(), c["z1"]!.GetValue<double>());
            }
        }

        var corrCl = CorrInner + CorrW * 0.5;
        foreach (var h in root["hatches"]!.AsArray())
        {
            var id = h?["id"]?.GetValue<string>() ?? "";
            if (id.StartsWith("HOLD-P", StringComparison.Ordinal))
                h!["y"] = -corrCl;
            else if (id.StartsWith("HOLD-S", StringComparison.Ordinal))
                h!["y"] = corrCl;
            // ENG / INF / GAL / CAB* stay on ±CORR_INNER (inner BH face)
        }

        if (root["corridorCenterlines"] is JsonArray cls)
        {
            foreach (var cl in cls)
            {
                var id = cl?["id"]?.GetValue<string>();
                if (id is "CORR_P_CL")
                    cl!["y"] = -corrCl;
                else if (id is "CORR_S_CL")
                    cl!["y"] = corrCl;
            }
        }

        var rev = root["rev"]?.GetValue<string>() ?? "B";
        if (string.CompareOrdinal(rev, "C") < 0)
            root["rev"] = "C";

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(lockJsonPath, root.ToJsonString(opts));
    }

    private static JsonArray BuildPlanRing(double y0, double y1, double z0, double z1, int samples = 16)
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
}
