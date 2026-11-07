using System.Numerics;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>
/// Hull-first acceptance stills: hull → frames → heavy BHs → representative transverse stations.
/// </summary>
internal static class HeadlessHullFirstExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = SilkCaptureSize.UltraWidth,
        int height = SilkCaptureSize.UltraHeight)
    {
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "hull-first");
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
            session.DeckFilter = null;
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.5f);
        }

        // A — Hull only (finished exterior).
        Add("A-hull-only", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.HullShell;
            session.ShowOmlGuide = false;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "hull-first:A hull only";
            ShipCamera();
        });

        // B — Transparent hull + ordinary frames.
        Add("B-hull-frames", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Frames;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "hull-first:B hull+frames";
            ShipCamera();
        });

        // C — Hull + frames + heavy BHs (primary acceptance).
        Add("C-hull-frames-bulkheads", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "hull-first:C frames+BH · BOW@13 CROSS@19/23 CREW@30.28 ENG@41/49.25";
            ShipCamera();
        });

        // D — Transverse station close-ups (from generated 3D).
        void SectionAt(string file, float zStem, string label, CalypsoVisualLayers layers)
        {
            Add(file, () =>
            {
                session.VisualLayers = layers;
                session.ShowOmlGuide = true;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                var zCad = 69f * 0.5f - zStem;
                // Looking from starboard toward CL at this station.
                renderer.SetOrbitPose(new Vector3(0f, 5.5f, zCad), 28f, MathF.PI * 0.5f, 0.08f);
                session.StatusText = $"hull-first:D {label} zStem={zStem:0.##}";
            });
        }

        SectionAt("D1-section-hold-frame", 56f, "HOLD perimeter frame", CalypsoVisualLayers.Frames);
        SectionAt("D2-section-eng-bh", 41f, "ENG-FORE heavy BH", CalypsoVisualLayers.Bulkheads);
        SectionAt("D3-section-accommodation", 28f, "accommodation ordinary frame", CalypsoVisualLayers.Frames);
        SectionAt("D4-section-bow-frame", 8f, "forward tapered frame", CalypsoVisualLayers.Frames);
        SectionAt("D5-section-bow-bh", 13f, "BOW-COLLISION heavy BH", CalypsoVisualLayers.Bulkheads);

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
