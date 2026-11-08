using CalypsoCad.Constraints;
using CalypsoCad.Geometry;
using CalypsoCad.Model;

namespace CalypsoCad.Generation;

/// <summary>Immutable inputs available to every part generator for one ship generate pass.</summary>
internal sealed class ShipGeometryContext
{
    public required ShipDefinition Ship { get; init; }
    public required HullGeometry Hull { get; init; }
    public required ShipPartRegistry Registry { get; init; }
    public required DesignConstraints Constraints { get; init; }
    public required float Loa { get; init; }
    public required float Web { get; init; }
    public required float TBh { get; init; }
    public required float TPlate { get; init; }
    public required IReadOnlyList<float> DeckUps { get; init; }
}
