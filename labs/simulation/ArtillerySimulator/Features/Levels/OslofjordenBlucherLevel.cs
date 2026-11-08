namespace ArtillerySimulator;

/// <summary>
/// Oscarsborg vs invading German fleet, night of 9 April 1940 (educational fiction on procedural fjord).
/// Ships steam from the south. Tab cycles 28 cm / M777 / HIMARS GMLRS.
/// </summary>
internal sealed class OslofjordenBlucherLevel : IGameLevel
{
    public const string LevelId = "oslofjorden.blucher";

    public string Id => LevelId;
    public string DisplayName => "Defense of Oslofjorden";
    public string SiteName => "Oscarsborg";
    public string Blurb =>
        "9 April 1940. Hold Oscarsborg as the fleet forces Drøbak Sound from the south. Tab: 28 cm / M777 / HIMARS.";
    public string PieceSpecId => ProductIdentity.Oscarsborg28PieceSpecId;
    public bool IsPlayable => true;

    public IReadOnlyList<BatteryMount> ResolveBattery() =>
    [
        // Historical main battery — Kaholmen.
        new BatteryMount(
            ProductIdentity.Oscarsborg28PieceSpecId,
            "28 cm COASTAL",
            OffsetEastMeters: 20f,
            OffsetNorthMeters: -15f,
            OpeningElevationDegrees: 22f),
        // Modern educational howitzer — south-facing pad.
        new BatteryMount(
            ProductIdentity.M777PieceSpecId,
            "M777 155",
            OffsetEastMeters: -70f,
            OffsetNorthMeters: -110f,
            OpeningElevationDegrees: 24f),
        // HIMARS far back (north/east rear) for long GMLRS sheaves into the Sound.
        new BatteryMount(
            ProductIdentity.HimarsPieceSpecId,
            "HIMARS GMLRS",
            OffsetEastMeters: 1_450f,
            OffsetNorthMeters: 1_100f,
            OpeningElevationDegrees: 42f),
    ];

    public void RebuildMap(TerrainWorld terrain) => terrain.Rebuild(OslofjordenMapTerrain.Instance);

    public IDefenseMission StartMission(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed) =>
        new OslofjordenBlucherMission(terrain, piece, seed);
}
