namespace CalypsoCad.Model;

/// <summary>Engineering assembly that owns one or more physical parts.</summary>
internal sealed record CadAssembly(
    AssemblyId Id,
    CadSystem System,
    string DisplayName,
    float? StationZStem = null,
    PartFunction Functions = PartFunction.None,
    IReadOnlyList<PartId>? MemberPartIds = null,
    CadAssemblyKind Kind = CadAssemblyKind.Other);
