namespace CalypsoCad.Model;

/// <summary>Nonphysical design references — never counted as manufactured steel.</summary>
internal enum CadReferenceKind
{
    Other,
    HullLoft,
    OuterMoldedLine,
    InnerMoldedLine,
    LoftStation,
    StructuralStation,
    DeckElevation,
    CompartmentEnvelope,
    ClearanceVolume,
    PassageReservation,
    CutawayPlane,
    RenderingGuide,
}
