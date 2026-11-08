using CalypsoCad.Hierarchy;
using CalypsoCad.Model;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Drawing;

/// <summary>
/// Calypso adapter: resolves a semantic hierarchy node via <see cref="ShipPartRegistry"/>
/// into drawable geometry using the generic Novolis.Cad.Drawing resolver.
/// </summary>
internal static class SubjectGeometryResolver
{
    public sealed record ResolveResult(
        string SubjectId,
        IReadOnlyList<DrawableGeometry> Geometry,
        IReadOnlyList<string> Diagnostics,
        bool HasDrawableGeometry);

    public static ResolveResult Resolve(
        string subjectId,
        ShipPartRegistry registry,
        CadDocument document)
    {
        var hierarchy = ShipHierarchyMapper.ToHierarchy(registry);
        var core = Novolis.Cad.Drawing.SubjectGeometryResolver.Resolve(subjectId, hierarchy, document);
        return new ResolveResult(core.SubjectId, core.Geometry, core.Diagnostics, core.HasDrawableGeometry);
    }
}
