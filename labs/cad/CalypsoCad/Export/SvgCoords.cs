using System.Numerics;
using CalypsoCad.Structure;

namespace CalypsoCad.Export;

/// <summary>Calypso SVG plan sheet ↔ world meters (blueprint / legacy SVG coords).</summary>
internal static class SvgCoords
{
    /// <summary>Meters per SVG unit (Rev G hull beam ~196 units ≈ 20 m).</summary>
    public const float Scale = 0.1f;

    public const float SvgCenterX = 160f;
    public const float SvgMidY = 403f;

    /// <summary>SVG plan (+Y down) → Novolis (+X starboard, +Z forward/bow, +Y up).</summary>
    public static Vector3 ToWorld(float svgX, float svgY, float deckY = 0f) =>
        new((svgX - SvgCenterX) * Scale, deckY, (SvgMidY - svgY) * Scale);

    public static float[] ToArray(Vector3 v) => CadVec.ToArray(v);

    public static Vector3 FromArray(float[] a) => CadVec.FromArray(a);
}
