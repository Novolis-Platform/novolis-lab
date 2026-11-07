using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator.Game;

/// <summary>
/// Kristiansand E/W policy on top of <see cref="MapRasterBasis"/>.
/// </summary>
internal static class KristiansandGeoref
{
    /// <summary>
    /// Set true only when raster column 0 is geographic east.
    /// Current Norgeskart PDF + NDH export: column 0 = west → false.
    /// </summary>
    public const bool RasterColumn0IsEast = false;

    /// <summary>Approx EGM undulation (m) near Kristiansand — Cesium z is ellipsoidal, NDH is orthometric.</summary>
    public const float GeoidUndulationMeters = 40.2f;

    public static float LonToWorldX(double longitudeDegrees, double westLon, double eastLon, float eastMeters) =>
        MapRasterBasis.LonToWorldX(longitudeDegrees, westLon, eastLon, eastMeters);

    public static float LatToWorldZ(double latitudeDegrees, double southLat, double northLat, float northMeters) =>
        MapRasterBasis.LatToWorldZ(latitudeDegrees, southLat, northLat, northMeters);

    public static float WorldToDataU(float worldXMeters, float eastMeters) =>
        MapRasterBasis.WorldToDataU(worldXMeters, eastMeters, RasterColumn0IsEast);

    public static float WorldToDataX(float worldXMeters, float eastMeters) =>
        MapRasterBasis.WorldToDataX(worldXMeters, eastMeters, RasterColumn0IsEast);

    /// <summary>
    /// Look-at with map-conventional screen east/west (compensates RH Y-up Z-north).
    /// Skip when matching a Kommunekart Cesium dump looking off-north.
    /// </summary>
    public static Matrix4x4 CreateMapView(Vector3 eye, Vector3 target, Vector3 up) =>
        Matrix4x4.CreateLookAt(eye, target, up) * Matrix4x4.CreateScale(-1f, 1f, 1f);

    /// <summary>Plain RH look-at — Cesium-matched for Kommunekart head/pitch dumps.</summary>
    public static Matrix4x4 CreateCesiumMatchedView(Vector3 eye, Vector3 target, Vector3 up) =>
        Matrix4x4.CreateLookAt(eye, target, up);
}
