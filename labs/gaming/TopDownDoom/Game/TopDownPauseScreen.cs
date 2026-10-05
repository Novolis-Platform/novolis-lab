using System.Numerics;
using Novolis.Game.MenuFlows;
using Novolis.Math.Geometry;
using Novolis.Silk;
using Novolis.Rendering.TwoD;
using TopDownDoom.Art;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

sealed class TopDownPauseScreen : PauseScreenBase
{
    public override string ScreenId => "top-down-doom-pause";
}
