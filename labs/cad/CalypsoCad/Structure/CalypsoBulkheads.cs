using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Limited full-depth / pressure-tight bulkheads per calypso-structural-construction:
/// bow collision, airlock/pressure transition, and massive ENG fore/aft machinery walls.
/// Propulsion mounts live on side nacelles — no stern “thrust bulkhead.”
/// Remaining hull uses transverse web frames + longitudinals + sandwich decks.
/// </summary>
internal static class CalypsoBulkheads
{
    /// <summary>
    /// Canonical limited BH stations (z from stem) + relative thickness scale.
    /// Prefer lock <c>stations.*</c> when <paramref name="stations"/> is provided.
    /// CROSS gets one face ahead and one aft (never a plate through mid-CROSS).
    /// CREW-AFT closes the cabin band before the med/galley pack.
    /// </summary>
    public static List<(string Id, float ZStem, float ThicknessScale)> BuildSchedule(
        float loa,
        IReadOnlyDictionary<string, float>? stations = null,
        float minSep = 0.55f)
    {
        float Z(string key, float fallback) =>
            stations is not null && stations.TryGetValue(key, out var v) ? v : fallback;

        var faces = new List<(string Id, float Z, float Scale)>
        {
            ("BOW-COLLISION", Z("Z_BRIDGE_AFT", 13f), 1.6f),
            ("CROSS-FORE", Z("Z_CROSS_FORE", 19f), 1.4f),
            ("CROSS-AFT", Z("Z_CROSS_AFT", 23f), 1.4f),
            ("CREW-AFT", Z("Z_CREW_AFT", 30.28f), 1.5f),
            ("ENG-FORE", Z("Z_ENG_FORE", 41f), 3.8f),
            ("ENG-AFT", Z("Z_ENG_AFT", 49.25f), 3.8f),
        };

        faces.Sort((a, b) => a.Z.CompareTo(b.Z));
        var merged = new List<(string Id, float ZStem, float ThicknessScale)>();
        foreach (var (id, z, scale) in faces)
        {
            if (z < 1.5f || z > loa - 1.0f)
                continue;
            if (merged.Count == 0 || z - merged[^1].ZStem >= minSep)
                merged.Add((id, z, scale));
        }

        return merged;
    }

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
        foreach (var (id, zStem, scale) in BuildSchedule(loa, stations))
        {
            var thickness = Math.Clamp(tBh * scale, 0.18f, 0.65f);
            var halfDepthZ = MathF.Max(0.09f, thickness * 0.5f);
            var z = Math.Clamp(zStem, thickness, loa - thickness);
            CalypsoLockGenerator.SectionAtPublic(z, out var halfB, out var halfH, out _, out _, out _);
            if (halfB < 0.45f || halfH < 0.45f)
                continue;

            var gaps = passageGapsAt(z, 0.28f);
            var hatchPortals = hatchPortalsAt?.Invoke(z, 0.28f);
            // All BHs use coarse spans — ENG used to take the dense voxel scan and read as soup.
            FrameWebPlateBuilder.EmitBulkhead(
                entities,
                loa,
                $"bh-{id}",
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
