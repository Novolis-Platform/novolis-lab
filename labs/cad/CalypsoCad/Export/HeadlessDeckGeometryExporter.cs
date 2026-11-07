using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Silk;

namespace CalypsoCad.Export;

/// <summary>
/// Whole-ship deck LOD stills — faces / peri+girders / manufacturing cores (fixed camera).
/// DeckFilter=0 so stacked DK−1/0/+1 grids do not read as one overlapping soup.
/// </summary>
internal static class HeadlessDeckGeometryExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = Novolis.Silk.Capture.SilkCaptureSize.UltraWidth,
        int height = Novolis.Silk.Capture.SilkCaptureSize.UltraHeight)
    {
        var outDir = CalypsoProjectPaths.DeckGeometryDirectory;
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.EnumerateFiles(outDir, "*.png"))
            File.Delete(old);

        var previousLayers = session.VisualLayers;
        var previousWire = session.WireMeshMode;
        var previousView = session.ViewMode;
        var previousOml = session.ShowOmlGuide;
        var previousDeck = session.DeckFilter;

        var jobs = new List<(string Path, Action Prepare)>();
        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        void ShipCamera()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            // Single deck elevation for coherent panel read (still full LOA).
            session.DeckFilter = 0;
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.5f);
        }

        Add("01-deck-faces", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.DeckFaces;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:faces-dk0";
            ShipCamera();
        });

        Add("02-deck-peri-girders", () =>
        {
            // Perimeters + primary longitudinal girders (not stringers/skin).
            session.VisualLayers = CalypsoVisualLayers.DeckPerimeters | CalypsoVisualLayers.Longitudinals;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:peri+girders-dk0";
            ShipCamera();
        });

        Add("03-deck-cores-mfg", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.DeckCores;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:cores-mfg-dk0";
            ShipCamera();
        });

        Add("04-structure-inspect", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureInspect;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:structure-inspect-dk0";
            ShipCamera();
        });

        try
        {
            return SilkHeadlessCapture.CaptureSequence(
                width,
                height,
                jobs,
                frame => renderer.DrawFrame(frame, 0f));
        }
        finally
        {
            session.VisualLayers = previousLayers;
            session.WireMeshMode = previousWire;
            session.ViewMode = previousView;
            session.ShowOmlGuide = previousOml;
            session.DeckFilter = previousDeck;
        }
    }
}
