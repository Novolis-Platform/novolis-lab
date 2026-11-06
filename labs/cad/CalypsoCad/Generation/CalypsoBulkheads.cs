using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>
/// Limited full-depth / pressure-tight bulkheads per calypso-structural-construction:
/// ENG, airlock/pressure transition, bow collision, stern thrust — not every bay or cabin face.
/// Remaining hull uses transverse web frames + longitudinals + sandwich decks.
/// </summary>
internal static class CalypsoBulkheads
{
    /// <summary>Canonical limited BH stations (z from stem).</summary>
    public static List<(string Id, float ZStem)> BuildSchedule(float loa, float minSep = 0.55f)
    {
        var faces = new List<(string Id, float Z)>
        {
            // Bow / forward collision protection
            ("BOW-COLLISION", 13f),
            // Airlock / pressure-transition (CROSS band)
            ("AIRLOCK-CROSS", 21f),
            // Engineering machinery boundaries
            ("ENG-FORE", 41.1f),
            ("ENG-AFT", 49.3f),
            // Stern / propulsion / aft closure
            ("STERN-THRUST", MathF.Max(loa - 4f, 65f)),
        };

        faces.Sort((a, b) => a.Z.CompareTo(b.Z));
        var merged = new List<(string Id, float ZStem)>();
        foreach (var (id, z) in faces)
        {
            if (z < 1.5f || z > loa - 1.0f)
                continue;
            if (merged.Count == 0 || z - merged[^1].ZStem >= minSep)
                merged.Add((id, z));
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
        var halfDepthZ = MathF.Max(0.06f, tBh * 0.5f);
        foreach (var (id, zStem) in BuildSchedule(loa))
        {
            var z = Math.Clamp(zStem, tBh, loa - tBh);
            CalypsoLockGenerator.SectionAtPublic(z, out var halfB, out var halfH, out _, out _, out _);
            if (halfB < 0.45f || halfH < 0.45f)
                continue;

            var gaps = passageGapsAt(z, 0.28f);
            FrameWebPlateBuilder.EmitBulkhead(
                entities,
                loa,
                $"bh-{id}",
                z,
                tBh,
                halfDepthZ,
                gaps,
                bowAligned: z < 18f,
                deckUps);
        }
    }
}
