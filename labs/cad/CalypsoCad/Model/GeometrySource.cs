namespace CalypsoCad.Model;

/// <summary>Typed description of how a part obtains its geometry — not the mesh itself.</summary>
internal abstract record GeometrySource;

internal sealed record HullSectionGeometry(float Station, float Inset) : GeometrySource;

internal sealed record HullFacetGeometry(int FacetIndex, float FromStation, float ToStation) : GeometrySource;

internal sealed record HullSurfacePathGeometry(int FacetIndex, float FromStation, float ToStation) : GeometrySource;

internal sealed record DeckFootprintGeometry(float Elevation, float FromStation, float ToStation) : GeometrySource;

internal sealed record ExplicitProfileGeometry(string ProfileKey) : GeometrySource;
