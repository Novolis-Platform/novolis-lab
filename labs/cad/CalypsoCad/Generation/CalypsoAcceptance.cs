using System.Numerics;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;
using Novolis.Ship.Topology;
using Novolis.Ship.Validation;

namespace CalypsoCad.Generation;

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
        Add("longitudinal girders (was stringer bays)", stringers + shellStringers >= 24, $"girders={shellStringers} legacyStringers={stringers}");
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
            .Where(o => o.Name is not null && !o.Name.StartsWith("D3-", StringComparison.OrdinalIgnoreCase)
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
        Add("passageway hatch frames", doc.Entities.Count(e =>
            e.Name?.StartsWith("outfit-hatch-jamb-", StringComparison.OrdinalIgnoreCase) == true) >= 12);
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
            .Where(z => z is >= 17f and <= 65f)
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        var uniform = true;
        for (var i = 1; i < frameZs.Count; i++)
        {
            if (MathF.Abs(frameZs[i]!.Value - frameZs[i - 1]!.Value - 4f) > 0.25f)
                uniform = false;
        }

        Add("uniform frame bay 4 m (midbody)", frameZs.Count < 2 || uniform, $"frames={frameZs.Count}");
        Add(
            "outfit LOUNGE-bar",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-LOUNGE-bar", StringComparison.OrdinalIgnoreCase)));
        Add(
            "outfit HOLD-gantry",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-HOLD-gantry", StringComparison.OrdinalIgnoreCase)));
        Add(
            "airlock leaves both sides",
            doc.Entities.Any(e => string.Equals(e.Name, "outfit-AIRLOCK_A_port-leaf", StringComparison.OrdinalIgnoreCase))
            && doc.Entities.Any(e => string.Equals(e.Name, "outfit-AIRLOCK_A_stbd-leaf", StringComparison.OrdinalIgnoreCase)));

        var webPlates = doc.Entities.Count(e =>
            e.Name?.Contains("-webplate-", StringComparison.OrdinalIgnoreCase) == true);
        Add("transverse frame web plates", webPlates >= 60, $"webplates={webPlates}");
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
        var girders = doc.Entities.Count(e =>
            e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true);
        Add("deck longitudinal girders", girders >= 24, $"girders={girders}");
        var deckBeams = doc.Entities.Count(e =>
            e.Name?.Contains("-deckbeam-", StringComparison.OrdinalIgnoreCase) == true);
        Add("transverse deck beams at frames", deckBeams >= 30, $"deckBeams={deckBeams}");

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
