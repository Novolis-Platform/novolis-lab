namespace CalypsoCad.Drawing;

/// <summary>
/// CAD world ↔ manufacturer lock stations.
/// CAD: +X starboard, +Y up, +Z toward bow from midships (<c>Z = LOA/2 − zFromStem</c>).
/// Lock drawings: <c>zFromStem</c> 0 at stem/bow … LOA at stern; ISO 128-15 stern left / bow right.
/// </summary>
internal static class CadShipCoords
{
    public static float ZStemFromWorld(float worldZ, float loa) => loa * 0.5f - worldZ;

    public static float WorldZFromStem(float zFromStem, float loa) => loa * 0.5f - zFromStem;

    public static float ClampZStem(float zFromStem, float loa) =>
        Math.Clamp(zFromStem, 0f, Math.Max(0f, loa));
}
