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

                var previousLayers = session.VisualLayers;
                session.VisualLayers = CalypsoVisualLayers.StructureOnlyCutaway;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                renderer.ApplyOrbitPreset("cutaway-long");
                session.StatusText = "headless:layer-cutaway-structure";
                CaptureSave(renderer, exportsDir, "layer-cutaway-structure", saved);

                session.WireMeshMode = CalypsoWireMeshMode.Wire;
                renderer.ApplyOrbitPreset("three-quarter-high");
                session.StatusText = "headless:layer-structure-wire";
                CaptureSave(renderer, exportsDir, "layer-structure-wire", saved);
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;

                session.VisualLayers = CalypsoVisualLayers.FitoutCutaway;
                session.StatusText = "headless:layer-cutaway-fitout";
                CaptureSave(renderer, exportsDir, "layer-cutaway-fitout", saved);
                session.VisualLayers = previousLayers;

                // Interior tour spaces (solid; bridge + cargo also cutaway section).
                var tourNames = new[]
                {
                    ("BRIDGE", "bridge"),
                    ("CROSSING", "crossing"),
                    ("CREW_1", "cabin1"),
                    ("CREW_2", "cabin2"),
                    ("CREW_3", "cabin3"),
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
                    ("PAX_1", "pax1"),
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
                        && (spaceName is not ("CREW_1" or "CREW_2" or "CREW_3") || s.Deck == 0)
                        && (spaceName is not "PAX_1" || s.Deck == 1));
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
                        : label is "portCorridor"
                            ? new[]
                            {
                                (CalypsoWireMeshMode.None, "solid"),
                                (CalypsoWireMeshMode.CutawayPartial, "cutaway"),
                            }
                            : new[] { (CalypsoWireMeshMode.None, "solid") };

                    foreach (var (wireMode, variantKind) in variants)
                    {
                        session.WireMeshMode = wireMode;
                        session.VisualLayers = label is "portCorridor" && variantKind == "cutaway"
                            ? CalypsoVisualLayers.CorridorStructure
                            : CalypsoVisualLayers.DefaultInterior;
                        session.StatusText = $"headless:interior:{label}:{variantKind}";
                        renderer.SyncInteriorFromSelection();
                        CaptureSave(renderer, exportsDir, $"interior-{variantKind}-{label}", saved);
                    }
                }

                session.VisualLayers = previousLayers;

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

    public static string? ExportOne(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string generatedRoot,
        string kind,
        string? preset = null,
        string? compartment = null,
        int width = 1280,
        int height = 720)
    {
        var exportsDir = ViewportPngExporter.ExportsDirectory(generatedRoot);
        Directory.CreateDirectory(exportsDir);
        RaylibDebug.Start();
        Logger.SetTraceLogLevel(TraceLogLevel.Warning);
        AudioDevice.Init();
        try
        {
            Window.SetConfigFlags(WindowStateFlags.Hidden);
            Window.Init(width, height, "CalypsoCad agent dump");
            if (!Window.IsReady())
                throw new InvalidOperationException("Raylib window failed to initialize (display/GLFW).");

            try
            {
                Time.SetTargetFPS(60);
                Window.SetExitKey((KeyboardKey)0);
                var saved = new List<string>();
                var file = ApplyDumpPose(session, renderer, kind, preset, compartment);
                CaptureSave(renderer, exportsDir, file, saved);
                return saved.Count == 0 ? null : saved[0];
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

    private static string ApplyDumpPose(CalypsoSession session, CalypsoRenderer renderer, string kind, string? preset, string? compartment)
    {
        switch (kind.ToLowerInvariant())
        {
            case "plan":
                session.ViewMode = CalypsoViewMode.Plan;
                session.DeckFilter = preset switch
                {
                    "m1" or "-1" => -1,
                    "1" or "p1" => 1,
                    _ => 0,
                };
                session.WireMeshMode = CalypsoWireMeshMode.None;
                return session.DeckFilter switch
                {
                    -1 => "plan-deck-m1",
                    1 => "plan-deck-p1",
                    _ => "plan-deck-0",
                };
            case "interior":
                var name = string.IsNullOrWhiteSpace(compartment) ? "BRIDGE" : compartment;
                var space = session.Spaces.FirstOrDefault(s =>
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
                if (space is not null)
                {
                    session.ViewMode = CalypsoViewMode.Interior;
                    session.SelectedSpaceId = space.Id;
                    session.DeckFilter = space.Deck;
                    session.WireMeshMode = CalypsoWireMeshMode.None;
                    renderer.SyncInteriorFromSelection();
                }

                return "interior-solid-" + name;
            default:
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                session.WireMeshMode = preset is "cutaway-long" or "cutaway-beam"
                    ? CalypsoWireMeshMode.CutawayPartial
                    : CalypsoWireMeshMode.None;
                renderer.ApplyOrbitPreset(string.IsNullOrWhiteSpace(preset) ? "broadside" : preset);
                return "orbit-" + (string.IsNullOrWhiteSpace(preset) ? "broadside" : preset);
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
