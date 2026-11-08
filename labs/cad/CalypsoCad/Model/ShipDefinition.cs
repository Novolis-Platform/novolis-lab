namespace CalypsoCad.Model;

/// <summary>Canonical ship identity and principal dimensions for one generate pass.</summary>
internal sealed record ShipDefinition(
    string ShipId,
    float LoaM,
    float BeamM,
    float OahM,
    IReadOnlyList<float> DeckUps,
    IReadOnlyDictionary<string, float> Stations);
