using System.Numerics;
using Novolis.Simulation.SpaceCombat;

namespace XFighter.Game;

internal sealed class LaserBolt
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public bool Active;
    public bool FromPlayer = true;
}
