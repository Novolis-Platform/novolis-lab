using CalypsoCad.Generation.Longitudinal;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Decks;

/// <summary>
/// Deck-owned edge girders for accommodation decks (Deck 0 / Deck +1).
/// Follows the deck edge path; does not invent matching CL/port/stbd grids per bay.
/// Knuckle stringers remain Hull-owned and are not duplicated here.
/// </summary>
internal static class DeckSupportGenerator
{
    public static void Generate(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> ordinaryFrames,
        IReadOnlyList<(string Id, float ZStem)> bulkheads)
    {
        if (topo.DeckUps.Count < 2)
            return;

        var joints = LongitudinalRunEmitter.MergeJoints(
            topo.ZStart,
            topo.ZEnd,
            ordinaryFrames.Select(f => f.ZStem),
            bulkheads.Select(b => b.ZStem));

        // Deck 0 and Deck +1 only — Deck -1 cargo edges are CargoFloorSupportGenerator.
        for (var deckIdx = 1; deckIdx < topo.DeckUps.Count; deckIdx++)
        {
            var deckUp = topo.DeckUps[deckIdx];
            var tag = deckIdx.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var asm = $"deck-dk{tag}";
            var webH = topo.Web * 0.35f;
            var up = deckUp - topo.TPlate - webH;
            var inset = CalypsoShipbuildingFrames.DefaultSkinInset + 0.28f;

            // Continuous Z coverage: skip atrium / open-hold voids for this elevation.
            foreach (var (z0, z1) in SolidDeckSpans(topo, deckUp))
            {
                if (z1 - z0 < 1.5f)
                    continue;

                float EdgeY(float z, float sign)
                {
                    if (CalypsoShipbuildingFrames.IsOpenAtriumDeck(z, deckUp, topo.OpenAtriumSpans))
                        return float.NaN;
                    var halfB = CalypsoLockGenerator.HalfBeamInsidePublic(z, up, inset);
                    var y = halfB - topo.Web * 0.85f;
                    // Stay clear of knuckle-adjacent shell — edge girder is deck structure, not a second knuckle.
                    return y < 0.5f ? float.NaN : sign * y;
                }

                LongitudinalRunEmitter.Emit(
                    entities, loa, topo.Web, joints,
                    new LongitudinalRunEmitter.RunSpec(
                        RunId: $"deck-edge-port-dk{tag}",
                        AssemblyId: asm,
                        Role: StructuralRoles.StructuralDeck,
                        Up: up,
                        HalfCrossY: topo.Web * 0.4f,
                        HalfUp: webH,
                        Z0: z0,
                        Z1: z1,
                        YAtStem: z => EdgeY(z, -1f),
                        Purpose: $"Deck {tag} port edge support"));

                LongitudinalRunEmitter.Emit(
                    entities, loa, topo.Web, joints,
                    new LongitudinalRunEmitter.RunSpec(
                        RunId: $"deck-edge-stbd-dk{tag}",
                        AssemblyId: asm,
                        Role: StructuralRoles.StructuralDeck,
                        Up: up,
                        HalfCrossY: topo.Web * 0.4f,
                        HalfUp: webH,
                        Z0: z0,
                        Z1: z1,
                        YAtStem: z => EdgeY(z, +1f),
                        Purpose: $"Deck {tag} starboard edge support"));
            }
        }
    }

    /// <summary>Deck plate Z ranges excluding HOLD open volume for upper decks.</summary>
    private static List<(float Z0, float Z1)> SolidDeckSpans(StructuralTopology topo, float deckUp)
    {
        var floor = topo.DeckUps[0];
        var spans = new List<(float, float)> { (topo.ZStart, topo.ZEnd) };

        // Upper decks do not cross the cargo hold volume.
        if (deckUp > floor + 0.15f)
        {
            spans = Subtract(spans, topo.HoldFore, topo.HoldAft);
        }

        // Cut atrium openings for this elevation.
        foreach (var (a0, a1, floorTop) in topo.OpenAtriumSpans ?? Array.Empty<(float, float, float)>())
        {
            if (deckUp > floorTop + 0.05f)
                spans = Subtract(spans, a0, a1);
        }

        return spans;
    }

    private static List<(float Z0, float Z1)> Subtract(List<(float Z0, float Z1)> spans, float cut0, float cut1)
    {
        var next = new List<(float, float)>();
        foreach (var (a, b) in spans)
        {
            if (cut1 <= a + 0.05f || cut0 >= b - 0.05f)
            {
                next.Add((a, b));
                continue;
            }

            if (cut0 > a + 0.5f)
                next.Add((a, cut0));
            if (cut1 < b - 0.5f)
                next.Add((cut1, b));
        }

        return next;
    }
}
