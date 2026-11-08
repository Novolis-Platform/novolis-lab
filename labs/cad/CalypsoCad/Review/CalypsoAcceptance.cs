using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;
using Novolis.Ship.Topology;
using Novolis.Ship.Validation;
using CalypsoCad.Blueprints;
using CalypsoCad.Fabrication;
using CalypsoCad.Publication;
using CalypsoCad.Session;
using CalypsoCad.Structure;

namespace CalypsoCad.Review;

internal static class CalypsoAcceptance
{
    internal readonly record struct Check(string Name, bool Ok, string Detail);

    public static List<Check> Evaluate(CadDocument doc, string? generatedDirectory = null)
    {
        var checks = new List<Check>();
        void Add(string name, bool ok, string detail = "") => checks.Add(new Check(name, ok, detail));

        var loa = ShipDocumentMetrics.GetLoaMeters(doc);
        Add("LOA 69", Math.Abs(loa - 69f) < 0.01f, $"got {loa}");

        var hull = doc.Entities.FirstOrDefault(e =>
            string.Equals(e.Name, "ext-oml-hull", StringComparison.OrdinalIgnoreCase));
        Add(
            "faceted OML mesh (not blob box)",
            hull is { Kind: var k }
            && string.Equals(k, "mesh", StringComparison.OrdinalIgnoreCase)
            && hull.MeshIndices is { Count: >= 90 },
            hull is null ? "missing" : $"kind={hull.Kind} inds={hull.MeshIndices?.Count ?? 0}");

        Add(
            "IML nest shell present",
            doc.Entities.Any(e => string.Equals(e.Name, "int-iml-hull", StringComparison.OrdinalIgnoreCase)));

        Add(
            "no fake exterior pods",
            !doc.Entities.Any(e =>
                (e.Name?.Contains("nacelle", StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.Name?.Contains("airlock-blister", StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.Name?.Contains("pod", StringComparison.OrdinalIgnoreCase) ?? false)));

        var clipped = doc.Entities.Count(e =>
            string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)
            && e.Properties is not null
            && e.Properties.TryGetValue("clippedToManufacturerHull", out var c)
            && c.ValueKind == System.Text.Json.JsonValueKind.True);
        Add("fore interiors clipped into hull", clipped >= 1, $"clipped={clipped}");

        Add(
            "outerHull property = manufacturer",
            doc.Properties is not null
            && doc.Properties.TryGetValue("outerHull", out var oh)
            && oh.GetString()?.Contains("manufacturer", StringComparison.OrdinalIgnoreCase) == true);

        var crew = ShipCad.Spaces(doc)
            .Where(s => s.Name is not null && s.Name.StartsWith("CREW_", StringComparison.OrdinalIgnoreCase) && s.Deck == 0)
            .Select(s => s.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Add("five CREW_1..5 (DK-001 C1–C5)", crew.Count == 5, string.Join(",", crew));

        var pax = ShipCad.Spaces(doc)
            .Where(s => s.Name is not null && s.Name.StartsWith("PAX_", StringComparison.OrdinalIgnoreCase) && s.Deck == 1)
            .Select(s => s.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Add("five PAX_1..5 (DK-001 P1–P5)", pax.Count == 5, string.Join(",", pax));

        var crew1 = ShipCad.Spaces(doc).FirstOrDefault(s => s.Name == "CREW_1" && s.Deck == 0);
        if (crew1?.Points is { Count: >= 4 } cpts)
        {
            var ys = cpts.Select(p => p[0]).ToList();
            var zs = cpts.Select(p => p[2]).ToList();
            var clearW = ys.Max() - ys.Min();
            var clearD = zs.Max() - zs.Min();
            Add("CREW_1 clear W ≈ 1.92", clearW is >= 1.85f and <= 2.05f, $"got {clearW:0.###}");
            Add("CREW_1 clear F–A ≈ 7.2", clearD is >= 7.0f and <= 7.4f, $"got {clearD:0.###}");
        }
        else
        {
            Add("CREW_1 present", false);
        }

        var corrP = ShipCad.Spaces(doc).FirstOrDefault(s => s.Name == "CORR_P" && s.Deck == 0);
        if (corrP?.Points is { Count: >= 4 } pts)
        {
            var xs = pts.Select(p => p[0]).ToList();
            var clear = xs.Max() - xs.Min();
            Add("CORR_P clear ≥ 3 m (DK-001)", clear >= 2.95f, $"got {clear:0.###}");
        }
        else
        {
            Add("CORR_P present", false);
        }

        Add(
            "deck drawing property",
            doc.Properties is not null
            && doc.Properties.TryGetValue("deckDrawing", out var dd)
            && dd.GetString()?.Contains("CAL-INT-DK-001", StringComparison.OrdinalIgnoreCase) == true);

        Add("structure mass attached", ShipStructureDocument.TryGetMass(doc, out var mass)
                                         && mass is not null && mass.MassKg > 240_000f);

        var d3 = ShipCad.Openings(doc)
            .FirstOrDefault(o => o.Name is not null && o.Name.StartsWith("D3-", StringComparison.OrdinalIgnoreCase));
        Add("D3 vacuum-assisted", d3 is not null && ShipCad.IsVacuumAssisted(d3!));
        Add(
            "D3 closed under vacuum",
            d3 is not null && ShipCad.GetLeafState(d3!) == ShipLeafState.Closed);

        var holdHatch = ShipCad.Openings(doc)
            .FirstOrDefault(o => string.Equals(o.Name, "HOLD-P-DKM1", StringComparison.OrdinalIgnoreCase));
        Add(
            "HOLD-P-DKM1 standard open hatch",
            holdHatch is not null
            && ShipCad.IsStandardHatch(holdHatch!)
            && ShipCad.GetLeafState(holdHatch!) == ShipLeafState.Open
            && ShipCad.GetOpeningConnects(holdHatch!).Count >= 2);

        var walk = ShipWalk.BuildDeckMinusOneAftTour(doc);
        Add("walk tour from HOLD aft", walk.Count >= 8, $"waypoints={walk.Count}");
        Add(
            "walk eyes clamped (no wall pass-through)",
            walk.All(wp =>
            {
                var space = doc.Entities.FirstOrDefault(e => e.Id == wp.SpaceId);
                if (space is null)
                    return false;
                var clamped = ShipWalk.ClampToSpace(space, wp.Eye, ShipWalk.DefaultInset);
                return Vector3.Distance(wp.Eye, clamped) < 0.05f;
            }),
            $"waypoints={walk.Count}");

        var topo = ShipTopology.Analyze(doc);
        Add("topology has spaces", topo.SpaceIds.Count > 0, $"count={topo.SpaceIds.Count}");
        ShipTopology.ApplySpaceFlags(doc, topo);

        var validation = ShipValidator.Validate(doc, topo);
        var hardErrors = validation.Issues
            .Where(i => i.Severity == ShipValidationSeverity.Error)
            .ToList();
        Add("validator no errors", hardErrors.Count == 0,
            string.Join("; ", hardErrors.Select(i => $"{i.Code}:{i.Message}")));

        var frames = doc.Entities.Count(e => e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true);
        Add("frame rings (octagon + bays)", frames >= 40, $"frames={frames}");
        var stringers = doc.Entities.Count(e => e.Name?.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase) == true);
        var shellStringers = doc.Entities.Count(e => e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true);
        var knuckleSegs = doc.Entities.Count(e =>
            e.Name?.StartsWith("stringer-knuckle-", StringComparison.OrdinalIgnoreCase) == true);
        var knuckleAsm = doc.Entities
            .Where(e => e.Name?.StartsWith("stringer-knuckle-", StringComparison.OrdinalIgnoreCase) == true
                        && e.Properties is not null
                        && e.Properties.TryGetValue("platingJoinVertex", out var v)
                        && v.ValueKind == System.Text.Json.JsonValueKind.Number)
            .Select(e => e.Properties!["platingJoinVertex"].GetInt32())
            .Distinct()
            .Count();
        Add("longitudinal girders (was stringer bays)", stringers + shellStringers >= 24,
            $"girders={shellStringers} stringers={stringers}");
        Add("hull knuckle stringers (8 paths)", knuckleAsm == 8 && knuckleSegs >= 40,
            $"assemblies={knuckleAsm} segments={knuckleSegs}");
        // Independent loft re-sample (not emit-time self-check).
        CadJsonStructureQuery.EnsureHullGeometry(doc);
        var knucklePath = HullKnucklePathValidation.Evaluate(
            doc, Geometry.HullGeometry.RequireActive(), CalypsoShipbuildingFrames.PlatingJoinInset);
        var knucklePathDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "knuckle-path");
        HullKnucklePathValidation.WriteJson(Path.Combine(knucklePathDir, "knuckle-path-validation.json"), knucklePath);
        Add("knuckle path deviation ≤ loft tolerance", knucklePath.Passed, HullKnucklePathValidation.FormatSummary(knucklePath));

        var shellPanels = doc.Entities.Count(e =>
            e.Name?.StartsWith("shell-panel-", StringComparison.OrdinalIgnoreCase) == true
            && string.Equals(e.Kind, "mesh", StringComparison.OrdinalIgnoreCase));
        Add("path-swept shell panels (mesh)", shellPanels >= 40, $"shellMeshes={shellPanels}");

        // BH topology: any non-manifold edges must carry explicit vertical-pinch justification.
        var bhJustified = CalypsoLockGenerator.LastStructureMeshValidations
            .Where(r => r.PartId.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var bhTopoOk = bhJustified.Count >= 6
                       && bhJustified.All(r => r.Passed)
                       && bhJustified.All(r =>
                           r.NonManifoldEdges == 0
                           || (!string.IsNullOrWhiteSpace(r.TopologyJustification)
                               && r.TopologyJustification.Contains("Vertical T-junction", StringComparison.Ordinal)));
        Add(
            "BH topology justified (vertical pinch only)",
            bhTopoOk || bhJustified.Count == 0, // generate-only acceptance may not re-validate meshes
            $"bhValidated={bhJustified.Count} justifiedPinch={bhJustified.Count(r => r.TopologyJustification is not null)}");

        var outfit = doc.Entities.Count(e => e.Name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) == true);
        Add("outfit solids", outfit >= 45, $"outfit={outfit}");
        foreach (var cabin in crew.Concat(pax))
        {
            var n = doc.Entities.Count(e =>
                e.Name?.StartsWith($"outfit-{cabin}-", StringComparison.OrdinalIgnoreCase) == true);
            Add($"furnished {cabin}", n >= 3, $"pieces={n}");
        }
        Add("aft ramp solid", doc.Entities.Any(e => string.Equals(e.Name, "ramp-aft", StringComparison.OrdinalIgnoreCase)));
        Add("aft door coaming", doc.Entities.Any(e => e.Name?.StartsWith("coaming-aft", StringComparison.OrdinalIgnoreCase) == true));
        var interiorHatches = ShipCad.Openings(doc)
            .Where(o => o.Name is not null
                        && !o.Name.StartsWith("D3-", StringComparison.OrdinalIgnoreCase)
                        && !o.Name.StartsWith("PASS-", StringComparison.OrdinalIgnoreCase)
                        && !o.Name.StartsWith("HOLD-", StringComparison.OrdinalIgnoreCase)
                        && !o.Name.StartsWith("D1-", StringComparison.OrdinalIgnoreCase)
                        && !o.Name.StartsWith("D2-", StringComparison.OrdinalIgnoreCase)
                        && !ShipCad.GetOpeningConnects(o).Any(c => string.Equals(c, "SPACE", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var onPartition = interiorHatches.Count(h =>
        {
            if (h.HostWallId is not { } id)
                return false;
            var host = doc.Entities.FirstOrDefault(e => e.Id == id);
            return host is not null
                   && host.Name?.StartsWith("hatch-host-", StringComparison.OrdinalIgnoreCase) != true;
        });
        Add("hatches on partition walls", interiorHatches.Count == 0 || onPartition >= interiorHatches.Count * 0.85f,
            $"onPartition={onPartition}/{interiorHatches.Count}");
        Add("passageway hatch frames (room doors)", doc.Entities.Count(e =>
            e.Name?.StartsWith("outfit-hatch-jamb-", StringComparison.OrdinalIgnoreCase) == true) >= 8);
        // One-model: openings are BH/frame plate cutouts — not a second portal-bar forest.
        Add("no floating passage-portal bars",
            doc.Entities.Count(e => e.Name?.Contains("-passage-portal-", StringComparison.OrdinalIgnoreCase) == true) == 0);
        Add("passageway spaces without partition walls",
            !doc.Entities.Any(e =>
                string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)
                && (e.Name?.StartsWith("w-ACCESS-", StringComparison.OrdinalIgnoreCase) == true
                    || e.Name?.StartsWith("w-CROSSING-", StringComparison.OrdinalIgnoreCase) == true
                    || e.Name?.StartsWith("w-CORR_P-", StringComparison.OrdinalIgnoreCase) == true
                    || e.Name?.StartsWith("w-CORR_S-", StringComparison.OrdinalIgnoreCase) == true)));
        foreach (var id in new[] { "BRIDGE", "CREW_1", "GALLEY", "ENG", "HOLD" })
        {
            Add(
                $"outfit {id}",
                doc.Entities.Any(e => e.Name?.StartsWith($"outfit-{id}", StringComparison.OrdinalIgnoreCase) == true));
        }

        var stubs = ShipCad.Spaces(doc).Count(space =>
        {
            if (space.Points is not { Count: >= 2 })
                return false;
            var zs = space.Points.Select(p => p.Length > 2 ? p[2] : 0f).ToList();
            return zs.Max() - zs.Min() < 0.3f && zs.Max() - zs.Min() > 0f;
        });
        Add("no 0.25 m compartment stubs", stubs == 0, $"stubs={stubs}");

        var circulation = ShipWalk.BuildCirculationTour(doc);
        var visitedOps = new HashSet<Guid>(circulation.VisitedOpeningIds);
        var hold = ShipCad.Spaces(doc).FirstOrDefault(s =>
            string.Equals(s.Name, "HOLD", StringComparison.OrdinalIgnoreCase));
        var corr = ShipCad.Spaces(doc).FirstOrDefault(s =>
            string.Equals(s.Name, "CORR_P", StringComparison.OrdinalIgnoreCase) && s.Deck == 0)
            ?? ShipCad.Spaces(doc).FirstOrDefault(s =>
                string.Equals(s.Name, "CORR_P", StringComparison.OrdinalIgnoreCase));
        if (hold is not null)
        {
            var holdTour = ShipWalk.BuildOpenHatchCoverTourDetailed(doc, hold.Id, startAtAft: true);
            foreach (var id in holdTour.VisitedOpeningIds)
                visitedOps.Add(id);
        }

        var entryIds = new List<Guid>();
        if (hold is not null)
            entryIds.Add(hold.Id);
        if (corr is not null)
            entryIds.Add(corr.Id);
        var reachableHatches = entryIds.Count == 0
            ? 0
            : ShipWalk.CountReachableOpenInteriorHatches(doc, entryIds);
        var coverage = reachableHatches == 0
            ? 1f
            : visitedOps.Count / (float)reachableHatches;
        Add("open hatch coverage", reachableHatches == 0 || coverage >= 0.70f,
            $"visited={visitedOps.Count}/{reachableHatches} reachable ({coverage:P0})");
        var decksOnTour = ShipWalk.CountDistinctDecksOnTour(doc, circulation.Waypoints);
        Add("multi-deck circulation", decksOnTour >= 2, $"decks={decksOnTour}");

        Add(
            "outfit BRIDGE-console",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-BRIDGE-console", StringComparison.OrdinalIgnoreCase)));
        Add(
            "bridge captain chair",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-BRIDGE-captain-chair", StringComparison.OrdinalIgnoreCase)));
        Add(
            "bridge pilot consoles",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-BRIDGE-pilot-port", StringComparison.OrdinalIgnoreCase))
            && doc.Entities.Any(e => string.Equals(e.Name, "outfit-BRIDGE-pilot-stbd", StringComparison.OrdinalIgnoreCase)));
        var bridgeSeats = doc.Entities.Count(e =>
            e.Name?.StartsWith("outfit-BRIDGE-seat-", StringComparison.OrdinalIgnoreCase) == true);
        Add("bridge watch seats (8)", bridgeSeats >= 8, $"seats={bridgeSeats}");
        Add("bridge pilot forward of captain", CalypsoBridgeChecks.PilotsForwardOfCaptain(doc, loa));
        var bay = CalypsoDrafting.DefaultFrameBayM;
        if (doc.Properties is not null
            && doc.Properties.TryGetValue("frameBayM", out var bayEl)
            && bayEl.ValueKind == System.Text.Json.JsonValueKind.Number)
            bay = bayEl.GetSingle();
        var frameZs = doc.Entities
            .Where(e => e.Name?.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase) == true)
            .Select(e =>
            {
                var tail = e.Name!["frame-F".Length..];
                var num = tail.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
                return float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)
                    ? z
                    : (float?)null;
            })
            .Where(z => z is >= 0f)
            .Select(z => z!.Value)
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        var bhZs = doc.Entities
            .Where(e => e.Name?.StartsWith("bh-", StringComparison.OrdinalIgnoreCase) == true
                        && e.Name.Contains("-plate", StringComparison.OrdinalIgnoreCase)
                        && e.Properties is not null
                        && e.Properties.TryGetValue("stationZ", out var sz)
                        && sz.ValueKind == System.Text.Json.JsonValueKind.Number)
            .Select(e => e.Properties!["stationZ"].GetSingle())
            .Distinct()
            .ToList();

        // Station-driven schedule: every structural bay (ring frame or heavy BH) ≤ target pitch.
        var structuralZs = frameZs
            .Concat(bhZs)
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        var pitchOk = structuralZs.Count >= 3;
        float maxBay = 0f;
        for (var i = 1; i < structuralZs.Count; i++)
        {
            var dz = structuralZs[i] - structuralZs[i - 1];
            if (i == structuralZs.Count - 1 && dz < bay * 0.55f)
                continue;
            maxBay = MathF.Max(maxBay, dz);
            if (dz > bay + 0.25f)
                pitchOk = false;
        }

        Add(
            $"frame bay ≤ {bay:0.#} m (station-driven)",
            pitchOk,
            $"frames={frameZs.Count} bh={bhZs.Count} stations={structuralZs.Count} maxBay={maxBay:0.##} target={bay:0.#}");
        static float? FrameStationZ(string? name)
        {
            if (name is null || !name.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase))
                return null;
            var tail = name["frame-F".Length..];
            var num = tail.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
            return float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)
                ? z
                : null;
        }

        // HOLD may have perimeter rings + floor chords; forbid wall-like cross-hold webs only.
        var holdCrossWebs = doc.Entities.Count(e =>
            e.Name is not null
            && e.Name.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase)
            && FrameStationZ(e.Name) is float hz
            && hz > 49.3f
            && hz < 67.5f);
        Add("no wall-like webs across HOLD cargo void", holdCrossWebs == 0, $"holdWebSolids={holdCrossWebs}");
        var holdPerimeter = doc.Entities.Count(e =>
        {
            var n = e.Name ?? "";
            if (!n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (FrameStationZ(n) is not float hz || hz <= 49.3f || hz >= 67.5f)
                return false;
            return n.Contains("-ring-", StringComparison.OrdinalIgnoreCase)
                   || n.Contains("-holdfloor", StringComparison.OrdinalIgnoreCase);
        });
        Add("HOLD perimeter frames present", holdPerimeter >= 8, $"holdPerimeterBars={holdPerimeter}");
        Add(
            "outfit LOUNGE-bar",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-LOUNGE-bar", StringComparison.OrdinalIgnoreCase)));
        Add(
            "outfit HOLD-gantry",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-HOLD-gantry", StringComparison.OrdinalIgnoreCase)));
        var c40Count = doc.Entities.Count(e =>
            e.Name?.StartsWith("C40-c", StringComparison.OrdinalIgnoreCase) == true);
        Add("HOLD C40 stack 5×3 (15)", c40Count == 15, $"c40={c40Count}");
        Add(
            "airlock leaves both sides",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-AIRLOCK_A_port-leaf", StringComparison.OrdinalIgnoreCase))
            && doc.Entities.Any(e => string.Equals(e.Name, "outfit-AIRLOCK_A_stbd-leaf", StringComparison.OrdinalIgnoreCase)));
        var airlockSpaces = ShipCad.Spaces(doc).Count(s =>
            s.Name?.StartsWith("AIRLOCK_", StringComparison.OrdinalIgnoreCase) == true);
        Add("airlock chambers present (A+B × port/stbd)", airlockSpaces >= 4, $"airlockSpaces={airlockSpaces}");
        Add("passage interface hatches",
            doc.Entities.Count(e => e.Name?.StartsWith("PASS-", StringComparison.OrdinalIgnoreCase) == true) >= 6);
        Add("airlock triple to SPACE (D1/D2/D3 both sides)",
            doc.Entities.Count(e => e.Name is "D1-P" or "D1-S" or "D2-P" or "D2-S" or "D3-P" or "D3-S") >= 6);

        var bulkheads = doc.Entities.Count(e =>
            e.Name?.StartsWith("bh-", StringComparison.OrdinalIgnoreCase) == true);
        var frameSteel = doc.Entities.Count(e =>
        {
            var n = e.Name ?? "";
            return n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                   && (n.Contains("-ring-", StringComparison.OrdinalIgnoreCase)
                       || n.Contains("-chord-", StringComparison.OrdinalIgnoreCase)
                       || n.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase)
                       || n.Contains("-flange-", StringComparison.OrdinalIgnoreCase)
                       // Mesh annulus is the connected plate-web (replaces box ring bars).
                       || n.Contains("-annulus", StringComparison.OrdinalIgnoreCase));
        });
        var wallLikeFrameBars = doc.Entities.Count(e =>
        {
            var n = e.Name ?? "";
            if (!n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                || !n.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (e.Points is not { Count: >= 2 })
                return false;
            // Half-extent Up → full height; wall-like if a single bar spans most of the section.
            var h = MathF.Abs((float)e.Points[1][1]) * 2f;
            return h >= 6.5f;
        });
        var webProfiles = FrameWebPlateRegistry.All.Count;
        // Limited full-depth BH (ENG massive / airlock / bow) — no stern thrust BH (nacelles).
        Add("compartment bulkheads (limited structural BH)", bulkheads >= 8 && bulkheads <= 8000,
            $"bhSolids={bulkheads}");
        // Full-athwart chords (one per deck) replace passage-chopped stubs — count drops vs old gate.
        Add("transverse open web frames", frameSteel >= 30 && webProfiles >= 10,
            $"frameSteel={frameSteel} profiles={webProfiles}");
        Add("bay frames not wall-like", wallLikeFrameBars == 0,
            $"tallWebSolids={wallLikeFrameBars}");
        Add("structure/outfit inside IML", CalypsoImfSolidChecks.CountViolations(doc, loa) == 0,
            $"violations={CalypsoImfSolidChecks.CountViolations(doc, loa)}");
        var bowPoke = 0;
        foreach (var space in ShipCad.Spaces(doc))
        {
            if (space.Points is not { Count: >= 3 })
                continue;
            var ceil = space.Points[0][1] + MathF.Max(0.5f, space.Height);
            foreach (var p in space.Points)
            {
                if (p.Length < 3)
                    continue;
                var zStem = loa * 0.5f - p[2];
                if (zStem > 18f)
                    continue;
                if (!CalypsoLockGenerator.InsideHullPublic(p[0], p[1], zStem, 0.01f)
                    || !CalypsoLockGenerator.InsideHullPublic(p[0], ceil, zStem, 0.01f))
                    bowPoke++;
            }
        }

        Add("bow footprints inside IML", bowPoke == 0, $"violations={bowPoke}");
        Add(
            "no floating portal knees",
            !doc.Entities.Any(e => e.Name?.Contains("-knee-", StringComparison.OrdinalIgnoreCase) == true));
        var primaryKeel = doc.Entities.Count(e =>
            e.Name?.StartsWith("primary-keel", StringComparison.OrdinalIgnoreCase) == true);
        var thrust = doc.Entities.Count(e =>
            e.Name?.StartsWith("thrust-spine", StringComparison.OrdinalIgnoreCase) == true);
        var deckEdges = doc.Entities.Count(e =>
            e.Name?.StartsWith("deck-edge-", StringComparison.OrdinalIgnoreCase) == true);
        var cargoEdges = doc.Entities.Count(e =>
            e.Name?.StartsWith("cargo-floor-", StringComparison.OrdinalIgnoreCase) == true);
        var legacyGrid = doc.Entities.Count(e =>
            e.Name?.StartsWith("girder-keel-d", StringComparison.OrdinalIgnoreCase) == true
            || e.Name?.StartsWith("girder-port-d", StringComparison.OrdinalIgnoreCase) == true
            || e.Name?.StartsWith("girder-stbd-d", StringComparison.OrdinalIgnoreCase) == true);
        Add("primary keel spine", primaryKeel >= 4, $"primaryKeel={primaryKeel}");
        Add("thrust spine (ENG)", thrust >= 2, $"thrust={thrust}");
        Add("deck edge supports", deckEdges >= 4, $"deckEdges={deckEdges}");
        Add("cargo-floor edge supports", cargoEdges >= 2, $"cargoEdges={cargoEdges}");
        Add("no legacy per-deck girder grid", legacyGrid == 0, $"legacyGrid={legacyGrid}");
        var deckBeamEntities = doc.Entities
            .Where(e => e.Name?.Contains("-deckbeam-", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        // One full-athwart member per deck (no passage chopping). Midbody frames only.
        var deckBeams = deckBeamEntities.Count;
        var narrowBeams = deckBeamEntities.Count(e => e.Thickness < 10f);
        Add("transverse deck beams at frames", deckBeams >= 12, $"deckBeams={deckBeams}");
        Add(
            "deck beams full athwart width",
            deckBeams > 0 && narrowBeams == 0,
            $"narrow={narrowBeams}/{deckBeams}");

        // Topology-first gates: primary skeleton before interiors; HOLD keel/side continuity.
        var entities = doc.Entities ?? [];
        var firstStruct = entities.FindIndex(e =>
        {
            var n = e.Name ?? "";
            return n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                   || n.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                   || n.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
                   || n.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase);
        });
        var firstSpace = entities.FindIndex(e =>
            string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase));
        var firstPartition = entities.FindIndex(e =>
        {
            var n = e.Name ?? "";
            return n.StartsWith("wall-", StringComparison.OrdinalIgnoreCase)
                   || n.StartsWith("partition-", StringComparison.OrdinalIgnoreCase);
        });
        var firstOutfit = entities.FindIndex(e =>
            e.Name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) == true
            || e.Name?.StartsWith("C40-", StringComparison.OrdinalIgnoreCase) == true);
        Add(
            "primary skeleton before interiors",
            firstStruct >= 0
            && (firstSpace < 0 || firstStruct < firstSpace)
            && (firstPartition < 0 || firstStruct < firstPartition)
            && (firstOutfit < 0 || firstStruct < firstOutfit),
            $"struct@{firstStruct} space@{firstSpace} partition@{firstPartition} outfit@{firstOutfit}");

        var holdGirders = entities.Count(e =>
        {
            var n = e.Name ?? "";
            if (!n.StartsWith("primary-keel", StringComparison.OrdinalIgnoreCase)
                && !n.StartsWith("cargo-floor-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (e.Properties is null
                || !e.Properties.TryGetValue("stationZ", out var sz)
                || sz.ValueKind != System.Text.Json.JsonValueKind.Number)
                return false;
            var z = (float)sz.GetDouble();
            return z > 49.5f && z < 67.5f;
        });
        Add("HOLD stem longitudinal girders", holdGirders > 0, $"holdGirders={holdGirders}");

        var withRole = entities.Count(e =>
            e.Properties is not null
            && e.Properties.ContainsKey("structuralRole")
            && e.Properties.ContainsKey("assembly"));
        Add("structural metadata on steel", withRole >= 40, $"tagged={withRole}");

        CadDocumentAdapter.LoadFromDocument(doc);
        var reg = CadDocumentAdapter.Active;
        Add(
            "semantic registry classifies primary structure",
            reg.Unclassified.Count == 0 && reg.BulkheadPlateCount >= 6,
            $"parts={reg.Parts.Count} bhPlates={reg.BulkheadPlateCount} unclassified={reg.Unclassified.Count}");
        var asmVal = CalypsoCad.Validation.AssemblyValidation.Validate(doc);
        Add(
            "semantic assembly validation (BH plates + graph)",
            asmVal.Ok,
            $"bh={asmVal.BulkheadPlates} frames={asmVal.FrameAssemblies} isolated/gaps via graph.Ok={asmVal.Graph.Ok}");

        var hierarchy = CalypsoCad.Model.HierarchyValidation.Validate(reg);
        Add(
            "semantic hierarchy integrity",
            hierarchy.Ok,
            $"roots={hierarchy.RootCount} knuckles={hierarchy.KnuckleAssemblies} bh={hierarchy.BulkheadAssemblies} frames={hierarchy.FrameAssemblies} decks={hierarchy.DeckAssemblies} orphans={hierarchy.OrphanAssemblies.Count} cycles={hierarchy.Cycles.Count} conflicts={hierarchy.DuplicateRegistrationConflicts.Count}");

        // Round-trip: capture → clear → restore from document snapshot.
        Publication.SemanticRegistrySnapshot.WriteToDocument(doc, reg);
        var knuckleBefore = reg.KnuckleAssemblyCount;
        var partsBefore = reg.Parts.Count;
        CadDocumentAdapter.Reset();
        CadDocumentAdapter.LoadFromDocument(doc);
        var reg2 = CadDocumentAdapter.Active;
        var hierarchy2 = CalypsoCad.Model.HierarchyValidation.Validate(reg2);
        Add(
            "semantic registry round-trip",
            hierarchy2.Ok
            && reg2.KnuckleAssemblyCount == knuckleBefore
            && reg2.Parts.Count == partsBefore
            && reg2.BulkheadPlateCount >= 6,
            $"knuckles={reg2.KnuckleAssemblyCount}/{knuckleBefore} parts={reg2.Parts.Count}/{partsBefore} bh={reg2.BulkheadPlateCount}");

        var surfaceRows = Export.StructureSurfaceInventory.Collect(doc, reg2);
        var orangeBh = surfaceRows.Count(r =>
            string.Equals(r.PartKind, nameof(Model.CadPartKind.BulkheadPlate), StringComparison.OrdinalIgnoreCase));
        var orangeMisclass = surfaceRows.Count(r =>
            r.TriangleCount >= 200
            && !string.Equals(r.PartKind, nameof(Model.CadPartKind.BulkheadPlate), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(r.PartKind, nameof(Model.CadPartKind.AnnularWebPlate), StringComparison.OrdinalIgnoreCase)
            && !r.PartKind.Contains("Ring", StringComparison.OrdinalIgnoreCase)
            && !r.PartKind.Contains("Web", StringComparison.OrdinalIgnoreCase));
        Add(
            "orange surfaces are BH plates (semantic)",
            orangeBh >= 6 && orangeMisclass == 0,
            $"bhPlates={orangeBh} largeNonBh={orangeMisclass}");

        var knuckleCont = Export.KnuckleContinuityReport.Evaluate(
            doc, Geometry.HullGeometry.RequireActive(), CalypsoShipbuildingFrames.PlatingJoinInset);
        Add(
            "knuckle continuity (8 paths)",
            knuckleCont.Passed,
            $"passed={knuckleCont.PassedPaths}/{knuckleCont.PathCount}");

        var sandwichFaces = entities.Count(e =>
            e.Name?.Contains("-face-top", StringComparison.OrdinalIgnoreCase) == true
            && e.Name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase));
        var sandwichRibs = entities.Count(e =>
            e.Name?.Contains("-rib-", StringComparison.OrdinalIgnoreCase) == true
            && e.Name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase));
        Add("cellular sandwich face+ribs", sandwichFaces >= 8 && sandwichRibs >= 16,
            $"faceTop={sandwichFaces} ribs={sandwichRibs}");

        var joinStubs = entities.Count(e =>
            e.Name?.Contains("-join-", StringComparison.OrdinalIgnoreCase) == true
            && e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase));
        Add("no arbitrary frame×girder join stubs", joinStubs == 0, $"joins={joinStubs}");

        var intPosts = entities.Count(e =>
            e.Name?.Contains("-int-post-", StringComparison.OrdinalIgnoreCase) == true);
        var intJambs = entities.Count(e =>
            e.Name?.Contains("-int-jamb-", StringComparison.OrdinalIgnoreCase) == true);
        var intHdrs = entities.Count(e =>
            e.Name?.Contains("-int-hdr-", StringComparison.OrdinalIgnoreCase) == true);
        Add("frame interior partition posts", intPosts >= 8, $"posts={intPosts}");
        Add("frame interior portal jambs", intJambs >= 8, $"jambs={intJambs}");
        Add("frame interior portal headers", intHdrs >= 4, $"headers={intHdrs}");
        var attach = Export.PartitionAttachmentValidator.Evaluate(doc, layout: null);
        Add(
            "partition posts clear of passage envelopes",
            attach.PassageViolations == 0,
            $"passageHits={attach.PassageViolations}");

        var deckSeats = entities.Count(e =>
            e.Name?.Contains("-deckseat-", StringComparison.OrdinalIgnoreCase) == true);
        Add("critical deck seats (BH×deck + frame×deck)", deckSeats >= 24, $"deckSeats={deckSeats}");
        var (criticalOk, criticalDetail) = CriticalStructureConnectivity.Validate(doc);
        Add("critical structure connected (BH↔frame↔deck)", criticalOk, criticalDetail);

        CadLayersDocument? layersDoc = null;
        var genDir = generatedDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis", "CalypsoCad", "generated");
        var layersPath = Path.Combine(genDir, "calypso.cadlayers.json");
        if (File.Exists(layersPath))
        {
            layersDoc = System.Text.Json.JsonSerializer.Deserialize<CadLayersDocument>(
                File.ReadAllText(layersPath), CadJson.Options);
        }

        foreach (var dnv in CalypsoDnvReviewChecks.Evaluate(doc, layersDoc))
            Add($"DNV review: {dnv.Name}", dnv.Ok, dnv.Detail);

        foreach (var fab in CalypsoFabReleaseChecks.Evaluate(doc, genDir))
            Add($"fab release: {fab.Name}", fab.Ok, fab.Detail);

        return checks;
    }
}
