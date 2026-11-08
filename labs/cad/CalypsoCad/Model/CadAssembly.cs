namespace CalypsoCad.Model;

/// <summary>
/// Engineering assembly that may own parts and nested child assemblies.
/// <see cref="ParentAssemblyId"/> is the authoritative ownership link; children are derived.
/// </summary>
internal sealed record CadAssembly(
    AssemblyId Id,
    CadSystem System,
    string DisplayName,
    float? StationZStem = null,
    PartFunction Functions = PartFunction.None,
    IReadOnlyList<PartId>? MemberPartIds = null,
    CadAssemblyKind Kind = CadAssemblyKind.Other,
    AssemblyId? ParentAssemblyId = null,
    float? ExtentZ0 = null,
    float? ExtentZ1 = null);
