namespace CalypsoCad.Session;

/// <summary>
/// CAD visibility flags — one concern per bit. Do not overload “Structure” for cargo/spaces.
/// </summary>
[Flags]
internal enum CalypsoVisualLayers
{
    None = 0,

    /// <summary>OML / IML shell meshes.</summary>
    HullShell = 1 << 0,

    /// <summary>Transverse bay web frames (<c>frame-F*</c>).</summary>
    Frames = 1 << 1,

    /// <summary>Pressure / compartment bulkheads (<c>bh-*</c>).</summary>
    Bulkheads = 1 << 2,

    /// <summary>Longitudinal girders + transverse deckbeams.</summary>
    Longitudinals = 1 << 3,

    /// <summary>Cellular sandwich deck panels (<c>deck-sandwich-*</c>) — faces + cores.</summary>
    DeckPlates = 1 << 4,

    /// <summary>Deck face sheets only (opaque floors) — hide for structure inspection.</summary>
    DeckFaces = 1 << 14,

    /// <summary>Cellular core webs / perimeter — inspection without opaque floors.</summary>
    DeckCores = 1 << 15,

    /// <summary>Interior partition walls.</summary>
    Partitions = 1 << 5,

    /// <summary>Furniture / outfit boxes (not cargo).</summary>
    Outfit = 1 << 6,

    /// <summary>Habitable / atrium / cargo space finish volumes.</summary>
    SpaceFinish = 1 << 7,

    /// <summary>Doors, hatches, ramps.</summary>
    Openings = 1 << 8,

    /// <summary>Diagnostic overlay for passageway tracks (not structural geometry).</summary>
    PassagewayCutouts = 1 << 9,

    /// <summary>HOLD C40 container stack only.</summary>
    Cargo = 1 << 10,

    /// <summary>Airlock chambers + D1/D2/D3 (pressure transition to SPACE).</summary>
    Airlocks = 1 << 11,

    /// <summary>Join stubs / brackets at frame×girder (optional detail).</summary>
    Joinery = 1 << 12,

    /// <summary>
    /// Outer hull cage only: perimeter frame rings, skin stringers/girders, bottom floor strip.
    /// Exclusive of BH plates, internal chords, outfit. See HOLD bay-test-noshell look.
    /// </summary>
    OuterHull = 1 << 13,

    /// <summary>
    /// Full-height ship elements (keel→weather / LOA). Not deck-scoped — deck filter must not hide these.
    /// Hull + transverse webs + pressure BHs + longitudinals.
    /// </summary>
    Ship = HullShell | Frames | Bulkheads | Longitudinals,

    /// <summary>Deck-scoped content (plates, rooms, outfit, cargo, openings).</summary>
    DeckScoped = DeckPlates | Partitions | Outfit | SpaceFinish | Openings | Cargo | PassagewayCutouts
                 | Airlocks | Joinery,

    /// <summary>Primary steel skeleton (ship steel + sandwich decks).</summary>
    Structure = Ship | DeckPlates,

    // PassagewayCutouts is opt-in — portals are already booleaned into webs/BHs.
    DefaultOrbit = Ship | DeckPlates | Partitions | Outfit | Cargo | SpaceFinish | Openings
                   | Airlocks | Joinery,
    DefaultInterior = Partitions | Outfit | Cargo | SpaceFinish | Openings,
    CorridorStructure = DefaultInterior | Structure,
    StructureOnlyCutaway = Ship | DeckPlates,
    FitoutCutaway = HullShell | Partitions | Outfit | Cargo | SpaceFinish | Openings,
    InteriorStructureNoHull = Structure | Partitions | Outfit | Cargo | SpaceFinish | Openings,
    Dk001Plan = Partitions | SpaceFinish | Openings | PassagewayCutouts,
    /// <summary>Ship steel without hull shell + decks.</summary>
    StructureNoHull = Frames | Bulkheads | Longitudinals | DeckPlates | Openings,
    /// <summary>Hide-hull skeleton test: open webs + BH + girders (no decks / outfit).</summary>
    ShipSkeleton = Frames | Bulkheads | Longitudinals,
    /// <summary>
    /// Outer skeleton layer — IML cage (rings + skin longs + floor), shell hidden.
    /// </summary>
    OuterSkeleton = OuterHull,
    /// <summary>Stage 1 — primary steel skeleton (alias of <see cref="ShipSkeleton"/>).</summary>
    PrimaryStructure = ShipSkeleton,
    /// <summary>
    /// Critical structures only: pressure BHs + open frames + sandwich deck plates (+ seats).
    /// </summary>
    CriticalStructure = Frames | Bulkheads | DeckPlates | Joinery,
    /// <summary>
    /// Structural inspection: frames + girders + deck cores + joinery — deck faces hidden.
    /// </summary>
    StructureInspect = Frames | Longitudinals | DeckCores | Joinery,
    /// <summary>Stage 2 — primary + cellular sandwich decks.</summary>
    PrimaryPlusDecks = PrimaryStructure | DeckPlates,
    /// <summary>Stage 3 — pressure boundary (shell + BH + openings).</summary>
    PressureStructure = HullShell | Bulkheads | Openings,
    /// <summary>Stage 4 — full arrangement (alias of <see cref="DefaultOrbit"/>).</summary>
    CompleteArrangement = DefaultOrbit,
    /// <summary>Open HOLD read: ship steel + decks + containers.</summary>
    HoldCargo = Frames | Bulkheads | Longitudinals | DeckPlates | Cargo | Openings,
    All = Ship | DeckScoped,
}
