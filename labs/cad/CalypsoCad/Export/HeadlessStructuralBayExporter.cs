using System.Numerics;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Silk;

namespace CalypsoCad.Export;

/// <summary>
/// Four diagnostic stills for the two-bay structural proof (no cutaway).
/// </summary>
internal static class HeadlessStructuralBayExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = Novolis.Silk.Capture.SilkCaptureSize.UltraWidth,
        int height = Novolis.Silk.Capture.SilkCaptureSize.UltraHeight)
    {
        var outDir = CalypsoProjectPaths.StructuralBayDirectory;
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

        void BayCamera()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            // Bow-quarter on midbody bay, 150% zoom, no cut.
            renderer.SetOrbitPose(new Vector3(0f, 4.4f, loaStemToCadZ(28f, 69f)), 18f, 0.9f, 0.42f);
        }

        Add("01-members-joints", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureInspect;
            session.ShowOmlGuide = false;
            session.StatusText = "bay:members+joints";
            BayCamera();
        });

        Add("02-members-shell-transparent", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureInspect | CalypsoVisualLayers.HullShell;
            session.ShowOmlGuide = true;
            session.StatusText = "bay:members+shell-guide";
            BayCamera();
        });

        Add("03-shell-deck-complete", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Frames
                                   | CalypsoVisualLayers.DeckPlates
                                   | CalypsoVisualLayers.Longitudinals
                                   | CalypsoVisualLayers.Joinery
                                   | CalypsoVisualLayers.HullShell;
            session.ShowOmlGuide = false;
            session.StatusText = "bay:shell+deck";
            BayCamera();
        });

        Add("04-exploded-mfg", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureInspect | CalypsoVisualLayers.DeckFaces;
            session.ShowOmlGuide = false;
            session.StatusText = "bay:exploded";
            renderer.SetOrbitPose(new Vector3(0f, 5.2f, loaStemToCadZ(28f, 69f)), 26f, 1.05f, 0.52f);
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.ViewMode = CalypsoViewMode.Orbit;
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

    private static float loaStemToCadZ(float zStem, float loa) => loa * 0.5f - zStem;
}
