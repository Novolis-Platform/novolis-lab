using System.Numerics;
using Novolis.Math.Geometry;

namespace FrankMoat.Particles;

internal struct Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Elevation;
    public float ElevationVelocity;
    public float Life;
    public float MaxLife;
    public Rgba32 ColorStart;
    public Rgba32 ColorEnd;
    public float SizeStart;
    public float SizeEnd;
    public ParticleKind Kind;
    public float Drag;
    public float Spin;
    public float Rotation;
    public bool Sticky;
}
