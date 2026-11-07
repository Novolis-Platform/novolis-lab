namespace CalypsoCad.Structure;

/// <summary>Engineering requirement for the joint (scantling-neutral for now).</summary>
internal readonly record struct JointSpecification(
    string WeldClass,
    string Notes = "");
