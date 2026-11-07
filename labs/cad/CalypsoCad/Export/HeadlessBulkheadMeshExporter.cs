using System.Numerics;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>
/// Acceptance stills for polygonal heavy-bulkhead solids (post box→extrude fix).
/// </summary>
internal static class HeadlessBulkheadMeshExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = SilkCaptureSize.UltraWidth,
        int height = SilkCaptureSize.UltraHeight)
    {
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "bulkhead-mesh");
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.EnumerateFiles(outDir, "*.png"))
            File.Delete(old);

        var previousLayers = session.VisualLayers;
        var previousWire = session.WireMeshMode;
        var previousView = session.ViewMode;
        var previousOml = session.ShowOmlGuide;
        var previousDeck = session.DeckFilter;
        var previousStatus = session.StatusText;

        var jobs = new List<(string Path, Action Prepare)>();
        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        void ShipCamera()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.5f);
        }

        // 1 — Transparent hull (OML guide) + solid bulkheads.
        Add("01-hull-bulkheads", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "bh-mesh: hull guide + solid plates";
            ShipCamera();
        });

        // 2 — Bulkheads only, solid shaded.
        Add("02-bulkheads-solid", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = false;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "bh-mesh: solid shaded";
            ShipCamera();
        });

        // 3 — Boundary edges only (no internal triangulation).
        Add("03-bulkheads-edges", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = false;
            session.WireMeshMode = CalypsoWireMeshMode.Wire;
            session.StatusText = "bh-edges: perimeter only";
            ShipCamera();
        });

        // 4 — Transverse section: ENG-FORE BH vs IML ring.
        Add("04-section-eng-fore", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            var zStem = 41f;
            var zCad = 69f * 0.5f - zStem;
            renderer.SetOrbitPose(new Vector3(0f, 5.5f, zCad), 22f, MathF.PI * 0.5f, 0.05f);
            session.StatusText = $"bh-mesh:section ENG-FORE zStem={zStem:0.##} vs IML";
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
            session.StatusText = previousStatus;
        }
    }
}
