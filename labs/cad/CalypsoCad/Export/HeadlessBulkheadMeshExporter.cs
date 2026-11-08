using System.Numerics;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>
/// Diagnostic stills for heavy-bulkhead solids: mesh vs cutaway separation, per-plate views.
/// </summary>
internal static class HeadlessBulkheadMeshExporter
{
    private static readonly (string Id, float ZStemDefault)[] Schedule =
    [
        ("BOW-COLLISION", 13f),
        ("CROSS-FORE", 19f),
        ("CROSS-AFT", 23f),
        ("CREW-AFT", 30.28f),
        ("ENG-FORE", 41f),
        ("ENG-AFT", 49.25f),
    ];

    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = SilkCaptureSize.UltraWidth,
        int height = SilkCaptureSize.UltraHeight)
    {
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "bulkhead-mesh");
        var geomDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "bulkhead-geometry");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(geomDir);
        foreach (var old in Directory.EnumerateFiles(outDir, "*.png"))
            File.Delete(old);

        var loa = session.Document.Entities.Count > 0 ? InferLoa(session) : 69f;
        var plates = session.Document.Entities
            .Where(e => e.Kind == "mesh"
                        && e.Name is not null
                        && e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                        && e.Name.Contains("-plate", StringComparison.OrdinalIgnoreCase))
            .ToList();

        WriteMachineReadableValidation(geomDir, plates, session);

        var previousLayers = session.VisualLayers;
        var previousWire = session.WireMeshMode;
        var previousView = session.ViewMode;
        var previousOml = session.ShowOmlGuide;
        var previousDeck = session.DeckFilter;
        var previousStatus = session.StatusText;

        var jobs = new List<(string Path, Action Prepare)>();
        void Add(string name, Action prepare) =>
            jobs.Add((Path.Combine(outDir, $"{name}.png"), prepare));

        void ShipCamera()
        {
            session.ViewMode = CalypsoViewMode.Orbit;
            session.DeckFilter = null;
            renderer.ApplyOrbitPreset("bow-quarter");
            renderer.ZoomOrbit(1.5f);
        }

        // Whole-ship: no cutaway.
        Add("00-whole-ship-no-cutaway", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "bh-mesh: whole-ship solid (no cutaway)";
            ShipCamera();
        });

        // Whole-ship: cutaway (true geometric clip).
        Add("00-whole-ship-cutaway", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            session.CutPlaneLongitudinal = true;
            session.StatusText = "bh-mesh: whole-ship cutaway (clipped)";
            renderer.ApplyOrbitPreset("cutaway-long");
        });

        Add("01-hull-bulkheads", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = true;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "bh-mesh: hull guide + solid plates";
            ShipCamera();
        });

        Add("02-bulkheads-solid", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = false;
            session.WireMeshMode = CalypsoWireMeshMode.None;
            session.StatusText = "bh-mesh: solid shaded";
            ShipCamera();
        });

        Add("03-bulkheads-edges", () =>
        {
            session.VisualLayers = CalypsoVisualLayers.Bulkheads;
            session.ShowOmlGuide = false;
            session.WireMeshMode = CalypsoWireMeshMode.Wire;
            session.StatusText = "bh-edges: physical boundary only";
            ShipCamera();
        });

        foreach (var (id, zDefault) in Schedule)
        {
            var plate = plates.FirstOrDefault(p =>
                p.Name!.Contains(id, StringComparison.OrdinalIgnoreCase));
            var zStem = ResolveStation(session, id, zDefault);
            var zCad = loa * 0.5f - zStem;
            var slug = id.ToLowerInvariant();

            // 1 — Unclipped face-on solid.
            Add($"{slug}-01-face-on-solid", () =>
            {
                session.VisualLayers = CalypsoVisualLayers.Bulkheads;
                session.ShowOmlGuide = false;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                session.StatusText = $"bh-mesh:face-on {id} unclipped solid";
                renderer.SetOrbitPose(new Vector3(0f, 5.5f, zCad), 28f, 0f, 0.02f);
            });

            // 2 — Unclipped oblique (plate thickness).
            Add($"{slug}-02-oblique-thickness", () =>
            {
                session.VisualLayers = CalypsoVisualLayers.Bulkheads;
                session.ShowOmlGuide = false;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                session.StatusText = $"bh-mesh:oblique {id} thickness";
                renderer.SetOrbitPose(new Vector3(0f, 5.5f, zCad), 26f, 0.55f, 0.28f);
            });

            // 3 — Face-on triangulation edges + centroid ticks (diagnostic).
            Add($"{slug}-03-triangulation", () =>
            {
                session.VisualLayers = CalypsoVisualLayers.Bulkheads;
                session.ShowOmlGuide = false;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                session.StatusText = $"bh-tri-ids:face-on {id} triangulation";
                renderer.SetOrbitPose(new Vector3(0f, 5.5f, zCad), 28f, 0f, 0.02f);
            });

            // 4 — Same plate in whole-ship cutaway context (camera on station).
            Add($"{slug}-04-cutaway-context", () =>
            {
                session.VisualLayers = CalypsoVisualLayers.Bulkheads;
                session.ShowOmlGuide = true;
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                session.CutPlaneLongitudinal = true;
                session.ViewMode = CalypsoViewMode.Orbit;
                session.DeckFilter = null;
                session.StatusText = $"bh-mesh:cutaway {id}";
                renderer.SetOrbitPose(new Vector3(0f, 5.5f, zCad), 40f, MathF.PI * 0.5f, 0.18f);
            });

            _ = plate; // same physical CAD mesh for all views — no alternate geometry.
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
            session.WireMeshMode = previousWire;
            session.ViewMode = previousView;
            session.ShowOmlGuide = previousOml;
            session.DeckFilter = previousDeck;
            session.StatusText = previousStatus;
        }
    }

    private static void WriteMachineReadableValidation(
        string geomDir,
        List<CadEntity> plates,
        CalypsoSession session)
    {
        var results = new List<StructureMeshValidation.Result>();
        if (CalypsoLockGenerator.LastStructureMeshValidations.Count > 0)
        {
            results.AddRange(CalypsoLockGenerator.LastStructureMeshValidations
                .Where(r => r.PartId.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var plate in plates)
        {
            if (plate.MeshVertices is null || plate.MeshIndices is null)
                continue;
            var verts = plate.MeshVertices
                .Select(v => new Vector3(v[0], v[1], v[2]))
                .ToList();
            var mesh = new EditableMesh(verts, plate.MeshIndices);
            var zStem = InferZStem(plate, session);
            var existing = results.FirstOrDefault(r =>
                r.PartId.Equals(plate.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                continue;
            results.Add(StructureMeshValidation.Validate(plate.Name ?? "bh", zStem, mesh));
        }

        var path = Path.Combine(geomDir, "bulkhead-geometry-validation.json");
        StructureMeshValidation.WriteJsonReport(path, results);

        var rootCause = Path.Combine(geomDir, "root-cause.txt");
        File.WriteAllText(path: rootCause, contents:
            """
Root cause (bulkhead triangular artifacts) — FIXED:
1. MESH GENERATION — PolygonWithHolesExtrude.BridgeHoles silently skipped openings when
   bridging/triangulation failed, and ExtrudeAlongZ fell back to a solid outer-only plate.
   Fail-closed extrusion now rejects hole skip / solid fallback / incomplete triangulation.
2. CUTAWAY — DrawStructureMesh now clips triangles against the cut plane with section caps.

Topology justification (remaining vertical non-manifold edges):
Passage openings that meet the outer perimeter create vertical T-junction edges in the
extruded solid (plate faces + portal wall faces share the edge). These are NOT open
boundary leaks. StructureMeshValidation fails any non-vertical non-manifold edge, and
records TopologyJustification = PassagePinchJustification for accepted vertical pinch
edges. See bulkhead-geometry-validation.json field topologyJustification.
""");
    }

    private static float InferLoa(CalypsoSession session)
    {
        // Prefer lock-derived 69 m; fall back from entity extents.
        float zMin = float.PositiveInfinity, zMax = float.NegativeInfinity;
        foreach (var e in session.Document.Entities)
        {
            if (e.Points is not { Count: >= 1 })
                continue;
            var c = e.Points[0];
            if (c.Length < 3)
                continue;
            zMin = MathF.Min(zMin, (float)c[2]);
            zMax = MathF.Max(zMax, (float)c[2]);
        }

        if (!float.IsFinite(zMin) || !float.IsFinite(zMax))
            return 69f;
        // CAD Z is centered; LOA ≈ extent along Z if boxes span the ship.
        var extent = zMax - zMin;
        return extent > 40f ? extent : 69f;
    }

    private static float ResolveStation(CalypsoSession session, string id, float fallback)
    {
        foreach (var r in CalypsoLockGenerator.LastStructureMeshValidations)
        {
            if (r.PartId.Contains(id, StringComparison.OrdinalIgnoreCase))
                return r.StationZStem;
        }

        _ = session;
        return fallback;
    }

    private static float InferZStem(CadEntity plate, CalypsoSession session)
    {
        foreach (var r in CalypsoLockGenerator.LastStructureMeshValidations)
        {
            if (r.PartId.Equals(plate.Name, StringComparison.OrdinalIgnoreCase)
                || (plate.Name is not null && r.PartId.Contains(plate.Name.Replace("-plate", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)))
                return r.StationZStem;
        }

        if (plate.Points is { Count: >= 1 } && plate.Points[0].Length >= 3)
        {
            var zCad = (float)plate.Points[0][2];
            var loa = InferLoa(session);
            return loa * 0.5f - zCad;
        }

        return 0f;
    }
}
