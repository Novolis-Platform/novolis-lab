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
/// Fixed-camera semantic structure inspection views A–E + inventory / knuckle reports.
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
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.45f);
        }

        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        Add("A-knuckles-hull-outline", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.OuterHull;
            session.ShowOmlGuide = true;
            session.RevealEntityIds = CollectKnuckles(registry);
            session.StatusText = "inspect:A knuckles + OML guide";
        });

        Add("B-ring-frames-only", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.OuterHull;
            session.ShowOmlGuide = true;
            session.RevealEntityIds = CollectRingFrames(registry);
            session.StatusText = "inspect:B ordinary ring frames";
        });

        Add("C-heavy-bulkheads-only", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.RevealEntityIds = CollectBulkheads(registry);
            session.StatusText = "inspect:C heavy bulkheads";
        });

        Add("D-frames-and-knuckles", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.OuterHull;
            session.ShowOmlGuide = true;
            var ids = CollectRingFrames(registry);
            ids.UnionWith(CollectKnuckles(registry));
            session.RevealEntityIds = ids;
            session.StatusText = "inspect:D frames + knuckles";
        });

        Add("E-primary-structure-semantic", () =>
        {
            ShipCamera();
            session.VisualLayers = CalypsoVisualLayers.ShipSkeleton;
            session.ShowOmlGuide = true;
            session.RevealEntityIds = null;
            session.StatusText = "inspect:E primary structure · semantic colors";
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
        // Annular web + chords only — not deckbeams that happen to live under FRAME-* assemblies.
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

    public static HashSet<Guid> CollectGirders(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var p in registry.Query(kind: CadPartKind.LongitudinalGirder))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
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
