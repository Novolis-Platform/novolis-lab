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
    /// <summary>Structure + airtight interiors without OML — passage tubes and HOLD bay read (OML wire guide still draws).</summary>
    InteriorStructureNoHull = Structure | Partitions | Outfit | SpaceFinish | Openings | PassagewayCutouts,
    /// <summary>DK-001 deck plan: blue shirt-sleeves + rooms + HOLD bay — no OML, frames, or outfit clutter.</summary>
    Dk001Plan = Partitions | SpaceFinish | Openings | PassagewayCutouts,
    /// <summary>Frames/BH/girders only — passageway cutouts visible, no OML.</summary>
    StructureNoHull = Structure | PassagewayCutouts | Openings,
    All = DefaultOrbit,
}
