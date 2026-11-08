using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>Whole-ship inspection A–F for ring-frame interior substructure.</summary>
internal static class HeadlessFrameInteriorInspectionExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = SilkCaptureSize.UhdWidth,
        int height = SilkCaptureSize.UhdHeight)
    {
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "frame-interior");
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.EnumerateFiles(outDir, "*.png"))
            File.Delete(old);

        CadDocumentAdapter.LoadFromDocument(session.Document);
        var registry = CadDocumentAdapter.Active;
        ShipHierarchyBuilder.EnsureCanonicalHierarchy(registry, session.Document);

        PartitionAttachmentValidator.WriteJson(session.Document, layout: null, outDir);

        var previousLayers = session.VisualLayers;
        var previousWire = session.WireMeshMode;
        var previousView = session.ViewMode;
        var previousOml = session.ShowOmlGuide;
        var previousReveal = session.RevealEntityIds;

        var jobs = new List<(string Path, Action Prepare)>();

        void Cam()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.ShowOmlGuide = false;
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.45f);
        }

        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        Add("A-annular-rings-only", () =>
        {
            Cam();
            session.VisualLayers = CalypsoVisualLayers.Frames;
            session.RevealEntityIds = CollectAnnularOnly(registry);
            session.StatusText = "frame-int:A annular rings";
        });

        Add("B-rings-and-chords", () =>
        {
            Cam();
            session.VisualLayers = CalypsoVisualLayers.Frames;
            session.RevealEntityIds = CollectRingsAndChords(registry);
            session.StatusText = "frame-int:B rings + deck chords";
        });

        Add("C-rings-with-interior-supports", () =>
        {
            Cam();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.Joinery;
            session.RevealEntityIds = CollectRingsChordsAndInterior(registry);
            session.StatusText = "frame-int:C rings + interior supports";
        });

        Add("D-supports-with-passage-highlight", () =>
        {
            Cam();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.Joinery
                                   | CalypsoVisualLayers.PassagewayCutouts;
            session.RevealEntityIds = CollectInteriorSupports(registry);
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            session.StatusText = "frame-int:D interiors + passage envelopes";
        });

        Add("E-supports-with-partition-walls", () =>
        {
            Cam();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.Joinery
                                   | CalypsoVisualLayers.Partitions;
            session.RevealEntityIds = null; // walls + supports together (full model filter by layers)
            session.StatusText = "frame-int:E supports + partition walls";
        });

        Add("F-complete-transverse-hull-outline", () =>
        {
            Cam();
            session.VisualLayers = CalypsoVisualLayers.Frames | CalypsoVisualLayers.Bulkheads
                                   | CalypsoVisualLayers.Joinery | CalypsoVisualLayers.OuterHull;
            session.ShowOmlGuide = true;
            session.RevealEntityIds = null;
            session.StatusText = "frame-int:F complete transverse + hull outline";
        });

        try
        {
            return SilkHeadlessCapture.CaptureSequence(
                width, height, jobs, frame => renderer.DrawFrame(frame, 0f));
        }
        finally
        {
            session.VisualLayers = previousLayers;
            session.WireMeshMode = previousWire;
            session.ViewMode = previousView;
            session.ShowOmlGuide = previousOml;
            session.RevealEntityIds = previousReveal;
        }
    }

    public static HashSet<Guid> CollectAnnularOnly(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var p in registry.Parts.Where(p =>
                     p.Kind is CadPartKind.AnnularWebPlate or CadPartKind.RingFrame or CadPartKind.WebFrame
                         or CadPartKind.RingFlange))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        return ids;
    }

    public static HashSet<Guid> CollectRingsAndChords(ShipPartRegistry registry)
    {
        var ids = CollectAnnularOnly(registry);
        foreach (var p in registry.Query(kind: CadPartKind.DeckChord))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        return ids;
    }

    public static HashSet<Guid> CollectInteriorSupports(ShipPartRegistry registry)
    {
        var ids = new HashSet<Guid>();
        foreach (var p in registry.Parts.Where(p =>
                     p.Kind is CadPartKind.PartitionAttachmentPost or CadPartKind.PartitionAttachmentRail
                         or CadPartKind.PortalJamb or CadPartKind.PortalHeader
                         or CadPartKind.PartitionTieBracket))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        // Name fallback before registry stamps catch up
        foreach (var p in registry.Parts.Where(p =>
                     p.DisplayName.Contains("-int-", StringComparison.OrdinalIgnoreCase)))
            ids.UnionWith(registry.CollectEntityIds(p.Id));
        return ids;
    }

    public static HashSet<Guid> CollectRingsChordsAndInterior(ShipPartRegistry registry)
    {
        var ids = CollectRingsAndChords(registry);
        ids.UnionWith(CollectInteriorSupports(registry));
        return ids;
    }
}
