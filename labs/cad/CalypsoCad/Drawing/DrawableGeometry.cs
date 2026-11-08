using System.Numerics;

namespace CalypsoCad.Drawing;

/// <summary>Normalized drawable unit sourced from a CAD entity (or reference).</summary>
internal sealed class DrawableGeometry
{
    public required Guid SourceEntityId { get; init; }
    public required string SourceName { get; init; }
    public required string SemanticNodeId { get; init; }
    public required DrawablePrimitiveKind Kind { get; init; }
    public string? UnsupportedReason { get; init; }

    /// <summary>Triangle mesh in world space (CAD +X stbd, +Y up, +Z bow).</summary>
    public Vector3[]? Vertices { get; init; }
    public int[]? Indices { get; init; }

    /// <summary>Open or closed polylines (paths / edges).</summary>
    public IReadOnlyList<Vector3[]>? Polylines { get; init; }

    public DrawingBounds3 Bounds { get; init; }
}
