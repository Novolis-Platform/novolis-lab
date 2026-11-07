using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Heavy bulkhead schedule — explicit design stations (red plan lines), not frame pitch.
/// Ordinary frames at the same station are suppressed (bulkhead owns the transverse station).
/// </summary>
internal static class CalypsoBulkheads
{
    public readonly record struct Station(
        string Id,
        float ZStem,
        float ThicknessScale,
        bool PressureTight,
        string Note);

    /// <summary>
    /// Canonical heavy BH stations (z from stem). Prefer lock <c>stations.*</c> when provided.
    /// </summary>
    public static List<Station> BuildSchedule(
        float loa,
        IReadOnlyDictionary<string, float>? stations = null,
        float minSep = 0.55f)
    {
        float Z(string key, float fallback) =>
            stations is not null && stations.TryGetValue(key, out var v) ? v : fallback;

        // Red-plan heavy stations — separate from ordinary 4 m frame pitch.
        var faces = new List<Station>
        {
            new("BOW-COLLISION", Z("Z_BRIDGE_AFT", 13f), 1.6f, PressureTight: true,
                "Bow collision / BRIDGE aft — follows tapered IML at this station"),
            new("CROSS-FORE", Z("Z_CROSS_FORE", 19f), 1.4f, PressureTight: true,
                "CROSS fore face — ACCESS/CROSS passageway openings"),
            new("CROSS-AFT", Z("Z_CROSS_AFT", 23f), 1.4f, PressureTight: true,
                "CROSS aft face — CORR openings"),
            new("CREW-AFT", Z("Z_CREW_AFT", 30.28f), 1.5f, PressureTight: true,
                "Crew cabin band aft diaphragm"),
            new("ENG-FORE", Z("Z_ENG_FORE", 41f), 3.8f, PressureTight: true,
                "Engineering atrium fore — machinery pressure wall"),
            new("ENG-AFT", Z("Z_ENG_AFT", 49.25f), 3.8f, PressureTight: true,
                "Engineering / HOLD interface — machinery pressure wall"),
        };

        faces.Sort((a, b) => a.ZStem.CompareTo(b.ZStem));
        var merged = new List<Station>();
        foreach (var face in faces)
        {
            if (face.ZStem < 1.5f || face.ZStem > loa - 1.0f)
                continue;
            if (!HullSectionAuthority.HasStructuralSection(face.ZStem))
                continue;
            if (merged.Count == 0 || face.ZStem - merged[^1].ZStem >= minSep)
                merged.Add(face);
        }

        return merged;
    }

    /// <summary>True when an ordinary frame at <paramref name="zStem"/> is owned by a heavy BH.</summary>
    public static bool OwnsFrameStation(
        float zStem,
        IReadOnlyList<Station> schedule,
        float tol = 2.05f) => // half of 4 m bay + ε — BH owns nearest ordinary station
        schedule.Any(b => MathF.Abs(b.ZStem - zStem) <= tol);

    public static Dictionary<string, float> StationsFromLock(JsonElement root)
    {
        var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("stations", out var st) || st.ValueKind != JsonValueKind.Object)
            return map;
        foreach (var p in st.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Number)
                map[p.Name] = (float)p.Value.GetDouble();
        }

        return map;
    }

    public static void AddAll(
        List<CadEntity> entities,
        float loa,
        float tBh,
        Func<float, float, List<(float Y0, float Y1)>> passageGapsAt,
        IReadOnlyList<float> deckUps,
        IReadOnlyDictionary<string, float>? stations = null,
        Func<float, float, List<FrameWebPlateBuilder.WebPlatePortal>>? hatchPortalsAt = null)
    {
        foreach (var face in BuildSchedule(loa, stations))
        {
            var thickness = Math.Clamp(tBh * face.ThicknessScale, 0.18f, 0.65f);
            var halfDepthZ = MathF.Max(0.09f, thickness * 0.5f);
            var z = Math.Clamp(face.ZStem, thickness, loa - thickness);
            if (!HullSectionAuthority.HasStructuralSection(z))
            {
                StructurePlacementLog.Record(
                    $"bh-{face.Id}", 0f, 0f, z, 0f, 0f, halfDepthZ,
                    "no structural hull section at BH station");
                continue;
            }

            var gaps = passageGapsAt(z, 0.28f);
            var hatchPortals = hatchPortalsAt?.Invoke(z, 0.28f);
            // Bow BHs use local section (bowAligned) so plate follows taper — not constant midbody width.
            FrameWebPlateBuilder.EmitBulkhead(
                entities,
                loa,
                $"bh-{face.Id}",
                z,
                thickness,
                halfDepthZ,
                gaps,
                bowAligned: z < 18f,
                deckUps,
                skipLightening: true,
                useCoarseSpans: true,
                hatchPortals: hatchPortals);
        }
    }
}
