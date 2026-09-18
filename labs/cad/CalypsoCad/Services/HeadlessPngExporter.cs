using Novolis.Raylib.Audio;
using Novolis.Raylib.Debug;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Logging;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Timing;
using Novolis.Raylib.Windowing;

namespace CalypsoCad.Services;

/// <summary>
/// Headless Raylib PNG export via hidden window + <see cref="ScreenFramebufferCapture.TryExportFramebufferToPng"/>.
/// Writes stable <c>{kind}.png</c> names (overwrite) and purges legacy <c>*-headless-*</c> dumps.
/// </summary>
internal static class HeadlessPngExporter
{
    public static IReadOnlyList<string> ExportViews(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string generatedRoot,
        int width = 2560,
        int height = 1440)
    {
        var exportsDir = ViewportPngExporter.ExportsDirectory(generatedRoot);
        Directory.CreateDirectory(exportsDir);
        ViewportPngExporter.PurgeLegacyHeadlessExports(generatedRoot);

        RaylibDebug.Start();
        Logger.SetTraceLogLevel(TraceLogLevel.Warning);
        AudioDevice.Init();
        try
        {
            Window.SetConfigFlags(WindowStateFlags.Hidden);
            Window.Init(width, height, "CalypsoCad headless export");
            if (!Window.IsReady())
                throw new InvalidOperationException("Raylib window failed to initialize (display/GLFW).");

            try
            {
                Time.SetTargetFPS(60);
                Window.SetExitKey((KeyboardKey)0);

                var saved = new List<string>();
                var previousSelected = session.SelectedSpaceId;
                var previousView = session.ViewMode;
                var previousWire = session.WireMeshMode;
                var previousDeck = session.DeckFilter;

                // Plan per deck.
                foreach (var (deck, kind) in new (int?, string)[] { (-1, "plan-deck-m1"), (0, "plan-deck-0"), (1, "plan-deck-p1") })
                {
                    session.ViewMode = CalypsoViewMode.Plan;
                    session.DeckFilter = deck;
                    session.WireMeshMode = CalypsoWireMeshMode.None;
                    session.StatusText = $"headless:{kind}";
                    CaptureSave(renderer, exportsDir, kind, saved);
                }

                session.DeckFilter = null;

                // Warship / fan-render exterior angles.
                foreach (var (preset, kind, wire) in new (string, string, CalypsoWireMeshMode)[]
                         {
                             ("bow-quarter", "orbit-bow-quarter", CalypsoWireMeshMode.None),
                             ("three-quarter-high", "orbit-three-quarter-high", CalypsoWireMeshMode.None),
                             ("broadside", "orbit-broadside", CalypsoWireMeshMode.None),
                             ("stern-quarter", "orbit-stern-quarter", CalypsoWireMeshMode.None),
                             ("bow-on", "orbit-bow-on", CalypsoWireMeshMode.None),
                             ("stern-on", "orbit-stern-on", CalypsoWireMeshMode.None),
                             ("ramp-close", "orbit-ramp-close", CalypsoWireMeshMode.None),
                             ("pod-port", "orbit-pod-port", CalypsoWireMeshMode.None),
                             ("pod-stbd", "orbit-pod-stbd", CalypsoWireMeshMode.None),
                             ("pod-ftl", "orbit-pod-ftl", CalypsoWireMeshMode.None),
                             ("top-down", "orbit-top-down", CalypsoWireMeshMode.None),
                             ("low-pass", "orbit-low-pass", CalypsoWireMeshMode.None),
                             ("cutaway-long", "orbit-cutaway-long", CalypsoWireMeshMode.CutawayPartial),
                             ("cutaway-beam", "orbit-cutaway-beam", CalypsoWireMeshMode.CutawayPartial),
                         })
                {
                    session.ViewMode = CalypsoViewMode.Orbit;
                    session.WireMeshMode = wire;
                    renderer.ApplyOrbitPreset(preset);
                    session.StatusText = $"headless:{kind}";
                    CaptureSave(renderer, exportsDir, kind, saved);
                }

                // Interior tour spaces (solid; bridge + cargo also cutaway section).
                var tourNames = new[]
                {
                    ("BRIDGE", "bridge"),
                    ("CROSSING", "crossing"),
                    ("CABIN_1", "cabin1"),
                    ("CABIN_2", "cabin2"),
                    ("CABIN_3", "cabin3"),
                    ("GALLEY", "galley"),
                    ("INFIRMARY", "infirmary"),
                    ("STAIRS_P", "stairs"),
                    ("ENG", "engineering"),
                    ("HOLD", "cargoVoid"),
                    ("LOUNGE", "lounge"),
                    ("AIRLOCK_A_port", "airlockPort"),
                    ("AIRLOCK_A_stbd", "airlockStbd"),
                    ("CORR_P", "portCorridor"),
                    ("CORR_S", "stbdCorridor"),
                };

                foreach (var (spaceName, label) in tourNames)
                {
                    var space = session.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase)
                        && (spaceName != "CROSSING" || s.Deck == 0)
                        && (spaceName != "BRIDGE" || s.Deck == 0)
                        && (spaceName != "STAIRS_P" || s.Deck == 0)
                        && (spaceName != "CORR_P" || s.Deck == 0)
                        && (spaceName != "CORR_S" || s.Deck == 0)
                        && (spaceName is not ("CABIN_1" or "CABIN_2" or "CABIN_3") || s.Deck == 1));
                    space ??= session.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));

                    if (space is null)
                        continue;

                    session.ViewMode = CalypsoViewMode.Interior;
                    session.SelectedSpaceId = space.Id;
                    session.SelectedHookId = null;
                    session.DeckFilter = space.Deck;
                    renderer.SyncInteriorFromSelection();

                    var variants = label is "bridge" or "cargoVoid" or "engineering"
                        ? new[]
                        {
                            (CalypsoWireMeshMode.None, "solid"),
                            (CalypsoWireMeshMode.CutawayPartial, "cutaway"),
                        }
                        : new[] { (CalypsoWireMeshMode.None, "solid") };

                    foreach (var (wireMode, variantKind) in variants)
                    {
                        session.WireMeshMode = wireMode;
                        session.StatusText = $"headless:interior:{label}:{variantKind}";
                        renderer.SyncInteriorFromSelection();
                        CaptureSave(renderer, exportsDir, $"interior-{variantKind}-{label}", saved);
                    }
                }

                // DK0 catwalk POV — stand on mid-deck catwalk, see C40 stack + passageways.
                foreach (var (preset, kind) in new (string, string)[]
                         {
                             ("catwalk-containers", "catwalk-containers"),
                             ("catwalk-containers-quarter", "catwalk-containers-quarter"),
                             ("catwalk-span", "catwalk-span"),
                             ("catwalk-passage-port", "catwalk-passage-port"),
                             ("catwalk-passage-stbd", "catwalk-passage-stbd"),
                         })
                {
                    renderer.ApplyInteriorPreset(preset);
                    session.StatusText = $"headless:{kind}";
                    CaptureSave(renderer, exportsDir, kind, saved);
                }

                session.SelectedSpaceId = previousSelected;
                session.ViewMode = previousView;
                session.WireMeshMode = previousWire;
                session.DeckFilter = previousDeck;
                renderer.SyncInteriorFromSelection();

                return saved;
            }
            finally
            {
                Window.Close();
            }
        }
        finally
        {
            AudioDevice.Close();
            RaylibDebug.Reset();
        }
    }

    private static void CaptureSave(CalypsoRenderer renderer, string exportsDir, string kind, List<string> saved)
    {
        var png = CaptureView(renderer, warmUpFrames: 6);
        if (png is null || png.Length == 0)
            return;
        var path = Path.Combine(exportsDir, $"{kind}.png");
        File.WriteAllBytes(path, png);
        saved.Add(path);
        Thread.Sleep(2);
    }

    private static byte[]? CaptureView(CalypsoRenderer renderer, int warmUpFrames)
    {
        byte[]? last = null;
        for (var i = 0; i < warmUpFrames; i++)
        {
            Window.PollInputEvents();
            Graphics.BeginDrawing();
            renderer.DrawFrame(Time.GetFrameTime(), Window.GetScreenWidth(), Window.GetScreenHeight());
            Graphics.EndDrawing();

            if (ScreenFramebufferCapture.TryExportFramebufferToPng(out var png) && png is { Length: > 0 })
                last = png;
        }

        for (var attempt = 0; attempt < 12 && last is null; attempt++)
        {
            if (attempt > 0 && attempt % 3 == 0)
                Thread.Sleep(5);

            Window.PollInputEvents();
            Graphics.BeginDrawing();
            renderer.DrawFrame(Time.GetFrameTime(), Window.GetScreenWidth(), Window.GetScreenHeight());
            Graphics.EndDrawing();
            if (ScreenFramebufferCapture.TryExportFramebufferToPng(out var png) && png is { Length: > 0 })
                last = png;
        }

        return last;
    }
}
