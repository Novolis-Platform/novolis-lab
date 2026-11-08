using System.Text.Json;
using CalypsoCad.Blueprints;
using CalypsoCad.Blueprints.DrawingPackage;
using CalypsoCad.Hierarchy;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Drawing;

/// <summary>
/// Calypso host drawing engine: generic Novolis.Cad.Drawing projection + Calypso SVG package chrome.
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

        var hierarchy = ShipHierarchyMapper.ToHierarchy(registry);
        var core = Novolis.Cad.Drawing.UniversalDrawingEngine.Render(request, document, hierarchy);
        var resolved = new SubjectGeometryResolver.ResolveResult(
            core.Resolved.SubjectId,
            core.Resolved.Geometry,
            core.Resolved.Diagnostics,
            core.Resolved.HasDrawableGeometry);

        var html = DrawingSvgComposer.ComposeHtml(
            request,
            resolved,
            core.Manifest.Measurements,
            core.Manifest.PartRegister,
            ship,
            core.Manifest);
        return new RenderResult(html, core.Manifest, resolved);
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
