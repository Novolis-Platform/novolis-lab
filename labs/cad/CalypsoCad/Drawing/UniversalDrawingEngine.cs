using System.Text.Json;
using CalypsoCad.Blueprints;
using CalypsoCad.Blueprints.DrawingPackage;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Drawing;

/// <summary>
/// Hierarchy-driven drawing engine: subject → geometry → project/section → compose.
/// One engine for interactive preview and headless package generation.
/// </summary>
internal static class UniversalDrawingEngine
{
    public sealed record RenderResult(
        string Html,
        DrawingManifest Manifest,
        SubjectGeometryResolver.ResolveResult Resolved);

    public static RenderResult Render(
        DrawingRequest request,
        CadDocument document,
        ShipPartRegistry? registry = null)
    {
        registry ??= EnsureRegistry(document);
        CadJsonStructureQuery.EnsureHullGeometry(document);
        var ship = PackageDrawingChrome.ReadShip(document);

        var resolved = SubjectGeometryResolver.Resolve(request.SubjectId, registry, document);
        var measurements = DrawingMeasurements.ComputeOverall(resolved.Geometry, request.Dimensions);
        var register = PartRegisterBuilder.Build(request.SubjectId, registry);

        var unsupported = resolved.Diagnostics
            .Where(d => d.Contains("Unsupported", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ok = resolved.HasDrawableGeometry
                 || request.Views.All(v => v.Kind == DrawingViewKind.Assembly);
        if (unsupported.Count > 0 && !resolved.HasDrawableGeometry)
            ok = false;

        var manifest = new DrawingManifest
        {
            SubjectId = request.SubjectId,
            DrawingNumber = request.DrawingNumber,
            ViewIds = request.Views.Select(v => v.Id).ToList(),
            GeometrySourceIds = resolved.Geometry.Select(g => g.SourceEntityId).Distinct().ToList(),
            Measurements = measurements,
            Diagnostics = resolved.Diagnostics,
            PartRegister = register,
            Ok = ok && unsupported.Count == 0,
        };

        var html = DrawingSvgComposer.ComposeHtml(
            request, resolved, measurements, register, ship, manifest);
        return new RenderResult(html, manifest, resolved);
    }

    public static string RenderManifestJson(DrawingManifest manifest) =>
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

    private static ShipPartRegistry EnsureRegistry(CadDocument document)
    {
        CadDocumentAdapter.LoadFromDocument(document);
        var registry = CadDocumentAdapter.Active;
        ShipHierarchyBuilder.EnsureCanonicalHierarchy(registry, document);
        return registry;
    }
}
