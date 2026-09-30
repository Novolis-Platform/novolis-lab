namespace PulseStrip.Core;

using System.Numerics;

/// <summary>Collectible pad on the circuit.</summary>
public sealed class TrackPickup
{
    public required int Id { get; init; }
    public required PickupKind Kind { get; init; }
    public required Vector3 Position { get; set; }
    public bool Available { get; set; } = true;
    public float RespawnTimer { get; set; }
}
