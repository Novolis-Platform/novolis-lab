using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Raylib;

namespace CalypsoCad.Services;

/// <summary>Calypso tour helpers on top of <see cref="CadViewportExporter"/>.</summary>
internal static class ViewportPngExporter
{
    public static string ExportsDirectory(string generatedRoot) =>
        CadViewportExporter.ExportsDirectory(generatedRoot);

    public static string AllocateTourPath(string generatedRoot, string kind) =>
        CadViewportExporter.AllocateTourPath(generatedRoot, kind);

    public static string AllocatePath(string generatedRoot, string kind) =>
        CadViewportExporter.AllocatePath(generatedRoot, kind);

    public static void PurgeLegacyHeadlessExports(string generatedRoot)
    {
        var dir = ExportsDirectory(generatedRoot);
        if (!Directory.Exists(dir))
            return;
        foreach (var file in Directory.EnumerateFiles(dir, "*-headless-*.png"))
        {
            try { File.Delete(file); }
            catch { /* best-effort cleanup */ }
        }
    }

    public static Task<string?> ExportCurrentAsync(
        RaylibHostControl host,
        string path,
        CancellationToken cancellationToken = default) =>
        CadViewportExporter.ExportCurrentPreviewPngAsync(host, path, cancellationToken);

    public static async Task<IReadOnlyList<string>> ExportViewsAsync(
        RaylibHostControl host,
        CalypsoSession session,
        CalypsoRenderer renderer,
        string generatedRoot,
        CancellationToken cancellationToken = default)
    {
        var previous = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var views = new List<(string Kind, Action SetView)>
        {
            ("plan", () =>
            {
                session.ViewMode = CalypsoViewMode.Plan;
                session.WireMeshMode = CalypsoWireMeshMode.None;
            }),
            ("orbit-bow-quarter", () =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                renderer.ApplyOrbitPreset("bow-quarter");
            }),
            ("interior-solid", () =>
            {
                session.ViewMode = CalypsoViewMode.Interior;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                renderer.SyncInteriorFromSelection();
            }),
            ("interior-cutaway", () =>
            {
                session.ViewMode = CalypsoViewMode.Interior;
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                renderer.SyncInteriorFromSelection();
            }),
        };

        try
        {
            return await CadViewportExporter.ExportViewTourAsync(host, views, generatedRoot, cancellationToken)
                .ConfigureAwait(true);
        }
        finally
        {
            session.ViewMode = previous;
            session.WireMeshMode = previousWire;
            if (previous == CalypsoViewMode.Interior)
                renderer.SyncInteriorFromSelection();
        }
    }
}
