using System.Globalization;
using System.Text;
using System.Text.Json;
using Novolis.Avalonia.Cad.Ship.Services;
using Novolis.Cad.Primitives;
using Novolis.Raylib.Audio;
using Novolis.Raylib.Debug;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Logging;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Timing;
using Novolis.Raylib.Windowing;

namespace CalypsoCad.Services;

/// <summary>
/// Progressive build slides: hull → bulkheads → frames → longitudinals →
/// passageways → airlocks → hatch groups → complete.
/// Structure phases use structure-wire; interior phases use fitout+cutouts so spaces/hatches read.
/// </summary>
internal static class HeadlessBuildStepExporter
{
    internal readonly record struct Step(string Id, string Title, string FileName, int EntityCount);

    private static readonly CalypsoVisualLayers StructureWire =
        CalypsoVisualLayers.StructureOnlyCutaway | CalypsoVisualLayers.Openings;

    /// <summary>Airtight passages / hold / hatches — no OML (corridor seal is partition walls).</summary>
    private static readonly CalypsoVisualLayers InteriorFitout =
        CalypsoVisualLayers.InteriorStructureNoHull;

    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        int width = 2560,
        int height = 1440)
    {
        var outDir = CalypsoProjectPaths.BuildStepsDirectory;
        Directory.CreateDirectory(outDir);
        foreach (var stale in Directory.EnumerateFiles(outDir, "*.png"))
        {
            try { File.Delete(stale); } catch { /* best-effort */ }
        }

        var steps = BuildSteps(session.Document);
        RaylibDebug.Start();
        Logger.SetTraceLogLevel(TraceLogLevel.Warning);
        AudioDevice.Init();
        try
        {
            Window.SetConfigFlags(WindowStateFlags.Hidden);
            Window.Init(width, height, "CalypsoCad build-steps");
            if (!Window.IsReady())
                throw new InvalidOperationException("Raylib window failed to initialize (display/GLFW).");

            try
            {
                Time.SetTargetFPS(60);
                Window.SetExitKey((KeyboardKey)0);

                var previousLayers = session.VisualLayers;
                var previousView = session.ViewMode;
                var previousWire = session.WireMeshMode;
                var previousReveal = session.RevealEntityIds;
                var previousStatus = session.StatusText;
                var previousDeck = session.DeckFilter;

                renderer.ApplyOrbitPreset("three-quarter-high");
                renderer.ZoomOrbit(1.5f);

                var saved = new List<string>();
                var cumulative = new HashSet<Guid>();
                var manifest = new List<Step>();

                for (var i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    foreach (var id in step.EntityIds)
                        cumulative.Add(id);

                    session.VisualLayers = step.Layers;
                    session.ViewMode = step.View;
                    session.DeckFilter = step.DeckFilter;
                    // Structure-only: wire cage. Passage/hold: cutaway tubes. DK-001 plan: solid plan.
                    session.WireMeshMode = step.View == CalypsoViewMode.Plan
                        ? CalypsoWireMeshMode.None
                        : step.Layers.HasFlag(CalypsoVisualLayers.Partitions)
                            ? CalypsoWireMeshMode.CutawayPartial
                            : CalypsoWireMeshMode.Wire;
                    session.RevealEntityIds = string.Equals(step.Id, "complete", StringComparison.Ordinal)
                        || string.Equals(step.Id, "dk001-plan", StringComparison.Ordinal)
                        ? null
                        : new HashSet<Guid>(cumulative);
                    session.StatusText = $"build-step:{i + 1}/{steps.Count} {step.Id}";
                    var fileName = $"{i:D2}-{Sanitize(step.Id)}.png";
                    var path = Path.Combine(outDir, fileName);
                    Capture(renderer, path);
                    saved.Add(path);
                    manifest.Add(new Step(
                        step.Id,
                        step.Title,
                        fileName,
                        session.RevealEntityIds?.Count ?? session.Document.Entities.Count));
                }

                WriteManifest(outDir, manifest);

                session.VisualLayers = previousLayers;
                session.ViewMode = previousView;
                session.WireMeshMode = previousWire;
                session.RevealEntityIds = previousReveal;
                session.StatusText = previousStatus;
                session.DeckFilter = previousDeck;
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

    private sealed record PlannedStep(
        string Id,
        string Title,
        List<Guid> EntityIds,
        CalypsoVisualLayers Layers,
        CalypsoViewMode View = CalypsoViewMode.Orbit,
        int? DeckFilter = null);

    private static List<PlannedStep> BuildSteps(CadDocument doc)
    {
        var steps = new List<PlannedStep>();
        var exterior = doc.Entities
            .Where(CadShipExterior.IsExteriorDrawable)
            .Select(e => e.Id)
            .ToList();
        steps.Add(new PlannedStep("hull", "OML / exterior shell", exterior, StructureWire));

        // —— Structure: bulkheads (massive plates) ——
        foreach (var g in GroupByKey(
                     doc.Entities.Where(e => e.Name?.StartsWith("bh-", StringComparison.OrdinalIgnoreCase) == true),
                     e => BulkheadKey(e.Name!),
                     bowFirst: true))
        {
            steps.Add(new PlannedStep(
                $"bh-{g.Key}",
                $"Bulkhead {g.Key} (+{g.Ids.Count})",
                g.Ids,
                StructureWire));
        }

        // —— Structure: bay frame rims ——
        foreach (var g in GroupByKey(
                     doc.Entities.Where(e => e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true),
                     e => FrameStationKey(e.Name!),
                     bowFirst: true))
        {
            steps.Add(new PlannedStep(
                $"frame-{g.Key}",
                $"Web frame {g.Key} (+{g.Ids.Count})",
                g.Ids,
                StructureWire));
        }

        var girders = doc.Entities
            .Where(e =>
                e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true
                || e.Name?.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase) == true
                || e.Name?.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase) == true
                || e.Name?.StartsWith("ramp-", StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => e.Id)
            .ToList();
        if (girders.Count > 0)
            steps.Add(new PlannedStep("longitudinals", $"Girders / coaming / ramp (+{girders.Count})", girders, StructureWire));

        // —— Passageway (DK-001: ACCESS + CROSS + CORR = one clear) ——
        foreach (var spaceName in new[] { "ACCESS", "CROSSING", "CORR_P", "CORR_S", "STAIRS_P", "ELEV_S" })
        {
            var ids = CollectSpaceEnsemble(doc, spaceName);
            if (ids.Count == 0)
                continue;
            steps.Add(new PlannedStep(
                $"passage-{spaceName}",
                $"Passageway {spaceName} (airtight tube, +{ids.Count})",
                ids,
                InteriorFitout));
        }

        // —— HOLD open cargo bay (DK-001 BOX AREA — walls + deck, not a cube) ——
        var holdIds = CollectSpaceEnsemble(doc, "HOLD");
        if (holdIds.Count > 0)
        {
            steps.Add(new PlannedStep(
                "hold-bay",
                $"HOLD open cargo bay (+{holdIds.Count})",
                holdIds,
                InteriorFitout));
        }

        // —— Airlocks (cad names: AIRLOCK_*_port / _stbd) ——
        foreach (var (sideKey, sideTag, hatchSide) in new[]
                 {
                     ("port", "port", "-P"),
                     ("stbd", "stbd", "-S"),
                 })
        {
            var ids = new List<Guid>();
            foreach (var name in new[] { $"AIRLOCK_A_{sideTag}", $"AIRLOCK_B_{sideTag}" })
                ids.AddRange(CollectSpaceEnsemble(doc, name));
            ids.AddRange(doc.Entities
                .Where(e => string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase))
                .Where(e =>
                    e.Name?.Contains(hatchSide, StringComparison.OrdinalIgnoreCase) == true
                    && (e.Name.StartsWith("D1-", StringComparison.OrdinalIgnoreCase)
                        || e.Name.StartsWith("D2-", StringComparison.OrdinalIgnoreCase)
                        || e.Name.StartsWith("D3-", StringComparison.OrdinalIgnoreCase)))
                .Select(e => e.Id));
            ids = ids.Distinct().ToList();
            if (ids.Count == 0)
                continue;
            steps.Add(new PlannedStep(
                $"airlock-{sideKey}",
                $"Airlock {sideKey} (vestibule + D1/D2/D3, +{ids.Count})",
                ids,
                InteriorFitout));
        }

        // —— Interior hatches by pack (all of them, grouped so each slide changes the read) ——
        AddHatchGroup(steps, doc, "hatches-crew", "Crew cabin hatches",
            n => n.StartsWith("CREW", StringComparison.OrdinalIgnoreCase));
        AddHatchGroup(steps, doc, "hatches-pax", "Passenger cabin hatches",
            n => n.StartsWith("PAX", StringComparison.OrdinalIgnoreCase));
        AddHatchGroup(steps, doc, "hatches-service", "Galley / infirmary / ENG / HOLD hatches",
            n => n.StartsWith("GAL", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("INF", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("ENG", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("HOLD", StringComparison.OrdinalIgnoreCase));
        AddHatchGroup(steps, doc, "hatches-vacuum", "Vacuum / shell hatches D1–D3",
            n => n.StartsWith("D1-", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("D2-", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("D3-", StringComparison.OrdinalIgnoreCase));

        // Any remaining openings not already queued.
        var queued = steps.SelectMany(s => s.EntityIds).ToHashSet();
        var leftover = doc.Entities
            .Where(e => string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase))
            .Where(e => !queued.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();
        if (leftover.Count > 0)
            steps.Add(new PlannedStep("hatches-other", $"Other openings (+{leftover.Count})", leftover, InteriorFitout));

        // DK-001 plan: blue shirt-sleeves passageway, red bulkheads (passage cut out), open HOLD.
        steps.Add(new PlannedStep(
            "dk001-plan",
            "DK-001 plan — blue passageway + red BH cutouts + HOLD bay",
            [],
            CalypsoVisualLayers.Dk001Plan,
            CalypsoViewMode.Plan,
            DeckFilter: 0));

        steps.Add(new PlannedStep(
            "complete",
            "Complete — blue shirt-sleeves + red bulkheads (no OML)",
            [],
            CalypsoVisualLayers.Dk001Plan,
            CalypsoViewMode.Plan,
            DeckFilter: 0));

        return steps;
    }

    private static void AddHatchGroup(
        List<PlannedStep> steps,
        CadDocument doc,
        string id,
        string title,
        Func<string, bool> nameMatch)
    {
        var openings = doc.Entities
            .Where(e => string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase))
            .Where(e => e.Name is not null && nameMatch(e.Name))
            .ToList();
        if (openings.Count == 0)
            return;

        var ids = new List<Guid>();
        foreach (var o in openings)
        {
            ids.Add(o.Id);
            // Hatch host walls / jambs named after the opening.
            var stem = o.Name!;
            ids.AddRange(doc.Entities
                .Where(e => e.Name?.Contains(stem, StringComparison.OrdinalIgnoreCase) == true)
                .Select(e => e.Id));
        }

        ids = ids.Distinct().ToList();
        steps.Add(new PlannedStep(id, $"{title} (+{openings.Count})", ids, InteriorFitout));
    }

    /// <summary>Space + its partition walls + openings that name-match the space.</summary>
    private static List<Guid> CollectSpaceEnsemble(CadDocument doc, string spaceName)
    {
        var ids = new List<Guid>();
        foreach (var e in doc.Entities)
        {
            if (e.Name is null)
                continue;
            if (string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Name, spaceName, StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(e.Id);
                continue;
            }

            if (string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)
                && e.Name.StartsWith($"w-{spaceName}-", StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(e.Id);
                continue;
            }

            if (string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase)
                && e.Name.Contains(spaceName, StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(e.Id);
            }
        }

        return ids.Distinct().ToList();
    }

    private static List<(string Key, List<Guid> Ids, float Z)> GroupByKey(
        IEnumerable<CadEntity> entities,
        Func<CadEntity, string> keyOf,
        bool bowFirst)
    {
        var q = entities
            .GroupBy(e => keyOf(e), StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                Key: g.Key,
                Ids: g.Select(e => e.Id).ToList(),
                Z: g.Average(EntityCadZ)));
        return bowFirst
            ? q.OrderByDescending(g => g.Z).ToList()
            : q.OrderBy(g => g.Z).ToList();
    }

    private static string BulkheadKey(string name)
    {
        var rest = name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
            ? name["bh-".Length..]
            : name;
        var cut = rest.IndexOf("-web-", StringComparison.OrdinalIgnoreCase);
        if (cut > 0)
            return rest[..cut];
        cut = rest.IndexOf("-flange-", StringComparison.OrdinalIgnoreCase);
        return cut > 0 ? rest[..cut] : rest;
    }

    private static string FrameStationKey(string name)
    {
        var rest = name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
            ? name["frame-".Length..]
            : name;
        var dash = rest.IndexOf('-');
        return dash > 0 ? rest[..dash] : rest;
    }

    private static float EntityCadZ(CadEntity e)
    {
        if (e.Points is { Count: >= 1 } && e.Points[0].Length > 2)
            return e.Points[0][2];
        if (e.A is { Length: >= 3 } && e.B is { Length: >= 3 })
            return (e.A[2] + e.B[2]) * 0.5f;
        if (e.Footprint is { Count: > 0 })
            return e.Footprint.Average(p => p.Length > 2 ? p[2] : 0f);
        return 0f;
    }

    private static void Capture(CalypsoRenderer renderer, string path)
    {
        byte[]? last = null;
        for (var i = 0; i < 4; i++)
        {
            Window.PollInputEvents();
            Graphics.BeginDrawing();
            renderer.DrawFrame(Time.GetFrameTime(), Window.GetScreenWidth(), Window.GetScreenHeight());
            Graphics.EndDrawing();
            if (ScreenFramebufferCapture.TryExportFramebufferToPng(out var png) && png is { Length: > 0 })
                last = png;
        }

        if (last is null)
            throw new InvalidOperationException($"Failed to capture build-step PNG: {path}");
        File.WriteAllBytes(path, last);
    }

    private static void WriteManifest(string outDir, List<Step> steps)
    {
        var path = Path.Combine(outDir, "manifest.json");
        var payload = new
        {
            packageId = "CAL-MFG-002",
            camera = "orbit three-quarter-high ×1.5 zoom; structure-wire then fitout+cutouts",
            generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            stepCount = steps.Count,
            steps = steps.Select(s => new
            {
                s.Id,
                s.Title,
                s.FileName,
                s.EntityCount,
            }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        var md = new StringBuilder();
        md.AppendLine("# Calypso structure build steps");
        md.AppendLine();
        md.AppendLine($"Camera: three-quarter-high ×1.5 · {steps.Count} slides (structure → passageways → airlocks → hatches)");
        md.AppendLine();
        for (var i = 0; i < steps.Count; i++)
            md.AppendLine($"{i + 1}. `{steps[i].FileName}` — {steps[i].Title}");
        File.WriteAllText(Path.Combine(outDir, "README.md"), md.ToString());
    }

    private static string Sanitize(string id)
    {
        var sb = new StringBuilder(id.Length);
        foreach (var c in id)
            sb.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        return sb.ToString();
    }
}
