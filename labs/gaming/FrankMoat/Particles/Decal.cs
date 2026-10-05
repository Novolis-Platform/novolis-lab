using System.Numerics;
using Novolis.Math.Geometry;

namespace FrankMoat.Particles;

internal struct Decal
{
    public Vector3 Position;
    public float Elevation;
    public float Width;
    public float Height;
    public float Rotation;
    public Rgba32 Color;
    public DecalKind Kind;
}
