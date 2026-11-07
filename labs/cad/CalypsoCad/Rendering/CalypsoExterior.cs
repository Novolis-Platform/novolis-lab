using CalypsoCad.Export;
using CalypsoCad.Host;
using Novolis.Silk;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge.Tessellation;

namespace CalypsoCad.Rendering;

/// <summary>Silk-path exterior drawable rules + mesh emit (no Raylib).</summary>
internal static class CalypsoExterior
{
    private static readonly Color FallbackSolid = Color.FromArgb(255, 120, 150, 170);
    private static readonly Color AccentCargo = Color.FromArgb(255, 168, 128, 62);
    private static readonly Color OmlFacet = Color.FromArgb(38, 70, 120, 150);
    private static readonly Color OmlCrease = Color.FromArgb(120, 90, 160, 200);

    public static bool IsExteriorDrawable(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var kind = entity.Kind.ToLowerInvariant();
        if (kind is not ("box" or "sphere" or "cylinder" or "cone" or "wedge" or "mesh"))
            return false;
        if (TryGetBoolProp(entity, "exterior") == false)
            return false;
        if (TryGetBoolProp(entity, "interiorOnly") == true)
            return false;
        if (TryGetBoolProp(entity, "exterior") == true)
            return true;
        return IsPreservedExterior(entity);
    }

    public static bool IsPreservedExterior(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (TryGetBoolProp(entity, "exterior") == true)
            return true;
        var name = entity.Name ?? "";
        return name.StartsWith("ext-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("nacelle-", StringComparison.OrdinalIgnoreCase);
    }

    public static void DrawOne(SilkCadWorld world, CadEntity entity) =>
        DrawEntity(world, entity, cutaway: false, default, default);

    public static void DrawOne(SilkCadWorld world, CadEntity entity, Vector3 cutOrigin, Vector3 cutNormal)
    {
        if (cutNormal.LengthSquared() < 1e-8f)
        {
            DrawOne(world, entity);
            return;
        }

        DrawEntity(world, entity, cutaway: true, cutOrigin, Vector3.Normalize(cutNormal));
    }

    public static void Draw(SilkCadWorld world, CadDocument document)
    {
        foreach (var entity in document.Entities)
        {
            if (IsExteriorDrawable(entity))
                DrawOne(world, entity);
        }
    }

    public static void Draw(SilkCadWorld world, CadDocument document, Vector3 cutOrigin, Vector3 cutNormal)
    {
        cutNormal = Vector3.Normalize(cutNormal);
        foreach (var entity in document.Entities)
        {
            if (IsExteriorDrawable(entity))
                DrawOne(world, entity, cutOrigin, cutNormal);
        }
    }

    /// <summary>
    /// Soft faceted OML guide (filled plates + sparse creases) — not triangle-edge soup.
    /// Ignores reveal/cutaway so structure slides keep full-hull context.
    /// </summary>
    public static void DrawOmlWireGuide(SilkCadWorld world, CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(document);
        foreach (var entity in document.Entities)
        {
            if (!string.Equals(entity.Kind, "mesh", StringComparison.OrdinalIgnoreCase))
                continue;
            var name = entity.Name ?? "";
            if (!name.StartsWith("ext-oml", StringComparison.OrdinalIgnoreCase))
                continue;

            var mesh = CadSolidTessellator.TryTessellate(entity);
            if (mesh is null)
                continue;

            for (var i = 0; i < mesh.Indices.Count; i += 3)
            {
                var a = mesh.Vertices[mesh.Indices[i]];
                var b = mesh.Vertices[mesh.Indices[i + 1]];
                var c = mesh.Vertices[mesh.Indices[i + 2]];
                world.DrawTriangle(a, b, c, OmlFacet);

                // Crease every ~8th triangle edge — plate silhouette, not full tessellation.
                if ((i / 3) % 8 == 0)
                {
                    world.DrawLine(a, b, OmlCrease);
                    world.DrawLine(b, c, OmlCrease);
                    world.DrawLine(c, a, OmlCrease);
                }
            }
        }
    }

    private static void DrawEntity(
        SilkCadWorld world, CadEntity entity, bool cutaway, Vector3 cutOrigin, Vector3 cutNormal)
    {
        var color = ResolveColor(entity);
        switch (entity.Kind.ToLowerInvariant())
        {
            case "box" when entity.Points is { Count: >= 2 }:
            {
                var c = SvgCoords.FromArray(entity.Points[0]);
                var he = SvgCoords.FromArray(entity.Points[1]);
                if (cutaway && Culled(c, cutOrigin, cutNormal))
                    return;
                world.DrawCube(c, MathF.Abs(he.X) * 2f, MathF.Abs(he.Y) * 2f, MathF.Abs(he.Z) * 2f, color);
                break;
            }
            case "sphere" when entity.Center is not null:
            {
                var center = new Vector3(entity.Center[0], entity.Center[1], entity.Center[2]);
                if (cutaway && Culled(center, cutOrigin, cutNormal))
                    return;
                world.DrawSphere(center, entity.Radius, color);
                break;
            }
            case "mesh":
            case "cone":
            case "wedge":
            case "cylinder":
            {
                var mesh = CadSolidTessellator.TryTessellate(entity);
                if (mesh is null)
                    return;
                var edge = Color.FromArgb(255,
                    System.Math.Clamp(color.R - 40, 0, 255),
                    System.Math.Clamp(color.G - 40, 0, 255),
                    System.Math.Clamp(color.B - 40, 0, 255));
                for (var i = 0; i < mesh.Indices.Count; i += 3)
                {
                    var a = mesh.Vertices[mesh.Indices[i]];
                    var b = mesh.Vertices[mesh.Indices[i + 1]];
                    var c = mesh.Vertices[mesh.Indices[i + 2]];
                    if (cutaway)
                    {
                        if (Culled(a, cutOrigin, cutNormal)
                            && Culled(b, cutOrigin, cutNormal)
                            && Culled(c, cutOrigin, cutNormal))
                            continue;
                    }

                    world.DrawTriangle(a, b, c, color);
                    world.DrawLine(a, b, edge);
                    world.DrawLine(b, c, edge);
                    world.DrawLine(c, a, edge);
                }

                break;
            }
        }
    }

    private static bool Culled(Vector3 p, Vector3 cutOrigin, Vector3 cutNormal) =>
        Vector3.Dot(p - cutOrigin, cutNormal) > 0f;

    private static Color ResolveColor(CadEntity entity)
    {
        if (entity.Name?.StartsWith("C40", StringComparison.OrdinalIgnoreCase) == true)
            return AccentCargo;
        if (entity.Color is { Length: >= 3 } rgb)
        {
            return Color.FromArgb(
                255,
                (int)(System.Math.Clamp(rgb[0], 0f, 1f) * 255),
                (int)(System.Math.Clamp(rgb[1], 0f, 1f) * 255),
                (int)(System.Math.Clamp(rgb[2], 0f, 1f) * 255));
        }

        return FallbackSolid;
    }

    private static bool? TryGetBoolProp(CadEntity entity, string key)
    {
        if (entity.Properties is null || !entity.Properties.TryGetValue(key, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var b) => b,
            _ => null,
        };
    }
}
