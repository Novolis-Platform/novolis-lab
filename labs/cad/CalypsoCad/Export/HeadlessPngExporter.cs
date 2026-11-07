using CalypsoCad.Rendering;
using CalypsoCad.Host;
using Novolis.Silk;
namespace CalypsoCad.Export;

/// <summary>
/// Headless Silk PNG export via hidden GLFW window + framebuffer readback.
/// Writes stable <c>{kind}.png</c> names (overwrite) and purges legacy <c>*-headless-*</c> dumps.
/// </summary>
internal static class HeadlessPngExporter
{
    public static IReadOnlyList<string> ExportViews(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string generatedRoot,
        int width = 3840,
        int height = 2160)
    {
        _ = generatedRoot;
        var exportsDir = CalypsoProjectPaths.RendersExportsDirectory;
        Directory.CreateDirectory(exportsDir);
        PurgeLegacyNamed(exportsDir);

        var previousSelected = session.SelectedSpaceId;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousDeck = session.DeckFilter;
        var previousLayers = session.VisualLayers;

        var jobs = new List<(string Path, Action Prepare)>();

        void Add(string kind, Action prepare) =>
            jobs.Add((Path.Combine(exportsDir, $"{kind}.png"), prepare));

        session.VisualLayers = CalypsoVisualLayers.Dk001Plan;
        foreach (var (deck, kind) in new (int?, string)[] { (-1, "plan-deck-m1"), (0, "plan-deck-0"), (1, "plan-deck-p1") })
        {
            var d = deck;
            var k = kind;
            Add(k, () =>
            {
                session.VisualLayers = CalypsoVisualLayers.Dk001Plan;
                session.ViewMode = CalypsoViewMode.Plan;
                session.DeckFilter = d;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.StatusText = $"headless:{k}";
            });
        }

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
            var p = preset;
            var k = kind;
            var w = wire;
            Add(k, () =>
            {
                session.VisualLayers = previousLayers;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                session.WireMeshMode = w;
                renderer.ApplyOrbitPreset(p);
                session.StatusText = $"headless:{k}";
            });
        }

        Add("layer-cutaway-structure", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureOnlyCutaway;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            renderer.ApplyOrbitPreset("cutaway-long");
            session.StatusText = "headless:layer-cutaway-structure";
        });

        Add("layer-structure-wire", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.StructureOnlyCutaway;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.Wire;
            renderer.ApplyOrbitPreset("three-quarter-high");
            renderer.ZoomOrbit(1.5f);
            session.StatusText = "headless:layer-structure-wire";
        });

        Add("layer-cutaway-fitout", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.FitoutCutaway;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            renderer.ApplyOrbitPreset("three-quarter-high");
            session.StatusText = "headless:layer-cutaway-fitout";
        });

        Add("layer-interior-structure", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.InteriorStructureNoHull;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            renderer.ApplyOrbitPreset("three-quarter-high");
            renderer.ZoomOrbit(1.5f);
            session.StatusText = "headless:layer-interior-structure";
        });

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
            var name = spaceName;
            var lab = label;
            var variants = label is "bridge" or "cargoVoid" or "engineering" or "portCorridor"
                ? new[]
                {
                    (CalypsoWireMeshMode.None, "solid"),
                    (CalypsoWireMeshMode.CutawayPartial, "cutaway"),
                }
                : new[] { (CalypsoWireMeshMode.None, "solid") };

            foreach (var (wireMode, variantKind) in variants)
            {
                var wm = wireMode;
                var vk = variantKind;
                Add($"interior-{vk}-{lab}", () =>
                {
                    var space = session.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
                        && (name != "CROSSING" || s.Deck == 0)
                        && (name != "BRIDGE" || s.Deck == 0)
                        && (name != "STAIRS_P" || s.Deck == 0)
                        && (name != "CORR_P" || s.Deck == 0)
                        && (name != "CORR_S" || s.Deck == 0)
                        && (name is not ("CREW_1" or "CREW_2" or "CREW_3") || s.Deck == 0)
                        && (name is not "PAX_1" || s.Deck == 1));
                    space ??= session.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (space is null)
                        return;

                    session.ViewMode = CalypsoViewMode.Interior;
                    session.SelectedSpaceId = space.Id;
                    session.SelectedHookId = null;
                    session.DeckFilter = space.Deck;
                    session.WireMeshMode = wm;
                    session.VisualLayers = CalypsoVisualLayers.DefaultInterior;
                    session.StatusText = $"headless:interior:{lab}:{vk}";
                    renderer.SyncInteriorFromSelection();
                });
            }
        }

        foreach (var (preset, kind) in new (string, string)[]
                 {
                     ("catwalk-containers", "catwalk-containers"),
                     ("catwalk-containers-quarter", "catwalk-containers-quarter"),
                     ("catwalk-span", "catwalk-span"),
                     ("catwalk-passage-port", "catwalk-passage-port"),
                     ("catwalk-passage-stbd", "catwalk-passage-stbd"),
                 })
        {
            var p = preset;
            var k = kind;
            Add(k, () =>
            {
                session.VisualLayers = previousLayers;
                renderer.ApplyInteriorPreset(p);
                session.StatusText = $"headless:{k}";
            });
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
            session.SelectedSpaceId = previousSelected;
            session.ViewMode = previousView;
            session.WireMeshMode = previousWire;
            session.DeckFilter = previousDeck;
            session.VisualLayers = previousLayers;
            renderer.SyncInteriorFromSelection();
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
        _ = generatedRoot;
        var exportsDir = CalypsoProjectPaths.RendersExportsDirectory;
        Directory.CreateDirectory(exportsDir);
        var file = ApplyDumpPose(session, renderer, kind, preset, compartment);
        var path = Path.Combine(exportsDir, $"{file}.png");
        var saved = SilkHeadlessCapture.CaptureSequence(
            width,
            height,
            [(path, () => { })],
            frame => renderer.DrawFrame(frame, 0f));
        return saved.Count == 0 ? null : saved[0];
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

    private static void PurgeLegacyNamed(string exportsDir)
    {
        if (!Directory.Exists(exportsDir))
            return;
        foreach (var file in Directory.EnumerateFiles(exportsDir, "*-headless-*.png"))
        {
            try { File.Delete(file); }
            catch { /* best-effort */ }
        }
    }
}
