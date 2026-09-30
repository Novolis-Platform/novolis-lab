using System.Numerics;
using System.Runtime.InteropServices;
using Novolis.Physics.Cloth;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;
using Novolis.Simulation.World.Builders;

namespace ClothPlay.Game;

internal enum ClothScenario
{
    Flag,
    DropDrape,
    DropCut,
}
