namespace CalypsoCad.Structure;

/// <summary>
/// First-class connection between two named structural members.
/// Presence of a record does not prove geometry is valid — validators check interfaces.
/// </summary>
internal sealed record StructuralJoint(
    string Id,
    string MemberA,
    string MemberB,
    StructuralJointType Type,
    JointInterface Interface,
    JointSpecification Specification,
    bool GeometryValid = false);
