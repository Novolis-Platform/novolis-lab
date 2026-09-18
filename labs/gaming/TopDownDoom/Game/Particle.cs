using System.Numerics;
using Novolis.Math.Geometry;
using TopDownDoom.Art;

namespace TopDownDoom.Game;

internal enum ParticleSprite
{
    SoftGlow,
    Spark,
    Smoke,
    Shell,
}

internal struct Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public float MaxLife;
    public Rgba32 ColorStart;
    public Rgba32 ColorEnd;
    public float SizeStart;
    public float SizeEnd;
    public ParticleSprite Sprite;
    public float Drag;
    public float Spin;
    public float Rotation;
}
