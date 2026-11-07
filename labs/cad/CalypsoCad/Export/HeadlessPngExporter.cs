using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Silk;

namespace CalypsoCad.Export;

/// <summary>
/// Curated headless Silk PNGs only — visual-gate set + a few orbit references.
/// Cabin tours / catwalks / orbit angle spam are not committed; use interactive host or --build-steps.
/// </summary>
internal static class HeadlessPngExporter
{
    /// <summary>Files written under docs/renders and expected by verify-calypso-visual-gate.</summary>
    public static readonly string[] CuratedExports =
    [
        "plan-deck-0.png",
        "orbit-bow-quarter.png",
        "orbit-three-quarter-high.png",
        "orbit-cutaway-long.png",
        "layer-cutaway-structure.png",
        "layer-structure-wire.png",
        "layer-cutaway-fitout.png",
        "interior-solid-bridge.png",
        "interior-cutaway-portCorridor.png",
    ];

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
        PurgeNonCurated(exportsDir);

        var previousSelected = session.SelectedSpaceId;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousDeck = session.DeckFilter;
        var previousLayers = session.VisualLayers;

        var jobs = new List<(string Path, Action Prepare)>();
        void Add(string kind, Action prepare) =>
            jobs.Add((Path.Combine(exportsDir, $"{kind}.png"), prepare));

        Add("plan-deck-0", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Dk001Plan;
            session.ViewMode = CalypsoViewMode.Plan;
            session.DeckFilter = 0;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "headless:plan-deck-0";
        });

        Add("orbit-bow-quarter", () =>
        {
            session.VisualLayers = previousLayers;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            renderer.ApplyOrbitPreset("bow-quarter");
            session.StatusText = "headless:orbit-bow-quarter";
        });

        Add("orbit-three-quarter-high", () =>
        {
            session.VisualLayers = previousLayers;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            renderer.ApplyOrbitPreset("three-quarter-high");
            session.StatusText = "headless:orbit-three-quarter-high";
        });

        Add("orbit-cutaway-long", () =>
        {
            session.VisualLayers = previousLayers;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            renderer.ApplyOrbitPreset("cutaway-long");
            session.StatusText = "headless:orbit-cutaway-long";
        });

        Add("layer-primary-steel", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.PrimaryStructure;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            renderer.ApplyOrbitPreset("cutaway-long");
            session.StatusText = "headless:layer-primary-steel";
        });

        Add("layer-primary-plus-decks", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.PrimaryPlusDecks;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            renderer.ApplyOrbitPreset("cutaway-long");
            session.StatusText = "headless:layer-primary-plus-decks";
        });

        Add("layer-pressure-airlocks", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.PressureStructure | CalypsoVisualLayers.Airlocks;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            renderer.ApplyOrbitPreset("cutaway-long");
            session.StatusText = "headless:layer-pressure-airlocks";
        });

        Add("layer-passageway-volumes", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.PrimaryPlusDecks | CalypsoVisualLayers.SpaceFinish
                                   | CalypsoVisualLayers.PassagewayCutouts;
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            renderer.ApplyOrbitPreset("cutaway-long");
            session.StatusText = "headless:layer-passageway-volumes";
        });

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

        Add("interior-solid-bridge", () =>
        {
            var space = session.Spaces.FirstOrDefault(s =>
                string.Equals(s.Name, "BRIDGE", StringComparison.OrdinalIgnoreCase) && s.Deck == 0)
                        ?? session.Spaces.FirstOrDefault(s =>
                            string.Equals(s.Name, "BRIDGE", StringComparison.OrdinalIgnoreCase));
            if (space is null)
                return;
            session.ViewMode = CalypsoViewMode.Interior;
            session.SelectedSpaceId = space.Id;
            session.DeckFilter = space.Deck;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.VisualLayers = CalypsoVisualLayers.DefaultInterior;
            session.StatusText = "headless:interior:bridge:solid";
            renderer.SyncInteriorFromSelection();
        });

        Add("interior-cutaway-portCorridor", () =>
        {
            var space = session.Spaces.FirstOrDefault(s =>
                string.Equals(s.Name, "CORR_P", StringComparison.OrdinalIgnoreCase) && s.Deck == 0)
                        ?? session.Spaces.FirstOrDefault(s =>
                            string.Equals(s.Name, "CORR_P", StringComparison.OrdinalIgnoreCase));
            if (space is null)
                return;
            session.ViewMode = CalypsoViewMode.Interior;
            session.SelectedSpaceId = space.Id;
            session.DeckFilter = space.Deck;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            session.VisualLayers = CalypsoVisualLayers.DefaultInterior;
            session.StatusText = "headless:interior:portCorridor:cutaway";
            renderer.SyncInteriorFromSelection();
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
        // Agent dump API — does not expand the curated commit set.
        var previousSelected = session.SelectedSpaceId;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousDeck = session.DeckFilter;
        var previousLayers = session.VisualLayers;
        try
        {
            var file = ApplyDumpPose(session, renderer, kind, preset, compartment);
            var path = Path.Combine(exportsDir, $"{file}.png");
            var saved = SilkHeadlessCapture.CaptureSequence(
                width,
                height,
                [(path, () => { })],
                frame => renderer.DrawFrame(frame, 0f));
            return saved.Count == 0 ? null : saved[0];
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

    private static string ApplyDumpPose(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string kind,
        string? preset,
        string? compartment)
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

    private static void PurgeNonCurated(string exportsDir)
    {
        var keep = new HashSet<string>(CuratedExports, StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(exportsDir, "*.png"))
        {
            var name = Path.GetFileName(path);
            if (!keep.Contains(name))
                File.Delete(path);
        }
    }
}
