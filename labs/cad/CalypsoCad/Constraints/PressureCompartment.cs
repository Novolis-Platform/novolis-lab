namespace CalypsoCad.Constraints;

internal sealed record PressureCompartment(
    string Id,
    string SpaceKey,
    bool Habitable,
    IReadOnlyList<string>? BoundaryAssemblyIds = null);
