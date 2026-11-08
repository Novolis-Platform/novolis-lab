namespace CalypsoCad.Model;

/// <summary>
/// Physical part kinds — engineering identity independent of mesh/box representation.
/// Ring-frame <em>assemblies</em> are <see cref="CadAssemblyKind"/>; these are the parts they own.
/// </summary>
internal enum CadPartKind
{
    ShellPanel,

    /// <summary>Plate with outer hull-conforming boundary and large central opening (polygonal annulus).</summary>
    AnnularWebPlate,

    /// <summary>Reinforcing flange following an annular web perimeter.</summary>
    RingFlange,

    /// <summary>Local stiffener on a web plate.</summary>
    WebStiffener,

    /// <summary>Athwart deck chord at a transverse station.</summary>
    DeckChord,

    /// <summary>Legacy alias for annular web / open-web frame parts — prefer <see cref="AnnularWebPlate"/>.</summary>
    RingFrame,

    /// <summary>Legacy alias — prefer <see cref="AnnularWebPlate"/>.</summary>
    WebFrame,

    BulkheadPlate,
    HullStringer,

    /// <summary>Manufactured segment of a continuous outer-hull knuckle path.</summary>
    KnuckleStringer,

    HullStiffener,
    ShellReinforcement,
    LongitudinalGirder,
    GirderStiffener,
    ConnectionBracket,
    DeckPanel,
    DeckFaceSheet,
    DeckCoreWeb,
    DeckPerimeterMember,
    DeckBeam,
    DeckEdgeGirder,
    StructuralBracket,
    Hatch,
    PressureDoor,
    AirlockStructure,
    CargoDoor,
    HatchCollar,
    Partition,
    InteriorFinish,
    Furniture,
    Fixture,
    Machinery,
    EquipmentFoundation,
    ServiceComponent,
    CargoContainer,
    ThrustMember,
    Unknown,
}
