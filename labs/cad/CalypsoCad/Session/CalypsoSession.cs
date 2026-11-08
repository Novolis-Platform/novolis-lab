using CalypsoCad.Export;
using System.Drawing;
using System.Numerics;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Visibility;
using Novolis.Cad.Primitives;
using CalypsoCad.Structure;

namespace CalypsoCad.Session;

internal sealed class CalypsoSession
{
    /// <summary>Authoritative physical/reference object visibility (Explorer + renderer).</summary>
    public HierarchyVisibilityService Visibility { get; } = new();

    public string GeneratedDirectory { get; private set; } = "";
    public CadDocument Document { get; private set; } = new();
    public CadShapesDocument Shapes { get; private set; } = new();
    public CadLayersDocument LayersCatalog { get; private set; } = new();
    public Dictionary<Guid, CadCatalogShape> ShapeById { get; private set; } = new();

    public CalypsoViewMode ViewMode { get; set; } = CalypsoViewMode.Orbit;
    public int? DeckFilter { get; set; } // null = all
    public Guid? SelectedSpaceId { get; set; }
    public Guid? SelectedHookId { get; set; }
    public CalypsoWireMeshMode WireMeshMode { get; set; } = CalypsoWireMeshMode.CutawayPartial;
    /// <summary>Start without solid OML — edge guide + interiors/structure read as one hull.</summary>
    public CalypsoVisualLayers VisualLayers { get; set; } = CalypsoVisualLayers.PrimaryStructure;
    public string StatusText { get; set; } = "";

    /// <summary>Explorer selection — assembly and/or part (ownership, not visibility).</summary>
    public AssemblyId? SelectedAssemblyId { get; set; }
    public PartId? SelectedPartId { get; set; }
    public string? SelectedReferenceId { get; set; }
    public string InspectorText { get; set; } = "";

    /// <summary>
    /// Deprecated presentation toggle — OML guide visibility is owned by hierarchy node
    /// <c>ref-oml-edge-guide</c>. Kept only for headless exporters that still flip a bool;
    /// renderer does not consult this flag.
    /// </summary>
    public bool ShowOmlGuide
    {
        get => Visibility.IsEffectivelyVisible("ref-oml-edge-guide");
        set => Visibility.SetLocal(
            "ref-oml-edge-guide",
            value ? LocalVisibility.Visible : LocalVisibility.Hidden,
            recordUndo: false);
    }

    /// <summary>World cutaway plane origin (used when <see cref="WireMeshMode"/> is CutawayPartial).</summary>
    public Vector3 CutPlaneOrigin { get; set; } = new(0f, 4f, 0f);

    /// <summary>Unit normal pointing toward the camera (camera-side half-space is culled).</summary>
    public Vector3 CutPlaneNormal { get; set; } = Vector3.UnitX;

    /// <summary>Longitudinal (YZ cut, ±X normal) vs beam (XY cut, ±Z normal) for orbit cutaway.</summary>
    public bool CutPlaneLongitudinal { get; set; } = true;

    /// <summary>
    /// Slide the invisible cut plane along its axis from midship (meters).
    /// Longitudinal: ±beam/2; beam cut: ±LOA/2. Bound in the renderer.
    /// </summary>
    public float CutPlaneOffset { get; set; }

    /// <summary>When true, orbit cutaway keeps the user offset (keys [ ] / - =) instead of snapping to CL.</summary>
    public bool CutPlaneUserDriven { get; set; }

    public IEnumerable<CadEntity> Spaces =>
        Document.Entities.Where(e => e.Kind == "space");

    public CadEntity? SelectedSpace =>
        SelectedSpaceId is { } id ? Document.Entities.FirstOrDefault(e => e.Id == id) : null;

    public CadHook? SelectedHook
    {
        get
        {
            if (SelectedHookId is not { } id)
                return null;

            foreach (var e in Document.Entities)
            {
                if (e.Hooks is null)
                    continue;
                foreach (var h in e.Hooks)
                    if (h.Id == id)
                        return h;
            }

            return null;
        }
    }

    public bool TryLoadExisting(string? directory = null)
    {
        var dir = directory ?? CalypsoLockGenerator.DefaultOutputDirectory;
        if (!File.Exists(Path.Combine(dir, "calypso.cadjson")))
            return false;

        GeneratedDirectory = dir;
        (LayersCatalog, Shapes, Document) = CadDocumentBundle.Read(dir, "calypso");
        ShapeById = Shapes.Shapes.ToDictionary(s => s.Id);
        SelectedSpaceId = Spaces.FirstOrDefault(s => s.Name == "BRIDGE" && s.Deck == 0)?.Id
                          ?? Spaces.FirstOrDefault()?.Id;
        RebuildSemanticRegistry();
        StatusText = $"Loaded → {dir}";
        return true;
    }

    public void RegenerateAndLoad()
    {
        GeneratedDirectory = CalypsoLockGenerator.Generate();
        (LayersCatalog, Shapes, Document) = CadDocumentBundle.Read(GeneratedDirectory, "calypso");
        ShapeById = Shapes.Shapes.ToDictionary(s => s.Id);
        SelectedSpaceId = Spaces.FirstOrDefault(s => s.Name == "BRIDGE" && s.Deck == 0)?.Id
                          ?? Spaces.FirstOrDefault()?.Id;
        RebuildSemanticRegistry();
        StatusText = $"Generated → {GeneratedDirectory}";
    }

    public void RebuildSemanticRegistry()
    {
        SelectedAssemblyId = null;
        SelectedPartId = null;
        SelectedReferenceId = null;
        InspectorText = "";
        CadDocumentAdapter.LoadFromDocument(Document);
        ShipHierarchyBuilder.EnsureCanonicalHierarchy(Registry, Document);
        Visibility.BindRegistry(Registry, Document);
        var visPath = VisibilityPersistence.DefaultPath(GeneratedDirectory);
        if (!string.IsNullOrWhiteSpace(GeneratedDirectory)
            && VisibilityPersistence.TryLoad(Visibility, visPath, out _))
        {
            // Restored saved overrides.
        }
        else
        {
            Visibility.ApplySnapshot(VisibilityPresets.PrimaryStructure(), recordUndo: false, asPreset: "primary");
            Visibility.SetLocal("ref-oml-edge-guide", LocalVisibility.Visible, recordUndo: false);
        }
    }

    public void SaveVisibilityOverrides()
    {
        if (string.IsNullOrWhiteSpace(GeneratedDirectory))
            return;
        VisibilityPersistence.Save(Visibility, VisibilityPersistence.DefaultPath(GeneratedDirectory));
    }

    public ShipPartRegistry Registry => CadDocumentAdapter.Active;

    /// <summary>Generate and load the two-bay structural proof CAD (not the full ship).</summary>
    public void RegenerateAndLoadStructuralBay()
    {
        GeneratedDirectory = CalypsoLockGenerator.GenerateStructuralBay();
        (LayersCatalog, Shapes, Document) = CadDocumentBundle.Read(GeneratedDirectory, "calypso-structural-bay");
        ShapeById = Shapes.Shapes.ToDictionary(s => s.Id);
        SelectedSpaceId = null;
        VisualLayers = CalypsoVisualLayers.StructureInspect;
        RebuildSemanticRegistry();
        Visibility.ApplySnapshot(VisibilityPresets.PrimaryStructure(), recordUndo: false, asPreset: "bay");
        Visibility.SetLocal("ref-oml-edge-guide", LocalVisibility.Hidden, recordUndo: false);
        StatusText = $"Structural bay proof → {GeneratedDirectory}";
    }

    public Color ResolveShapeColor(Guid? shapeId, Color fallback)
    {
        return ResolveShapeMaterial(shapeId, fallback).color;
    }

    public (Color color, float roughness, float metalness) ResolveShapeMaterial(Guid? shapeId, Color fallback)
    {
        if (shapeId is not { } id || !ShapeById.TryGetValue(id, out var shape))
            return (fallback, 0.5f, 0f);

        var rgb = shape.Extensions?.Appearance?.Fill?.Color
                  ?? shape.Extensions?.Material?.Albedo;
        var roughness = shape.Extensions?.Material?.Roughness ?? 0.5f;
        var metalness = shape.Extensions?.Material?.Metalness ?? 0f;

        if (rgb is not { Length: >= 3 })
            return (fallback, roughness, metalness);

        var c = Color.FromArgb(255,
            (int)(Math.Clamp(rgb[0], 0, 1) * 255),
            (int)(Math.Clamp(rgb[1], 0, 1) * 255),
            (int)(Math.Clamp(rgb[2], 0, 1) * 255));

        return (c, roughness, metalness);
    }

    public string ResolveMaterialName(Guid? shapeId)
    {
        if (shapeId is not { } id || !ShapeById.TryGetValue(id, out var shape))
            return "(none)";
        return shape.Extensions?.Material?.Preset ?? shape.Name ?? id.ToString("N")[..8];
    }

    public static Vector3 SpaceCentroid(CadEntity space)
    {
        if (space.Points is not { Count: > 0 } pts)
            return Vector3.Zero;
        var sum = Vector3.Zero;
        foreach (var p in pts)
            sum += SvgCoords.FromArray(p);
        return sum / pts.Count;
    }
}
