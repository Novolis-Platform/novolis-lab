using System.Numerics;

namespace ArtillerySimulator;

internal readonly struct GunSite(Vector3 position, float openingElevationDegrees, float columnLeadMeters)
{
    public Vector3 Position { get; } = position;
    public float OpeningElevationDegrees { get; } = openingElevationDegrees;
    public float ColumnLeadMeters { get; } = columnLeadMeters;
}
