using System.Numerics;

namespace ArtillerySimulator;

internal struct RouteLane
{
    public GroundPolyline Line;
    public float CrossingArc;
    public int Away;
    public string Name;
    /// <summary>Cached world-space ribbon samples; built once when the lane is spawned.</summary>
    public Vector3[]? Ribbon;
}
