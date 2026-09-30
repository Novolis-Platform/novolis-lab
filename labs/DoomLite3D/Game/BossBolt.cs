using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Novolis.Simulation.World;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace DoomLite3D.Game;

internal sealed class BossBolt
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float TimeToLive = 4f;
}
