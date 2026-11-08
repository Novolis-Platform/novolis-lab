namespace CalypsoCad.Model;

/// <summary>Stable assembly IDs for the canonical Explorer ownership tree.</summary>
internal static class ShipHierarchyIds
{
    public const string ShipRoot = "CALYPSO";
    public const string ReferenceGeometry = "REF-GEOMETRY";
    public const string HullLoft = "REF-HULL-LOFT";
    public const string StructuralStations = "REF-STRUCT-STATIONS";
    public const string DeckElevations = "REF-DECK-ELEVATIONS";
    public const string CompartmentEnvelopes = "REF-COMPARTMENT-ENVELOPES";
    public const string ClearanceVolumes = "REF-CLEARANCE-VOLUMES";

    public const string PhysicalStructure = "PHYS-STRUCTURE";
    public const string Hull = "PHYS-HULL";
    public const string PressureHullShell = "PHYS-HULL-SHELL";
    public const string KnuckleStringers = "PHYS-KNUCKLE-STRINGERS";

    public const string Transverse = "PHYS-TRANSVERSE";
    public const string RingFrames = "PHYS-RING-FRAMES";
    public const string HeavyBulkheads = "PHYS-HEAVY-BULKHEADS";

    public const string Longitudinal = "PHYS-LONGITUDINAL";
    public const string PrimaryHullGirders = "PHYS-PRIMARY-GIRDERS";
    public const string ThrustStructure = "PHYS-THRUST";
    /// <summary>Legacy group ids — retained for old snapshots; new models use PrimaryHullGirders.</summary>
    public const string KeelGirders = PrimaryHullGirders;
    public const string PortGirders = PrimaryHullGirders;
    public const string StbdGirders = PrimaryHullGirders;

    public const string DeckStructure = "PHYS-DECK-STRUCTURE";
    public const string DeckM1 = "PHYS-DECK-M1";
    public const string Deck0 = "PHYS-DECK-0";
    public const string DeckP1 = "PHYS-DECK-P1";
    public const string ShellStiffeners = "PHYS-HULL-SHELL-STIFFENERS";
    public const string Closures = "PHYS-CLOSURES";
    public const string Machinery = "PHYS-MACHINERY";
    public const string Interior = "PHYS-INTERIOR";
    public const string Partitions = "PHYS-PARTITIONS";
    public const string Furniture = "PHYS-FURNITURE";
    public const string CargoSystems = "PHYS-CARGO-SYSTEMS";
    public const string LoadedCargo = "PHYS-LOADED-CARGO";
    public const string Diagnostics = "DIAGNOSTICS";
}
