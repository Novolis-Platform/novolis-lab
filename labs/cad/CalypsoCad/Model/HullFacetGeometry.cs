namespace CalypsoCad.Model;

internal sealed record HullFacetGeometry(int FacetIndex, float FromStation, float ToStation) : GeometrySource;
