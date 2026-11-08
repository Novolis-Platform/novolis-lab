namespace CalypsoCad.Layout;

/// <summary>
/// Shared interior arrangement computed before ring-frame internals and wall fabrication.
/// Structure and partition panels must consume the same definitions.
/// </summary>
internal sealed class CompartmentLayout
{
    public IReadOnlyList<CompartmentBound> Compartments { get; init; } = [];
    public IReadOnlyList<LongitudinalPartitionPath> LongitudinalPartitions { get; init; } = [];
    public IReadOnlyList<TransversePartitionPath> TransversePartitions { get; init; } = [];
    public IReadOnlyList<PassageEnvelope> Passages { get; init; } = [];
    public IReadOnlyList<float> DeckUps { get; init; } = [];
    public float TPlate { get; init; } = 0.1f;
    public float RoomClearH { get; init; } = 3.2f;
    public float DoorClearH { get; init; } = 2.1f;
    public float CorrInner { get; init; } = 5f;
    public float CorrOuter { get; init; } = 8f;
    public float CorrW { get; init; } = 3f;

    public bool ContainsStem(CompartmentBound c, float zStem) =>
        zStem > c.Z0 + 0.05f && zStem < c.Z1 - 0.05f;

    public IEnumerable<LongitudinalPartitionPath> LongitudinalAtStem(float zStem) =>
        LongitudinalPartitions.Where(p => zStem > p.Z0 + 0.05f && zStem < p.Z1 - 0.05f);

    public IEnumerable<PassageEnvelope> PassagesAtStem(float zStem) =>
        Passages.Where(p => zStem > p.Z0 + 0.05f && zStem < p.Z1 - 0.05f);

    public bool YInsideAnyPassage(float y, float zStem, float margin = 0.05f)
    {
        foreach (var p in PassagesAtStem(zStem))
        {
            if (y >= p.Y0 + margin && y <= p.Y1 - margin)
                return true;
        }

        return false;
    }
}
