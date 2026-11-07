namespace CalypsoCad.Structure;

/// <summary>
/// Lock-driven primary structure topology — stations, open bays, decks — before rooms/partitions.
/// Authority for emit order: BH → frames → girders → sandwich → coaming (see calypso-structural-construction).
/// </summary>
internal sealed class StructuralTopology
{
    public required float Loa { get; init; }
    public required float Web { get; init; }
    public required float Depth { get; init; }
    public required float TBh { get; init; }
    public required float FrameBay { get; init; }
    public required float TPlate { get; init; }
    public required IReadOnlyList<float> DeckUps { get; init; }
    public required IReadOnlyList<(string Id, float ZStem, bool BowAligned)> FrameStations { get; init; }
    public required IReadOnlyList<(float Z0, float Z1, float FloorTop)> OpenAtriumSpans { get; init; }
    public required float HoldFore { get; init; }
    public required float HoldAft { get; init; }
    public required float HoldDoorBand { get; init; }
    public required float ZStart { get; init; }
    public required float ZEnd { get; init; }
    public required IReadOnlyList<(int DeckNum, float PlateTop)> SandwichDecks { get; init; }

    public bool InOpenHoldCell(float zStem) =>
        zStem > HoldFore + 0.05f && zStem < HoldAft - HoldDoorBand;
}
