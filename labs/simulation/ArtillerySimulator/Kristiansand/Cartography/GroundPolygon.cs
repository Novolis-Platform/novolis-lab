using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Named water or land polygon in world meters. X is east, Y of the vector is north (world Z).</summary>
internal sealed class GroundPolygon
{
    public GroundPolygon(string name, Vector2[] ring)
    {
        Name = name;
        Ring = ring;
    }

    public string Name { get; }

    public Vector2[] Ring { get; }
}
