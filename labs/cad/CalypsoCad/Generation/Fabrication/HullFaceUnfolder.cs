using System.Numerics;

namespace CalypsoCad.Generation.Fabrication;

internal static class HullFaceUnfolder
{
    public static List<(float X, float Y)> UnfoldQuad(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3)
    {
        var u = p1 - p0;
        var v1 = p2 - p0;
        var v2 = p3 - p0;
        var n = Vector3.Cross(u, v1);
        if (n.LengthSquared() < 1e-8f)
            n = Vector3.Cross(u, v2);
        if (n.LengthSquared() < 1e-8f)
        {
            return
            [
                (0, 0),
                (u.Length(), 0),
                (Vector2.Dot(new Vector2(v1.X, v1.Y), Vector2.Normalize(new Vector2(u.X, u.Y))), v1.Z),
                (0, 0),
            ];
        }

        n = Vector3.Normalize(n);
        var tangent = Vector3.Normalize(u);
        var bitangent = Vector3.Normalize(Vector3.Cross(n, tangent));
        float Px(Vector3 p) => Vector3.Dot(p, tangent);
        float Py(Vector3 p) => Vector3.Dot(p, bitangent);
        return [(Px(p0), Py(p0)), (Px(p1), Py(p1)), (Px(p2), Py(p2)), (Px(p3), Py(p3))];
    }

    public static float PolygonAreaM2(IReadOnlyList<(float X, float Y)> ring)
    {
        if (ring.Count < 3)
            return 0f;
        var a = 0f;
        for (var i = 0; i < ring.Count; i++)
        {
            var j = (i + 1) % ring.Count;
            a += ring[i].X * ring[j].Y - ring[j].X * ring[i].Y;
        }

        return MathF.Abs(a) * 0.5f;
    }

    public static (float MinX, float MinY, float MaxX, float MaxY) Bounds(IReadOnlyList<(float X, float Y)> ring)
    {
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var (x, y) in ring)
        {
            minX = MathF.Min(minX, x);
            minY = MathF.Min(minY, y);
            maxX = MathF.Max(maxX, x);
            maxY = MathF.Max(maxY, y);
        }

        return (minX, minY, maxX, maxY);
    }

    public static (bool Pass, float MaxDeltaM) VerifyQuadEdgeClosure(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        IReadOnlyList<(float X, float Y)> profile,
        float epsilon = 0.02f)
    {
        if (profile.Count < 4)
            return (false, float.PositiveInfinity);
        float[] d3 =
        [
            Vector3.Distance(p0, p1),
            Vector3.Distance(p1, p2),
            Vector3.Distance(p2, p3),
            Vector3.Distance(p3, p0),
        ];
        float[] d2 =
        [
            Dist(profile[0], profile[1]),
            Dist(profile[1], profile[2]),
            Dist(profile[2], profile[3]),
            Dist(profile[3], profile[0]),
        ];
        var max = 0f;
        for (var i = 0; i < 4; i++)
        {
            var tol = MathF.Max(epsilon, d3[i] * 0.03f);
            max = MathF.Max(max, MathF.Abs(d3[i] - d2[i]));
            if (MathF.Abs(d3[i] - d2[i]) > tol)
                return (false, max);
        }

        return (true, max);
    }

    private static float Dist((float X, float Y) a, (float X, float Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
