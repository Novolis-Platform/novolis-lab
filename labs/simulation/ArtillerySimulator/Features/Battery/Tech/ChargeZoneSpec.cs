namespace ArtillerySimulator;

/// <summary>
/// One modular charge zone for a piece. Add new zones by appending to a piece's charge list —
/// consumers index by ordinal (0…n-1) the same way the UI and agent do today.
/// </summary>
internal sealed class ChargeZoneSpec
{
    public ChargeZoneSpec(string id, string label, float muzzleSpeedMps, float referenceMaxRangeKm)
    {
        Id = id;
        Label = label;
        MuzzleSpeedMps = muzzleSpeedMps;
        ReferenceMaxRangeKm = referenceMaxRangeKm;
    }

    public string Id { get; }
    public string Label { get; }
    public float MuzzleSpeedMps { get; }
    public float ReferenceMaxRangeKm { get; }
}
