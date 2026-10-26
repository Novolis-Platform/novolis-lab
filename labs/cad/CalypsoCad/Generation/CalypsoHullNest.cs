using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>Pulls interior space footprints inside the manufacturer IML octagon (fore taper).</summary>
internal static class CalypsoHullNest
{
    private const float Inset = CalypsoLockGenerator.TShellPublic + 0.04f;

    public static void RefineAllSpaceFootprints(List<CadEntity> entities, float loa)
    {
        foreach (var space in entities.Where(e => string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (space.Points is not { Count: >= 3 })
                continue;

            var floor = space.Points[0].Length > 1 ? space.Points[0][1] : 0f;
            var ceil = floor + MathF.Max(0.5f, space.Height);
            var moved = false;
            var ring = space.Points.Select(p => ClipVertex(p, loa, floor, ceil, ref moved)).ToList();
            ring = DensifyAndClip(ring, loa, floor, ceil, ref moved);
            if (ring.Count >= 3)
                space.Points = ring;
            if (moved)
            {
                space.Properties ??= new Dictionary<string, System.Text.Json.JsonElement>();
                space.Properties["clippedToManufacturerHull"] =
                    System.Text.Json.JsonSerializer.SerializeToElement(true);
            }
        }
    }

    private static float[] ClipVertex(float[] p, float loa, float floor, float ceil, ref bool moved)
    {
        if (p.Length < 3)
            return p;
        var y = p[0];
        var up = p[1];
        var zStem = loa * 0.5f - p[2];
        var (cy, cUp, cStem) = NestPoint(y, up, zStem, floor, ceil);
        if (MathF.Abs(cy - y) > 1e-3f || MathF.Abs(cUp - up) > 1e-3f || MathF.Abs(cStem - zStem) > 1e-3f)
            moved = true;
        var w = CalypsoLockGenerator.LockToWorldPublic(cy, cUp, cStem, loa);
        return [w.X, cUp, w.Z];
    }

    private static List<float[]> DensifyAndClip(List<float[]> ring, float loa, float floor, float ceil, ref bool moved)
    {
        const int passes = 2;
        for (var pass = 0; pass < passes; pass++)
        {
            var next = new List<float[]>(ring.Count * 2);
            for (var i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                next.Add(a);
                if (a.Length < 3 || b.Length < 3)
                    continue;
                var mid = new float[]
                {
                    (a[0] + b[0]) * 0.5f,
                    (a[1] + b[1]) * 0.5f,
                    (a[2] + b[2]) * 0.5f,
                };
                var zStem = loa * 0.5f - mid[2];
                if (!CalypsoLockGenerator.InsideHullPublic(mid[0], floor, zStem, Inset)
                    || !CalypsoLockGenerator.InsideHullPublic(mid[0], ceil, zStem, Inset))
                {
                    var clipped = ClipVertex(mid, loa, floor, ceil, ref moved);
                    next.Add(clipped);
                }
            }

            ring = next;
        }

        return ring;
    }

    private static (float Y, float Up, float ZStem) NestPoint(float y, float up, float zStem, float floor, float ceil)
    {
        zStem = MathF.Max(0.05f, MathF.Min(zStem, 69f - 0.05f));
        up = MathF.Max(floor, MathF.Min(up, ceil));
        for (var n = 0; n < 16; n++)
        {
            var limit = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, floor, Inset);
            y = Math.Clamp(y, -limit, limit);
            var maxUp = CalypsoLockGenerator.MaxUpInsidePublic(zStem, y, Inset);
            var minUp = CalypsoLockGenerator.MinUpInsidePublic(zStem, y, Inset);
            up = Math.Clamp(up, MathF.Max(floor, minUp), MathF.Min(ceil, maxUp));
            if (CalypsoLockGenerator.InsideHullPublic(y, floor, zStem, Inset)
                && CalypsoLockGenerator.InsideHullPublic(y, ceil, zStem, Inset))
                return (y, up, zStem);
            y *= 0.9f;
            if (zStem < 18f)
                zStem += 0.08f;
        }

        return (y, up, zStem);
    }
}
