using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>
/// Limited full-depth / pressure-tight bulkheads per calypso-structural-construction:
/// bow collision, airlock/pressure transition, and massive ENG fore/aft machinery walls.
/// Propulsion mounts live on side nacelles — no stern “thrust bulkhead.”
/// Remaining hull uses transverse web frames + longitudinals + sandwich decks.
/// </summary>
internal static class CalypsoBulkheads
{
    /// <summary>Canonical limited BH stations (z from stem) + relative thickness scale.</summary>
    public static List<(string Id, float ZStem, float ThicknessScale)> BuildSchedule(
        float loa, float minSep = 0.55f)
    {
        var faces = new List<(string Id, float Z, float Scale)>
        {
            // Bow / forward collision protection
            ("BOW-COLLISION", 13f, 1.6f),
            // Airlock / pressure-transition (CROSS band)
            ("AIRLOCK-CROSS", 21f, 1.4f),
            // Engineering machinery boundaries — on stations.* SoT (Z_ENG_FORE / Z_ENG_AFT)
            ("ENG-FORE", 41f, 3.8f),
            ("ENG-AFT", 49.25f, 3.8f),
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

    public static void AddAll(
        List<CadEntity> entities,
        float loa,
        float tBh,
        Func<float, float, List<(float Y0, float Y1)>> passageGapsAt,
        IReadOnlyList<float> deckUps)
    {
        foreach (var (id, zStem, scale) in BuildSchedule(loa))
        {
            var thickness = Math.Clamp(tBh * scale, 0.18f, 0.65f);
            var halfDepthZ = MathF.Max(0.09f, thickness * 0.5f);
            var z = Math.Clamp(zStem, thickness, loa - thickness);
            CalypsoLockGenerator.SectionAtPublic(z, out var halfB, out var halfH, out _, out _, out _);
            if (halfB < 0.45f || halfH < 0.45f)
                continue;

            var gaps = passageGapsAt(z, 0.28f);
            // ENG machinery walls: dense plate scan so atrium faces read as continuous steel.
            // Bow/airlock: coarse spans (thin remnant wings after passageway cuts).
            var engMassive = id.StartsWith("ENG-", StringComparison.OrdinalIgnoreCase);
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
                // Pressure BH: passageway cuts only — no lightening swiss-cheese.
                skipLightening: true,
                useCoarseSpans: !engMassive);
        }
    }
}
