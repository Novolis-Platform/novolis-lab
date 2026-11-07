using System.Numerics;

namespace ArtillerySimulator.Game;

/// <summary>
/// Single E/W policy for Kristiansand.
///
/// World X always increases with longitude (east). That mapping never flips.
/// If a PDF/NDH raster is mirrored (column 0 = geographic east), only
/// <see cref="WorldToDataU"/> flips when indexing pixels / heightfield columns.
///
/// Separately: RH <see cref="Matrix4x4.CreateLookAt"/> with Y-up / Z-north puts
/// geographic east on screen-left when facing north. <see cref="CreateMapView"/>
/// flips view-space X so the compass matches a map (east right when looking north).
/// Never "fix" that by flipping lon→world — that parks the gun on the wrong coast.
/// </summary>
internal static class KristiansandGeoref
{
    /// <summary>
    /// Set true only when raster column 0 is geographic east.
    /// Current Norgeskart PDF + NDH export: column 0 = west → false.
    /// Probe: east-of-fort has more open water than west (Odderøya).
    /// </summary>
    public const bool RasterColumn0IsEast = false;

    /// <summary>Lon → world X (east-positive). Never mirrors.</summary>
    public static float LonToWorldX(double longitudeDegrees, double westLon, double eastLon, float eastMeters) =>
        (float)((longitudeDegrees - westLon) / (eastLon - westLon) * eastMeters);

    /// <summary>Lat → world Z (north-positive). Never mirrors.</summary>
    public static float LatToWorldZ(double latitudeDegrees, double southLat, double northLat, float northMeters) =>
        (float)((latitudeDegrees - southLat) / (northLat - southLat) * northMeters);

    /// <summary>
    /// World X → unit raster U in [0,1] (0 = left column of the PDF/heightfield).
    /// This is the only place an E/W raster mirror is allowed.
    /// </summary>
    public static float WorldToDataU(float worldXMeters, float eastMeters)
    {
        var u = worldXMeters / eastMeters;
        return RasterColumn0IsEast ? 1f - u : u;
    }

    /// <summary>World X → data-space meters along the raster (left = 0).</summary>
    public static float WorldToDataX(float worldXMeters, float eastMeters) =>
        WorldToDataU(worldXMeters, eastMeters) * eastMeters;

    /// <summary>Approx EGM undulation (m) near Kristiansand — Cesium z is ellipsoidal, NDH is orthometric.</summary>
    public const float GeoidUndulationMeters = 40.2f;

    /// <summary>
    /// Look-at view with map-conventional screen east/west (compensates RH Y-up Z-north).
    /// Do <b>not</b> use this when matching a Kommunekart Cesium dump looking off-north —
    /// the X flip swaps N/S on screen when facing east/west.
    /// </summary>
    public static Matrix4x4 CreateMapView(Vector3 eye, Vector3 target, Vector3 up) =>
        Matrix4x4.CreateLookAt(eye, target, up) * Matrix4x4.CreateScale(-1f, 1f, 1f);

    /// <summary>Plain RH look-at — same basis as Cesium for Kommunekart head/pitch dumps.</summary>
    public static Matrix4x4 CreateCesiumMatchedView(Vector3 eye, Vector3 target, Vector3 up) =>
        Matrix4x4.CreateLookAt(eye, target, up);
}
