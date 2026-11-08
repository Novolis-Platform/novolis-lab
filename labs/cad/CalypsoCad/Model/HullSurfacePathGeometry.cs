namespace CalypsoCad.Model;

internal sealed record HullSurfacePathGeometry(int FacetIndex, float FromStation, float ToStation) : GeometrySource;
