namespace ArtillerySimulator;

/// <summary>One seated tube in a level battery (piece + pad offset from mission fort).</summary>
internal readonly record struct BatteryMount(
    string PieceSpecId,
    string Label,
    float OffsetEastMeters,
    float OffsetNorthMeters,
    float OpeningElevationDegrees);
