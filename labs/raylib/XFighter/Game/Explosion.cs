using System.Numerics;
using Novolis.Simulation.SpaceCombat;

namespace XFighter.Game;

internal sealed class Explosion
{
    public Vector3 Position;
    public float Life;
    public float MaxLife;
    public bool Active;
}
