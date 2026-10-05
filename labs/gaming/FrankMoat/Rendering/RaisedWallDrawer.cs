using System.Numerics;
using FrankMoat.Art;
using FrankMoat.Levels;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Rendering;

internal sealed class RaisedWallDrawer
{
    private readonly List<WallFaceSprite> _faces = [];

    public IReadOnlyList<WallFaceSprite> Faces => _faces;

    public void Build(TwoDScene scene, IReadOnlyList<RaisedWall> walls, RangeArt art)
    {
        _faces.Clear();
        foreach (var wall in walls)
        {
            AddWall(scene, wall, art);
        }
    }

    public void TickOcclusion(Vector3 frank, float muzzle)
    {
        foreach (var face in _faces)
        {
            var alpha = 255;
            if (face.Occludes && PointInExtrusion(frank, face.A, face.B, face.Extrusion))
            {
                alpha = 86;
            }

            var lit = muzzle > 0f ? 1.14f : 1f;
            face.Polygon.FillColor = new Rgba32(Mul(255, lit), Mul(255, lit), Mul(255, lit), (byte)alpha);
        }
    }

    private void AddWall(TwoDScene scene, RaisedWall wall, RangeArt art)
    {
        var ext = HeightProjection.Offset(wall.Height);
        var texture = art.Wall(wall.Material);
        foreach (var segment in wall.Segments)
        {
            var edge = segment.End - segment.Start;
            var outward = new Vector3(edge.Z, 0f, -edge.X);
            var southOrEast = Vector3.Dot(outward, new Vector3(0.15f, 0f, -1f)) > 0.05f
                || Vector3.Dot(outward, new Vector3(1f, 0f, 0f)) > 0.05f;
            if (!southOrEast)
            {
                continue;
            }

            var occludes = true;
            var a = segment.Start;
            var b = segment.End;
            var shape = new Polygon([a, b, b + ext, a + ext]);
            var poly = new TwoDStaticPolygon(shape, Rgba32.White)
            {
                DrawFilled = true,
                Texture = texture,
                TextureMeters = 2f,
                SortKey = 40 + (int)(MathF.Min(a.Z, b.Z) * 4f),
            };
            scene.StaticPolygons.Add(poly);
            _faces.Add(new WallFaceSprite
            {
                Polygon = poly,
                A = a,
                B = b,
                Extrusion = ext,
                Material = wall.Material,
                Occludes = occludes,
            });
        }

        AddTop(scene, wall, ext, texture);
    }

    private static void AddTop(TwoDScene scene, RaisedWall wall, Vector3 ext, TwoDTextureId texture)
    {
        if (wall.Footprint.Length < 3)
        {
            return;
        }

        var lifted = new Vector3[wall.Footprint.Length];
        var minZ = float.MaxValue;
        for (var i = 0; i < wall.Footprint.Length; i++)
        {
            lifted[i] = wall.Footprint[i] + ext;
            minZ = MathF.Min(minZ, wall.Footprint[i].Z);
        }

        scene.StaticPolygons.Add(new TwoDStaticPolygon(new Polygon(lifted), Rgba32.White)
        {
            DrawFilled = true,
            Texture = texture,
            TextureMeters = 2f,
            SortKey = 20 + (int)(minZ * 4f),
        });
    }

    private static byte Mul(int channel, float lit) => (byte)Math.Clamp((int)(channel * lit), 0, 255);

    private static bool PointInExtrusion(Vector3 point, Vector3 a, Vector3 b, Vector3 ext)
    {
        var p = new Vector2(point.X, point.Z);
        Span<Vector2> quad =
        [
            new(a.X, a.Z),
            new(b.X, b.Z),
            new(b.X + ext.X, b.Z + ext.Z),
            new(a.X + ext.X, a.Z + ext.Z),
        ];
        return Contains(quad, p);
    }

    private static bool Contains(ReadOnlySpan<Vector2> poly, Vector2 p)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var zi = poly[i].Y;
            var zj = poly[j].Y;
            var xi = poly[i].X;
            var xj = poly[j].X;
            var intersect = zi > p.Y != zj > p.Y && p.X < (xj - xi) * (p.Y - zi) / (zj - zi + 1e-6f) + xi;
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
