namespace CalypsoCad.Model;

/// <summary>Typed connection between parts or assemblies (replaces string join lists).</summary>
internal sealed record StructuralConnection(
    string Id,
    PartId? PartA,
    PartId? PartB,
    AssemblyId? AssemblyA,
    AssemblyId? AssemblyB,
    string JointType,
    string Interface,
    string Specification,
    bool GeometryValid);
