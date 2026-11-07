using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;
using Novolis.Silk;

namespace CalypsoCad.Export;

/// <summary>
/// Piece-by-piece structural bay Tests A–E PNGs under docs/renders/structural-bay/.
/// </summary>
internal static class HeadlessStructuralBayExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string tests,
        int width = 1920,
        int height = 1080)
    {
        var outDir = CalypsoProjectPaths.StructuralBayDirectory;
        Directory.CreateDirectory(outDir);

        var wanted = ParseTests(tests);
        var previousLayers = session.VisualLayers;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousReveal = session.RevealEntityIds;
        var previousStatus = session.StatusText;
        var previousGuide = session.ShowOmlGuide;

        // Frame the midbody bay (z≈24–28 → CAD Z ≈ +8.5 at LOA 69), not the full 69 m hull.
        renderer.SetOrbitPose(new System.Numerics.Vector3(0f, 5.5f, 8.5f), distance: 22f, yaw: 0.95f, pitch: 0.42f);

        var jobs = new List<(string Path, Action Prepare)>();
        foreach (var t in wanted)
        {
            var fileName = $"bay-test-{t}.png";
            var path = Path.Combine(outDir, fileName);
            var test = t;
            jobs.Add((path, () =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ShowOmlGuide = false;
                session.DeckFilter = null;
                session.RevealEntityIds = StructuralBayEmitter.EntityIdsForTest(session.Document, test);
                session.VisualLayers = test switch
                {
                    'A' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.Longitudinals,
                    'B' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryStructure,
                    'C' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryPlusDecks,
                    'D' => CalypsoVisualLayers.PrimaryPlusDecks, // shell hidden
                    'E' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryPlusDecks,
                    _ => CalypsoVisualLayers.All,
                };
                session.StatusText = $"structural-bay:Test {test}";
            }));
        }

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
            session.ViewMode = previousView;
            session.WireMeshMode = previousWire;
            session.RevealEntityIds = previousReveal;
            session.StatusText = previousStatus;
            session.ShowOmlGuide = previousGuide;
        }
    }

    private static List<char> ParseTests(string tests)
    {
        if (string.IsNullOrWhiteSpace(tests)
            || string.Equals(tests, "all", StringComparison.OrdinalIgnoreCase))
            return ['A', 'B', 'C', 'D', 'E'];

        var list = new List<char>();
        foreach (var c in tests.ToUpperInvariant())
        {
            if (c is >= 'A' and <= 'E' && !list.Contains(c))
                list.Add(c);
        }

        return list.Count > 0 ? list : ['A', 'B', 'C', 'D', 'E'];
    }
}
