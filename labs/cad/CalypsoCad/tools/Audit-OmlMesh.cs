#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Text.Json;

var path = args.Length > 0
    ? args[0]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Novolis", "CalypsoCad", "generated", "calypso.cadjson");

using var doc = JsonDocument.Parse(File.ReadAllText(path));
foreach (var e in doc.RootElement.GetProperty("entities").EnumerateArray())
{
    if (e.GetProperty("kind").GetString() != "mesh")
        continue;
    var name = e.GetProperty("name").GetString() ?? "";
    if (!name.Contains("oml", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("iml", StringComparison.OrdinalIgnoreCase))
        continue;

    var verts = e.GetProperty("meshVertices").EnumerateArray()
        .Select(v => (X: v[0].GetSingle(), Y: v[1].GetSingle(), Z: v[2].GetSingle()))
        .ToList();
    var inds = e.GetProperty("meshIndices").EnumerateArray().Select(i => i.GetInt32()).ToList();
    Console.WriteLine($"{name}: verts={verts.Count} tris={inds.Count / 3}");
    if (verts.Count == 0)
        continue;
    Console.WriteLine($"  X [{verts.Min(v => v.X):F3} .. {verts.Max(v => v.X):F3}] mean={verts.Average(v => v.X):F3}");
    Console.WriteLine($"  Y [{verts.Min(v => v.Y):F3} .. {verts.Max(v => v.Y):F3}]");
    Console.WriteLine($"  Z [{verts.Min(v => v.Z):F3} .. {verts.Max(v => v.Z):F3}]");
    var zMax = verts.Max(v => v.Z);
    var tip = verts.Where(v => v.Z >= zMax - 0.25f).OrderBy(v => v.X).ToList();
    Console.WriteLine($"  tip band (Z≥{zMax - 0.25f:F2}) n={tip.Count} Xmean={tip.Average(v => v.X):F3}");
    foreach (var t in tip)
        Console.WriteLine($"    X={t.X,8:F3} Y={t.Y,8:F3} Z={t.Z,8:F3}");

    // Degenerate / huge aspect triangles near bow
    var bad = 0;
    for (var i = 0; i + 2 < inds.Count; i += 3)
    {
        var a = verts[inds[i]];
        var b = verts[inds[i + 1]];
        var c = verts[inds[i + 2]];
        if (Math.Max(a.Z, Math.Max(b.Z, c.Z)) < zMax - 5f)
            continue;
        float Len((float X, float Y, float Z) p, (float X, float Y, float Z) q)
        {
            var dx = p.X - q.X; var dy = p.Y - q.Y; var dz = p.Z - q.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        var e0 = Len(a, b); var e1 = Len(b, c); var e2 = Len(c, a);
        var mx = Math.Max(e0, Math.Max(e1, e2));
        var mn = Math.Max(1e-6f, Math.Min(e0, Math.Min(e1, e2)));
        if (mx / mn > 40f)
        {
            bad++;
            if (bad <= 8)
                Console.WriteLine($"  skinny tri aspect={mx / mn:F0}: A({a.X:F2},{a.Y:F2},{a.Z:F2}) B({b.X:F2},{b.Y:F2},{b.Z:F2}) C({c.X:F2},{c.Y:F2},{c.Z:F2})");
        }
    }
    Console.WriteLine($"  skinny bow tris: {bad}");
}
