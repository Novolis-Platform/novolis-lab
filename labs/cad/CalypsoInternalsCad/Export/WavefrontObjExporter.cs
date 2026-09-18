using System.Globalization;
using System.Numerics;
using System.Text;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Math.Geometry;

namespace CalypsoInternalsCad.Export;

/// <summary>Tessellates a <see cref="CadDocument"/> into Wavefront OBJ + MTL (groups per entity).</summary>
internal static class WavefrontObjExporter
{
    public sealed record MeshExportStats(
        int Groups,
        int Vertices,
        int Triangles,
        int SkippedEntities);

    public static MeshExportStats Write(CadDocument cad, string objPath, string mtlPath)
    {
        ArgumentNullException.ThrowIfNull(cad);
        var mtlName = Path.GetFileName(mtlPath);
        var obj = new StringBuilder();
        var mtl = new StringBuilder();
        obj.AppendLine("# CalypsoInternalsCad — CAL-INT lock + manufacturer hull");
        obj.AppendLine($"mtllib {mtlName}");
        mtl.AppendLine("# CalypsoInternalsCad materials");

        var groups = 0;
        var verts = 0;
        var tris = 0;
        var skipped = 0;
        var vertBase = 1;

        foreach (var entity in cad.Entities)
        {
            var mesh = CadEntityTessellator.TryTessellate(entity);
            if (mesh is null || mesh.TriangleCount == 0)
            {
                skipped++;
                continue;
            }

            var safe = Sanitize(entity.Name ?? entity.Kind ?? $"entity-{groups}");
            var mat = $"mat_{safe}";
            var color = GuessColor(entity);
            mtl.AppendLine($"newmtl {mat}");
            mtl.AppendLine(CultureInfo.InvariantCulture, $"Kd {color.X:0.###} {color.Y:0.###} {color.Z:0.###}");
            mtl.AppendLine("d 1.0");
            mtl.AppendLine();

            obj.AppendLine($"g {safe}");
            obj.AppendLine($"usemtl {mat}");
            foreach (var v in mesh.Vertices)
                obj.AppendLine(CultureInfo.InvariantCulture, $"v {v.X:0.######} {v.Y:0.######} {v.Z:0.######}");

            for (var i = 0; i < mesh.Indices.Count; i += 3)
            {
                var a = vertBase + mesh.Indices[i];
                var b = vertBase + mesh.Indices[i + 1];
                var c = vertBase + mesh.Indices[i + 2];
                obj.AppendLine($"f {a} {b} {c}");
            }

            vertBase += mesh.VertexCount;
            verts += mesh.VertexCount;
            tris += mesh.TriangleCount;
            groups++;
        }

        File.WriteAllText(mtlPath, mtl.ToString());
        File.WriteAllText(objPath, obj.ToString());
        return new MeshExportStats(groups, verts, tris, skipped);
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');
        return sb.Length == 0 ? "unnamed" : sb.ToString();
    }

    private static Vector3 GuessColor(CadEntity entity)
    {
        var name = entity.Name ?? "";
        var kind = entity.Kind ?? "";
        if (name.Contains("oml", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hull", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.45f, 0.52f, 0.58f);
        if (name.Contains("iml", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.62f, 0.64f, 0.66f);
        if (name.Contains("HOLD", StringComparison.OrdinalIgnoreCase)
            || name.Contains("C40", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.35f, 0.42f, 0.32f);
        if (name.Contains("ENG", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.55f, 0.38f, 0.35f);
        if (name.Contains("BRIDGE", StringComparison.OrdinalIgnoreCase)
            || name.Contains("LOUNGE", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.40f, 0.48f, 0.55f);
        if (name.StartsWith("D3-", StringComparison.OrdinalIgnoreCase)
            || name.Contains("AIRLOCK", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.75f, 0.55f, 0.25f);
        if (string.Equals(kind, "wall", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.55f, 0.58f, 0.62f);
        if (string.Equals(kind, "space", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.48f, 0.50f, 0.54f);
        return new Vector3(0.55f, 0.55f, 0.55f);
    }
}
