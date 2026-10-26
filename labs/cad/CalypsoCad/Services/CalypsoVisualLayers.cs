namespace CalypsoCad.Services;

/// <summary>Toggleable draw groups for orbit / interior / plan (wet-ship layer read).</summary>
[Flags]
internal enum CalypsoVisualLayers
{
    None = 0,
    HullShell = 1 << 0,
    Structure = 1 << 1,
    Partitions = 1 << 2,
    Outfit = 1 << 3,
    SpaceFinish = 1 << 4,
    Openings = 1 << 5,
    PassagewayCutouts = 1 << 6,

    DefaultOrbit = HullShell | Structure | Partitions | Outfit | SpaceFinish | Openings | PassagewayCutouts,
    DefaultInterior = Partitions | Outfit | SpaceFinish | Openings,
    CorridorStructure = DefaultInterior | Structure | PassagewayCutouts,
    StructureOnlyCutaway = HullShell | Structure | PassagewayCutouts,
    FitoutCutaway = HullShell | Partitions | Outfit | SpaceFinish | Openings | PassagewayCutouts,
    All = DefaultOrbit,
}
