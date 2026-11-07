using System.Numerics;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Silk;

namespace CalypsoCad.Export;

/// <summary>
/// Whole-ship deck footprint inspection stills (fixed camera for before/after).
/// </summary>
internal static class HeadlessDeckGeometryExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = 3840,
        int height = 2160)
    {
        var outDir = CalypsoProjectPaths.DeckGeometryDirectory;
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.EnumerateFiles(outDir, "*.png"))
            File.Delete(old);

        var previousLayers = session.VisualLayers;
        var previousWire = session.WireMeshMode;
        var previousView = session.ViewMode;
        var previousOml = session.ShowOmlGuide;

        var jobs = new List<(string Path, Action Prepare)>();
        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        void ShipCamera()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            // Bow-quarter whole ship, 150% zoom — same language as critical-structure stills.
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.5f);
        }

        Add("01-deck-faces", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.DeckFaces;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:faces";
            ShipCamera();
        });

        Add("02-deck-cores", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.DeckCores;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:cores";
            ShipCamera();
        });

        Add("03-deck-transparent-hull", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.DeckFaces;
            session.ShowOmlGuide = true;
            session.StatusText = "deck:faces+oml-guide";
            ShipCamera();
        });

        Add("04-structure-inspect", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureInspect;
            session.ShowOmlGuide = false;
            session.StatusText = "deck:structure-inspect";
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
        }
    }
}
