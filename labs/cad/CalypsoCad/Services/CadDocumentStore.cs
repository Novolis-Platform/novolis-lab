using System.Numerics;
using System.Text.Json;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Services;

internal static class SvgCoords
{
    /// <summary>Meters per SVG unit (Rev G hull beam ~196 units ≈ 20 m).</summary>
    public const float Scale = 0.1f;

    public const float SvgCenterX = 160f;
    public const float SvgMidY = 403f;

    /// <summary>SVG plan (+Y down) → Novolis (+X starboard, +Z forward/bow, +Y up).</summary>
    public static Vector3 ToWorld(float svgX, float svgY, float deckY = 0f) =>
        new((svgX - SvgCenterX) * Scale, deckY, (SvgMidY - svgY) * Scale);

    public static float[] ToArray(Vector3 v) => [v.X, v.Y, v.Z];

    public static Vector3 FromArray(float[] a) =>
        a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.Zero;
}

internal static class CadDocumentStore
{
    public static void WriteAll(string directory, CadLayersDocument layers, CadShapesDocument shapes, CadDocument cad)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "calypso.cadlayers.json"), JsonSerializer.Serialize(layers, CadJson.Options));
        File.WriteAllText(Path.Combine(directory, "calypso.cadshapejson"), JsonSerializer.Serialize(shapes, CadJson.Options));
        File.WriteAllText(Path.Combine(directory, "calypso.cadjson"), JsonSerializer.Serialize(cad, CadJson.Options));
    }

    public static (CadLayersDocument Layers, CadShapesDocument Shapes, CadDocument Cad) ReadAll(string directory)
    {
        var layers = JsonSerializer.Deserialize<CadLayersDocument>(
            File.ReadAllText(Path.Combine(directory, "calypso.cadlayers.json")), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to read calypso.cadlayers.json");
        var shapes = JsonSerializer.Deserialize<CadShapesDocument>(
            File.ReadAllText(Path.Combine(directory, "calypso.cadshapejson")), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to read calypso.cadshapejson");
        var cad = JsonSerializer.Deserialize<CadDocument>(
            File.ReadAllText(Path.Combine(directory, "calypso.cadjson")), CadJson.Options)
            ?? throw new InvalidOperationException("Failed to read calypso.cadjson");
        return (layers, shapes, cad);
    }
}
