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

        var jobs = new List<(string Path, Action Prepare)>();
        foreach (var t in wanted)
        {
            var fileName = t switch
            {
                'H' => "bay-test-HOLD-shell.png",
                'N' => "bay-test-HOLD-noshell.png",
                _ => $"bay-test-{t}.png",
            };
            var path = Path.Combine(outDir, fileName);
            var test = t;
            jobs.Add((path, () =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ShowOmlGuide = false;
                session.DeckFilter = null;
                var holdFocus = test is 'H' or 'N';
                // Midbody ~Z+8.5; HOLD bay z52–56 → CAD Z ≈ 34.5−54 = −19.5
                if (holdFocus)
                    renderer.SetOrbitPose(new System.Numerics.Vector3(0f, 5.5f, -19.5f), 24f, 0.95f, 0.42f);
                else
                    renderer.SetOrbitPose(new System.Numerics.Vector3(0f, 5.5f, 8.5f), 22f, 0.95f, 0.42f);

                if (holdFocus)
                {
                    var ids = new HashSet<Guid>();
                    foreach (var e in session.Document.Entities ?? [])
                    {
                        if (StructuralBayEmitter.IsHoldBayEntity(e)
                            || (StructuralBayEmitter.IsShellEntity(e) && StructuralBayEmitter.IsHoldBayEntity(e)))
                            ids.Add(e.Id);
                        // Shell panels named *-z52
                        if ((e.Name?.Contains("-z52", StringComparison.OrdinalIgnoreCase) == true)
                            || (e.Name?.Contains("F52", StringComparison.OrdinalIgnoreCase) == true)
                            || (e.Name?.Contains("F56", StringComparison.OrdinalIgnoreCase) == true)
                            || (e.Name?.Contains("-holdfloor", StringComparison.OrdinalIgnoreCase) == true))
                            ids.Add(e.Id);
                    }

                    // Also include shell/stiffeners/girders/decks tagged stationZ in hold span.
                    foreach (var e in session.Document.Entities ?? [])
                    {
                        if (e.Properties is null
                            || !e.Properties.TryGetValue("stationZ", out var sz)
                            || sz.ValueKind != System.Text.Json.JsonValueKind.Number)
                            continue;
                        var z = (float)sz.GetDouble();
                        if (z >= StructuralBayEmitter.HoldBayZ0 - 0.6f && z <= StructuralBayEmitter.HoldBayZ1 + 0.6f)
                            ids.Add(e.Id);
                    }

                    session.RevealEntityIds = ids;
                    session.VisualLayers = test == 'H'
                        ? CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryPlusDecks
                        : CalypsoVisualLayers.PrimaryPlusDecks;
                }
                else
                {
                    session.RevealEntityIds = StructuralBayEmitter.EntityIdsForTest(session.Document, test);
                    session.VisualLayers = test switch
                    {
                        'A' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.Longitudinals,
                        'B' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryStructure,
                        'C' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryPlusDecks,
                        'D' => CalypsoVisualLayers.PrimaryPlusDecks,
                        'E' => CalypsoVisualLayers.HullShell | CalypsoVisualLayers.PrimaryPlusDecks,
                        _ => CalypsoVisualLayers.All,
                    };
                }

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
            return ['A', 'B', 'C', 'D', 'E', 'H', 'N'];

        var list = new List<char>();
        foreach (var c in tests.ToUpperInvariant())
        {
            if ((c is >= 'A' and <= 'E' || c is 'H' or 'N') && !list.Contains(c))
                list.Add(c);
        }

        return list.Count > 0 ? list : ['A', 'B', 'C', 'D', 'E', 'H', 'N'];
    }
}
