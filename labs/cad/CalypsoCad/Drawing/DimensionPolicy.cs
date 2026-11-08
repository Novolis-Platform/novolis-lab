namespace CalypsoCad.Drawing;

internal sealed record DimensionPolicy(
    DimensionPolicyKind Kind = DimensionPolicyKind.OverallExtents,
    IReadOnlyList<string>? ExplicitIntentIds = null);
