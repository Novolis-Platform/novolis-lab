using CalypsoCad.Geometry;

namespace CalypsoCad.Model;

/// <summary>
/// Semantic geometry source for an <see cref="CadPartKind.AnnularWebPlate"/> —
/// distinct from the solid op (<c>AnnularPlateExtrude</c> / polygon-with-holes).
/// </summary>
internal sealed record AnnularWebGeometry(
    float StationZStem,
    float WebDepth,
    float PlateThickness,
    float SkinInset,
    AnnularProfile? Profile = null) : GeometrySource;
