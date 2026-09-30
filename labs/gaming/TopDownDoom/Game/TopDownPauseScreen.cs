using System.Numerics;
using Novolis.Game.MenuFlows;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.TwoD;
using Novolis.Rendering.Presentation;
using TopDownDoom.Art;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

file sealed class TopDownPauseScreen : PauseScreenBase
{
    public override string ScreenId => "top-down-doom-pause";
}
