using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Novolis.Governance.InspectNorgeskartPdf;

internal static class UtmProbe
{
    public static void Print()
    {
        var wgs84 = GeographicCoordinateSystem.WGS84;
        var utm = ProjectedCoordinateSystem.WGS84_UTM(33, true);
        var toGeo = new CoordinateTransformationFactory().CreateFromCoordinateSystems(utm, wgs84);
        const double ce = 90315.74;
        const double cn = 6471192.41;
        const double halfE = 13_500;
        const double halfN = 9_500;
        foreach (var (e, n, label) in new (double, double, string)[]
        {
            (ce - halfE, cn - halfN, "sw"),
            (ce + halfE, cn - halfN, "se"),
            (ce - halfE, cn + halfN, "nw"),
            (ce + halfE, cn + halfN, "ne"),
            (ce, cn, "center"),
        })
        {
            var p = toGeo.MathTransform.Transform(new[] { e, n });
            Console.WriteLine($"{label}: lon={p[0]:F5} lat={p[1]:F5}");
        }
    }
}
