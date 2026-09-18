namespace PulseStrip.Core;

using System.Numerics;

/// <summary>Plasma bolt fired by a hover craft.</summary>
public sealed class HoverProjectile
{
    public required int OwnerId { get; init; }
    public required Vector3 Position { get; set; }
    public required Vector3 Velocity { get; set; }
    public float Life { get; set; } = 1.4f;
    public bool Active { get; set; } = true;
}
