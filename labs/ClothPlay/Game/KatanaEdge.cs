using System.Drawing;
using System.Numerics;
using Novolis.Physics.Cloth;
using Novolis.Raylib.Game;
using Novolis.Simulation.World.Builders;

namespace ClothPlay.Game;

internal enum KatanaEdge
{
    /// <summary>Sharp edge faces +Y — classic “edge up” for falling cloth to meet.</summary>
    Up,

    /// <summary>Sharp edge faces −Y — blade inverted on the stand.</summary>
    Down,
}
