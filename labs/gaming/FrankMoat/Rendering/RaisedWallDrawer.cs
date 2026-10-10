using System.Numerics;
using FrankMoat.Art;
using FrankMoat.Levels;
using Novolis.Math.Topology;
using Novolis.Rendering.Planar;

namespace FrankMoat.Rendering;

internal sealed class RaisedWallDrawer
{
    private readonly List<PlanarWallFace> _faces = [];

    public void Build(PlanarScene scene, IReadOnlyList<RaisedWall> walls, RangeArt art)
    {
        _faces.Clear();
        foreach (var wall in walls)
        {
            var prism = wall.Footprint.Extrude(PlanarElevation.Offset(wall.Height));
            _faces.AddRange(PlanarWallComposer.Add(scene, prism, art.Wall(wall.Material)));
        }
    }

    public void TickOcclusion(Vector3 frank, float muzzle) =>
        PlanarWallComposer.TickOcclusion(_faces, frank, muzzle > 0f ? 1.14f : 1f);
}
