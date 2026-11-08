namespace CalypsoCad.Constraints;

/// <summary>Material subtraction belonging to an assembly — not a free-floating render cue.</summary>
internal sealed record OpeningDefinition(
    string Id,
    string OwnerAssemblyId,
    OpeningPurpose Purpose,
    float Y0,
    float Y1,
    float U0,
    float U1,
    float? ZStem = null);
