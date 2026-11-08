using CalypsoCad.Geometry;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>
/// Fixed-camera longitudinal / structure inspection views A–E + inventory / knuckle reports.
/// Same physical model; isolation via RevealEntityIds from the registry.
/// </summary>
internal static class HeadlessStructureInspectionExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = SilkCaptureSize.UhdWidth,
        int height = SilkCaptureSize.UhdHeight)
    {
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "structure-inspection");
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.EnumerateFiles(outDir, "*.png"))
            File.Delete(old);

        CadDocumentAdapter.LoadFromDocument(session.Document);
        var registry = CadDocumentAdapter.Active;
        ShipHierarchyBuilder.EnsureCanonicalHierarchy(registry, session.Document);

        var inventoryPath = StructureSurfaceInventory.WriteJson(session.Document, registry, outDir);
        Console.WriteLine($"  surface inventory → {inventoryPath}");

        try
        {
            var hull = HullGeometry.RequireActive();
            var knucklePath = KnuckleContinuityReport.WriteJson(
                session.Document, hull, CalypsoShipbuildingFrames.PlatingJoinInset, outDir);
            Console.WriteLine($"  knuckle continuity → {knucklePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WARN knuckle continuity skipped: {ex.Message}");
        }

        var previousLayers = session.VisualLayers;
        var previousWire = session.WireMeshMode;
        var previousView = session.ViewMode;
        var previousOml = session.ShowOmlGuide;
        var previousDeck = session.DeckFilter;
        var previousReveal = session.RevealEntityIds;

        var jobs = new List<(string Path, Action Prepare)>();

        void ShipCamera()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            // Isolation: never draw OML reference guide unless reveal is cleared.
            session.ShowOmlGuide = false;
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.45f);
        }

        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        Add("A-knuckle-stringers", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.Longitudinals;
            session.RevealEntityIds = CollectKnuckles(registry);
            session.StatusText = "inspect:A hull knuckle stringers";
        });

        Add("B-deck0-supports", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Longitudinals;
            session.RevealEntityIds = CollectDeck0Supports(registry);
            session.StatusText = "inspect:B Deck 0 edge supports";
        });

        Add("C-deck-m1-cargo-floor", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Longitudinals;
            session.RevealEntityIds = CollectCargoFloorSupports(registry);
            session.StatusText = "inspect:C Deck -1 cargo-floor supports";
        });

        Add("D-primary-longitudinal", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Longitudinals;
            session.RevealEntityIds = CollectPrimaryLongitudinal(registry);
            session.StatusText = "inspect:D primary keel + thrust";
        });

        Add("E-all-longitudinal-loadbearing", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Longitudinals | CalypsoVisualLayers.Frames;
            session.RevealEntityIds = CollectAllLongitudinalLoadBearing(registry);
            session.StatusText = "inspect:E all longitudinal load-bearing";
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
            session.RevealEntityIds = previousReveal;
        }
    }

    public static HashSet<Guid> CollectKnuckles(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var asm in registry.Assemblies.Where(a => a.Kind == CadAssemblyKind.HullKnuckleStringer))
            ids.UnionWith(registry.CollectEntityIds(asm.Id));
        foreach (var p in registry.Query(kind: CadPartKind.KnuckleStringer))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        return ids;
    }

    public static HashSet<Guid> CollectRingFrames(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var p in registry.Parts)
        {
            if (p.Kind is CadPartKind.AnnularWebPlate or CadPartKind.RingFrame or CadPartKind.WebFrame
                or CadPartKind.RingFlange or CadPartKind.WebStiffener or CadPartKind.DeckChord)
                ids.UnionWith(registry.CollectEntityIds(p.Id));
        }

        return ids;
    }

    public static HashSet<Guid> CollectBulkheads(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var p in registry.Query(kind: CadPartKind.BulkheadPlate))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        return ids;
    }

    /// <summary>Primary longitudinal system only (keel + thrust) — not deck edges or knuckles.</summary>
    public static HashSet<Guid> CollectPrimaryLongitudinal(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        ids.UnionWith(registry.CollectEntityIds(AssemblyId.Of(ShipHierarchyIds.PrimaryHullGirders)));
        ids.UnionWith(registry.CollectEntityIds(AssemblyId.Of(ShipHierarchyIds.ThrustStructure)));
        ids.UnionWith(registry.CollectEntityIds(AssemblyId.Of(ShipHierarchyIds.Longitudinal)));
        foreach (var p in registry.Parts)
        {
            if (p.System != CadSystem.Longitudinal)
                continue;
            if (p.Kind is CadPartKind.LongitudinalGirder or CadPartKind.ThrustMember)
                ids.UnionWith(registry.CollectEntityIds(p.Id));
        }

        return ids;
    }

    /// <summary>Legacy button name → primary longitudinal only.</summary>
    public static HashSet<Guid> CollectGirders(ShipPartRegistry registry) =>
        CollectPrimaryLongitudinal(registry);

    public static HashSet<Guid> CollectDeck0Supports(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        ids.UnionWith(registry.CollectEntityIds(AssemblyId.Of(ShipHierarchyIds.Deck0)));
        foreach (var p in registry.Parts)
        {
            if (p.Kind != CadPartKind.DeckEdgeGirder)
                continue;
            if (p.Assembly.Value.Contains("dk0", StringComparison.OrdinalIgnoreCase)
                || p.Id.Value.Contains("-dk0", StringComparison.OrdinalIgnoreCase))
                ids.UnionWith(registry.CollectEntityIds(p.Id));
        }

        return ids;
    }

    public static HashSet<Guid> CollectCargoFloorSupports(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        ids.UnionWith(registry.CollectEntityIds(AssemblyId.Of(ShipHierarchyIds.DeckM1)));
        foreach (var p in registry.Parts)
        {
            if (p.Id.Value.StartsWith("cargo-floor-", StringComparison.OrdinalIgnoreCase)
                || p.Assembly.Value.Contains("dkm1", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Kind is CadPartKind.LongitudinalGirder or CadPartKind.DeckEdgeGirder)
                    ids.UnionWith(registry.CollectEntityIds(p.Id));
            }
        }

        return ids;
    }

    /// <summary>Virtual filter — not a physical assembly.</summary>
    public static HashSet<Guid> CollectAllLongitudinalLoadBearing(ShipPartRegistry registry)
    {
        var ids = CollectPrimaryLongitudinal(registry);
        ids.UnionWith(CollectKnuckles(registry));
        foreach (var p in registry.Parts)
        {
            if (p.Kind is CadPartKind.DeckEdgeGirder or CadPartKind.LongitudinalGirder
                or CadPartKind.ThrustMember or CadPartKind.KnuckleStringer)
                ids.UnionWith(registry.CollectEntityIds(p.Id));
        }

        return ids;
    }

    public static HashSet<Guid> CollectBrackets(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var p in registry.Query(kind: CadPartKind.StructuralBracket))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        foreach (var p in registry.Query(kind: CadPartKind.ConnectionBracket))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        return ids;
    }
}
