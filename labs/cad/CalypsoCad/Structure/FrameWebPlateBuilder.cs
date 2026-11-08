using CalypsoCad.Session;
using Novolis.Ship.Structure;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Transverse CNC plate-web frames (primary skeleton) and limited full-depth bulkheads.
/// Both: full plate → boolean passageway/lightening → dense remnant. See
/// calypso-structural-construction.
/// </summary>
internal static class FrameWebPlateBuilder
{
    private const float ShellDoorBandUp0 = 3.6f;
    private const float ShellDoorBandUp1 = 7.4f;
    private const float ShellDoorReliefM = 1.15f;
    /// <summary>Scan step for plate voxels — continuous plate-web read.</summary>
    private const float PlateScan = 0.32f;
    private const float WebClearance = 0.28f;
    private const float BowExtraClearance = 0.18f;
    /// <summary>Shirt-sleeves portal clear height (m) for passageway track cuts.</summary>
    private const float PassagePortalClearH = 2.15f;

    /// <summary>Rectangular cut through a web/BH: athwart Y × elevation Up (sill/head).</summary>
    internal readonly record struct WebPlatePortal(float Y0, float Y1, float U0, float U1);

    /// <summary>
    /// Open transverse web frame: fab plate with large clearances; viz = perimeter ring + deck chords
    /// + narrow residual webs. Not a wall with door slits.
    /// </summary>
    public static void Emit(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned,
        IReadOnlyList<float>? deckUps = null,
        IReadOnlyList<WebPlatePortal>? hatchPortals = null)
    {
        if (!TryPreparePlate(
                zStem, web, bowAligned, deckUps, athwartGaps, hatchPortals,
                out var inset, out var upMin, out var upMax, out var mid, out var ring,
                out var gaps, out var decks, out var portals, out var portalBands))
            return;

        var fabPartId = FabPartId(frameId, "P-FRAME");
        // Open-frame: passageway clearances only — no lightening swiss-cheese (one-model: fab ≡ viz).
        var holeLoops = BuildHoles(
            gaps, portals, portalBands, upMin, upMax, zStem, inset, decks, 0f, bowAligned,
            skipLightening: true, openWebFrame: true);
        RegisterFabProfile(fabPartId, ring, holeLoops, zStem, web);

        EmitOpenWebFrameViz(
            entities, loa, frameId, zStem, web, halfDepthZ,
            upMin, upMax, mid, ring, gaps, decks, holeLoops, fabPartId, inset, bowAligned);
    }

    /// <summary>
    /// Pressure bulkhead: one extruded polygonal plate (outer IML ring − openings).
    /// Fabrication profile and rendered solid share the same boundary — no AABB voxel soup.
    /// </summary>
    public static void EmitBulkhead(
        List<CadEntity> entities,
        float loa,
        string bulkheadId,
        float zStem,
        float thickness,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned,
        IReadOnlyList<float>? deckUps = null,
        bool skipLightening = false,
        bool useCoarseSpans = false,
        IReadOnlyList<WebPlatePortal>? hatchPortals = null)
    {
        _ = skipLightening;
        _ = useCoarseSpans;
        if (!TryPreparePlate(
                zStem, thickness, bowAligned, deckUps, athwartGaps, hatchPortals,
                out var inset, out var upMin, out var upMax, out _, out var ring,
                out var gaps, out var decks, out var portals, out var portalBands))
            return;

        var fabPartId = FabPartId(bulkheadId, "P-BH");
        var holeLoops = BuildHoles(
            gaps, portals, portalBands, upMin, upMax, zStem, inset, decks, 0f, bowAligned,
            skipLightening: true, openWebFrame: false);
        RegisterFabProfile(fabPartId, ring, holeLoops, zStem, thickness);

        var outer = ring.Select(v => (v.Y, v.Up)).ToList();
        var holes = holeLoops
            .Select(h => (IReadOnlyList<(float Y, float Up)>)h.Select(p => (p.X, p.Y)).ToList())
            .ToList();
        var zCad = loa * 0.5f - zStem;
        EditableMesh mesh;
        PolygonWithHolesExtrude.ExtrudeReport extrudeReport;
        try
        {
            mesh = PolygonWithHolesExtrude.ExtrudeAlongZ(
                outer, holes, zCad, halfDepthZ, bulkheadId, out extrudeReport);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL {bulkheadId} polygon extrude: {ex.Message}");
            StructurePlacementLog.Record(
                bulkheadId, 0f, 0f, zStem, 0f, 0f, halfDepthZ,
                $"polygon extrude failed: {ex.Message}");
            return;
        }

        CalypsoLockGenerator.TryAddStructureMesh(
            entities,
            loa,
            $"{bulkheadId}-plate",
            zStem,
            mesh,
            fabPartId,
            extrudeReport: extrudeReport,
            expectedOpenings: extrudeReport.ExpectedOpenings);
    }

    private static string SanitizeFileToken(string id) =>
        string.Concat(id.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));

    private static bool TryPreparePlate(
        float zStem,
        float web,
        bool bowAligned,
        IReadOnlyList<float>? deckUps,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<WebPlatePortal>? hatchPortals,
        out float inset,
        out float upMin,
        out float upMax,
        out float mid,
        out (float Y, float Up)[] ring,
        out List<(float Y0, float Y1)> gaps,
        out List<float> decks,
        out List<WebPlatePortal> portals,
        out List<(float U0, float U1)> portalBands)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out _, out var halfH, out var sectionMid, out _, out _);
        var skinInset = CalypsoShipbuildingFrames.DefaultSkinInset
                        + WebClearance
                        + (bowAligned ? BowExtraClearance : 0f);
        var u0 = sectionMid - halfH + skinInset + web * 0.5f;
        var u1 = sectionMid + halfH - skinInset - web * 0.5f;
        mid = sectionMid;
        inset = skinInset;
        upMin = u0;
        upMax = u1;
        ring = [];
        gaps = [];
        decks = [];
        portals = [];
        portalBands = [];
        if (u1 <= u0 + 0.2f)
            return false;

        // Keep the authoritative section in absolute CAD Y/Up — do not recenter.
        // Recenter would detach fab/viz from HullSectionAuthority and break contour checks.
        ring = HullSectionAuthority.InnerRing(zStem, skinInset);
        gaps = NormalizeGaps(athwartGaps, zStem, skinInset, bowAligned);
        var deckList = new List<float>();
        var sources = deckUps is { Count: > 0 }
            ? deckUps
            : (IReadOnlyList<float>)[0.5f, 4.4f, 8.0f];
        foreach (var u in sources)
        {
            if (u > u0 + 0.25f && u < u1 - 0.25f)
                deckList.Add(u);
        }

        deckList.Sort();
        decks = deckList;
        portals = NormalizePortals(hatchPortals, zStem, skinInset, bowAligned);
        portalBands = BuildPassagePortalBands(u0, u1, decks);
        return true;
    }

    private static void RegisterFabProfile(
        string fabPartId,
        (float Y, float Up)[] ring,
        List<List<(float X, float Y)>> holeLoops,
        float zStem,
        float web)
    {
        var outerLoop = ring.Select(v => (v.Y, v.Up)).ToList();
        var outerArea = HullFaceUnfolder.PolygonAreaM2(outerLoop);
        var holeArea = holeLoops.Sum(h => HullFaceUnfolder.PolygonAreaM2(h));
        var netArea = MathF.Max(0.01f, outerArea - holeArea);
        FrameWebPlateRegistry.Register(
            fabPartId,
            new FrameWebPlateRegistry.Entry(
                new FabCutProfile
                {
                    PartId = fabPartId,
                    OuterLoop = outerLoop,
                    Holes = holeLoops.Select(h => h.ToList()).ToList(),
                    BendAllowed = false,
                    AreaM2 = netArea,
                },
                zStem,
                web,
                "CAL-INT-GA-001.json"));
    }

    /// <summary>
    /// Physical annular plate-web from the fab outer ring (hull-conforming) + inward web offset.
    /// Same geometry as the fabrication profile outer boundary.
    /// </summary>
    private static void EmitAnnularFrameFromRing(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        (float Y, float Up)[] outerRing,
        string fabPartId,
        float structuralInset)
    {
        if (outerRing.Length < 3)
            return;
        var webWidth = MathF.Max(0.08f, web);
        var inner = PolygonInset2D.Inset(outerRing, webWidth);
        if (inner.Length != outerRing.Length)
        {
            StructurePlacementLog.Record(
                frameId, 0f, 0f, zStem, 0f, 0f, halfDepthZ,
                "annular frame inner offset collapsed");
            return;
        }

        var zCad = loa * 0.5f - zStem;
        EditableMesh mesh;
        try
        {
            mesh = AnnularPlateExtrude.ExtrudeAlongZ(
                outerRing, inner, zCad, halfDepthZ, frameId);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL {frameId} annular frame: {ex.Message}");
            StructurePlacementLog.Record(
                frameId, 0f, 0f, zStem, 0f, 0f, halfDepthZ,
                $"annular frame failed: {ex.Message}");
            return;
        }

        // Emit-time contour check: outerRing is already the geometric inset of the hull section.
        var contourMm = HullSectionAuthority.MaxRingDeviation(outerRing, zStem, structuralInset) * 1000f;
        FrameContourEmitLog.Record(frameId, zStem, structuralInset, contourMm, mesh.TriangleCount, mesh.VertexCount);

        CalypsoLockGenerator.TryAddStructureMesh(
            entities, loa, $"{frameId}-annulus", zStem, mesh, fabPartId,
            meta: new StructuralSolidMeta(
                StructuralRoles.TransverseFrame,
                frameId.Contains('-')
                    ? string.Join('-', frameId.Split('-').Take(2))
                    : frameId,
                StationZ: zStem));
    }

    /// <summary>
    /// Viz for open web frames: annular plate-web + deck chords split at passageway tracks.
    /// Clearances stay empty — no portal-bar second model (one steel story).
    /// </summary>
    private static void EmitOpenWebFrameViz(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float upMin,
        float upMax,
        float mid,
        (float Y, float Up)[] ring,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<float> decks,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        string fabPartId,
        float inset,
        bool bowAligned)
    {
        _ = mid;
        _ = upMin;
        _ = upMax;
        _ = holeLoops;
        _ = inset;
        // 1) Connected annular plate-web from the SAME outer ring as the fab profile.
        //    Do not rebuild an independent bar ring — fab ≡ viz geometry.
        EmitAnnularFrameFromRing(
            entities, loa, frameId, zStem, web, halfDepthZ, ring, fabPartId, inset);

        // 2) Deck chords — full athwart load path at lock plate tops.
        //    Passage openings belong in the web plate portals, not as chord gaps.
        _ = athwartGaps;
        _ = bowAligned;
        var chordHalf = MathF.Max(0.08f, web * 0.45f);
        var chordInset = CalypsoShipbuildingFrames.PlatingJoinInset;
        foreach (var deckUp in decks)
        {
            // Nest half-beam across the chord flange height so corners stay inside the IML.
            var halfY = MathF.Min(
                CalypsoLockGenerator.HalfBeamInsidePublic(zStem, deckUp, chordInset),
                MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, deckUp - chordHalf, chordInset),
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, deckUp + chordHalf, chordInset))) - 0.15f;
            if (halfY < 0.35f)
                continue;
            var asm = frameId.Contains('-')
                ? string.Join('-', frameId.Split('-').Take(2))
                : frameId;
            var chordName = $"{frameId}-chord-{deckUp:0.#}";
            PlaceChordExact(entities, loa, chordName, 0f, deckUp, zStem, halfY, chordHalf, halfDepthZ, asm, frameId);
        }
    }

    /// <summary>
    /// Place a CL-centered full-athwart deck chord. Half-span is pre-nested to the IML;
    /// on BoxFits failure only a small end trim is allowed (never passage chopping).
    /// </summary>
    private static void PlaceChordExact(
        List<CadEntity> entities,
        float loa,
        string chordName,
        float cy,
        float deckUp,
        float zStem,
        float hy,
        float chordHalf,
        float halfDepthZ,
        string assembly,
        string frameId)
    {
        var tryHy = hy;
        for (var step = 0; step < 8; step++)
        {
            if (CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, chordName, cy, deckUp, zStem, tryHy, chordHalf, halfDepthZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.TransverseFrame, assembly,
                        StationZ: zStem, DeckUp: deckUp, Joins: [frameId])))
                return;
            // Tiny end trim to clear chamfer — keep one continuous member.
            tryHy *= 0.97f;
            if (tryHy < 0.35f)
                return;
        }
    }

    /// <summary>
    /// Emit only thin residual strips that survive after large open-frame holes (max ~0.55 m wide).
    /// Wide “solid” spans are forced open — walls are forbidden for bay frames.
    /// </summary>
    private static void EmitResidualWebStrips(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float upMin,
        float upMax,
        (float Y, float Up)[] ring,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<float> decks,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        float inset,
        bool bowAligned,
        ref int cellIndex)
    {
        // Perimeter-adjacent residual only — wide remnants read as walls.
        const float MaxResidualWidth = 0.40f;
        var bands = BuildElevationBands(upMin, upMax, decks);
        if (bands.Count == 0)
            bands.Add((upMin, upMax));

        foreach (var (u0, u1) in bands)
        {
            var uMid = (u0, u1).GetCenter();
            var hu = (u1 - u0) * 0.48f;
            if (hu < 0.12f)
                continue;
            var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, uMid, inset) - 0.12f;
            if (halfY < 0.25f)
                continue;

            // Holes overlapping this band → Y gaps for residual steel.
            var yGaps = new List<(float Y0, float Y1)>(athwartGaps);
            foreach (var hole in holeLoops)
            {
                if (hole.Count < 3)
                    continue;
                var minY = hole.Min(p => p.X);
                var maxY = hole.Max(p => p.X);
                var minU = hole.Min(p => p.Y);
                var maxU = hole.Max(p => p.Y);
                if (maxU < u0 + 0.05f || minU > u1 - 0.05f)
                    continue;
                yGaps.Add((minY, maxY));
            }

            yGaps = MergeGaps(yGaps);
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, yGaps))
            {
                var width = yb - ya;
                if (width < 0.18f)
                    continue;
                // Wide remnant = failed open-frame; keep only outboard/inboard edge strips.
                if (width > MaxResidualWidth)
                {
                    EmitResidualEdge(
                        entities, loa, frameId, ref cellIndex, ya, ya + MaxResidualWidth * 0.9f,
                        uMid, hu, halfDepthZ, zStem, ring, holeLoops, bowAligned);
                    EmitResidualEdge(
                        entities, loa, frameId, ref cellIndex, yb - MaxResidualWidth * 0.9f, yb,
                        uMid, hu, halfDepthZ, zStem, ring, holeLoops, bowAligned);
                    continue;
                }

                EmitResidualEdge(
                    entities, loa, frameId, ref cellIndex, ya, yb,
                    uMid, hu, halfDepthZ, zStem, ring, holeLoops, bowAligned);
            }
        }

        _ = web;
    }

    private static void EmitResidualEdge(
        List<CadEntity> entities,
        float loa,
        string frameId,
        ref int cellIndex,
        float y0,
        float y1,
        float uMid,
        float hu,
        float halfDepthZ,
        float zStem,
        (float Y, float Up)[] ring,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        bool bowAligned)
    {
        var width = y1 - y0;
        if (width < 0.16f)
            return;
        var cy = (y0, y1).GetCenter();
        var hy = width * 0.48f;
        if (bowAligned && cy < -0.02f)
            return;
        // Reject if footprint center is inside a clearance (absent material).
        if (CenterInAnyHole(cy, uMid, holeLoops))
            return;
        if (!TryEmitPlateVoxel(
                entities, loa, frameId, ref cellIndex, fabPartId: "",
                cy, uMid, hy, hu, halfDepthZ, zStem, ring, holeLoops, coarse: true))
            return;
        if (bowAligned && cy > 0.02f)
        {
            TryEmitPlateVoxel(
                entities, loa, frameId, ref cellIndex, fabPartId: "",
                -cy, uMid, hy, hu, halfDepthZ, zStem, ring, holeLoops, coarse: true);
        }
    }

    private static List<(float Y0, float Y1)> NormalizeGaps(
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        float zStem,
        float inset,
        bool bowAligned)
    {
        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, 6f, inset);
        var list = new List<(float Y0, float Y1)>();
        foreach (var (y0, y1) in athwartGaps)
        {
            var a = MathF.Max(y0, -halfY);
            var b = MathF.Min(y1, halfY);
            if (b <= a + 0.08f)
                continue;
            // Leave web remnant outboard so the station still reads as a ring.
            // ≥1.8 m total (0.9 m/side) so pressure-BH wings survive shell-door relief.
            var maxGap = halfY * 2f - 1.8f;
            if (b - a > maxGap && maxGap > 0.8f)
            {
                var c = (a, b).GetCenter();
                a = c - maxGap * 0.5f;
                b = c + maxGap * 0.5f;
            }

            list.Add((a, b));
        }

        var merged = MergeGaps(list);
        // Bow: force every cut onto the spine so the plate stays centered.
        if (bowAligned)
        {
            for (var i = 0; i < merged.Count; i++)
                merged[i] = CalypsoDrafting.CenterAthwartOnSpine(merged[i].Y0, merged[i].Y1);
            merged = MergeGaps(merged);
        }

        return merged;
    }

    private static List<WebPlatePortal> NormalizePortals(
        IReadOnlyList<WebPlatePortal>? hatchPortals,
        float zStem,
        float inset,
        bool bowAligned)
    {
        if (hatchPortals is null || hatchPortals.Count == 0)
            return [];

        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, 6f, inset);
        var list = new List<WebPlatePortal>();
        foreach (var p in hatchPortals)
        {
            var a = MathF.Max(p.Y0, -halfY);
            var b = MathF.Min(p.Y1, halfY);
            if (b <= a + 0.08f || p.U1 <= p.U0 + 0.4f)
                continue;
            if (bowAligned)
            {
                var (cy0, cy1) = CalypsoDrafting.CenterAthwartOnSpine(a, b);
                a = cy0;
                b = cy1;
            }

            list.Add(new WebPlatePortal(a, b, p.U0, p.U1));
        }

        return list;
    }

    /// <summary>
    /// Coarse plate as one ship-height web with portal cutouts (door-in-wall), not deck-high stubs.
    /// Wings run <paramref name="upMin"/>→<paramref name="upMax"/>; portal columns keep sill/lintel steel.
    /// </summary>
    private static void EmitPlateSpans(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float upMin,
        float upMax,
        (float Y, float Up)[] ring,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<WebPlatePortal> hatchPortals,
        IReadOnlyList<(float U0, float U1)> portalBands,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        string fabPartId,
        float inset,
        bool bowAligned,
        IReadOnlyList<float> decks,
        bool skipShellDoor = false)
    {
        _ = web;
        _ = decks;
        var uMidShip = (upMin, upMax).GetCenter();
        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, uMidShip, inset) - 0.12f;
        if (halfY < 0.2f || upMax <= upMin + 0.3f)
            return;

        // All athwart portal tracks (passage + hatch Y) — shell-door relief stays height-limited.
        var yCuts = MergeGaps(athwartGaps.ToList());
        foreach (var p in hatchPortals)
            yCuts.Add((p.Y0, p.Y1));
        yCuts = MergeGaps(yCuts);

        var cellIndex = 0;

        // 1) Full-height wings / inter-portal columns (no doorway in this Y).
        foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, yCuts))
        {
            EmitCoarseBar(
                entities, loa, frameId, ref cellIndex, fabPartId,
                ya, yb, upMin, upMax, halfDepthZ, zStem, ring, holeLoops, bowAligned);
        }

        // 2) Portal columns: solid only outside each doorway (sill / inter-deck / lintel).
        foreach (var (y0, y1) in yCuts)
        {
            if (y1 <= y0 + 0.08f)
                continue;
            var uHoles = new List<(float Y0, float Y1)>();
            foreach (var (pu0, pu1) in portalBands)
            {
                if (pu1 > pu0 + 0.4f)
                    uHoles.Add((pu0, pu1));
            }

            foreach (var p in hatchPortals)
            {
                // Hatch only punches its own Y column.
                if (p.Y1 < y0 - 0.05f || p.Y0 > y1 + 0.05f)
                    continue;
                uHoles.Add((p.U0, p.U1));
            }

            if (!bowAligned && !skipShellDoor)
            {
                // Shell-door relief is a side notch, not a full portal column.
            }

            uHoles = MergeGaps(uHoles);
            foreach (var (ua, ub) in CalypsoLockGenerator.SolidSpansPublic(upMin, upMax, uHoles))
            {
                EmitCoarseBar(
                    entities, loa, frameId, ref cellIndex, fabPartId,
                    y0, y1, ua, ub, halfDepthZ, zStem, ring, holeLoops, bowAligned);
            }
        }

        // 3) Optional shell-door notches on the outer wings (frames only).
        if (!bowAligned && !skipShellDoor)
        {
            var doorGaps = new List<(float Y0, float Y1)>
            {
                (halfY - ShellDoorReliefM, halfY + 0.05f),
                (-halfY - 0.05f, -halfY + ShellDoorReliefM),
            };
            // Already omitted from full-height wings via yCuts? Not in yCuts — punch by not
            // emitting wing into those Y at door band. Wings are full-height; subtract via
            // skipping door-Y from wing emit would gut the wing. Leave as lightening in fab only.
            _ = doorGaps;
        }
    }

    private static void EmitCoarseBar(
        List<CadEntity> entities,
        float loa,
        string frameId,
        ref int cellIndex,
        string fabPartId,
        float y0,
        float y1,
        float u0,
        float u1,
        float halfDepthZ,
        float zStem,
        (float Y, float Up)[] ring,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        bool bowAligned)
    {
        var width = y1 - y0;
        if (width < 0.25f)
            return;
        var cy = (y0, y1).GetCenter();
        // Outboard AABB corners poke the octagon when using ship-mid up span — clip U to local chord.
        if (TryOctagonUpSpanAtY(ring, cy, out var localU0, out var localU1))
        {
            u0 = MathF.Max(u0, localU0 + 0.04f);
            u1 = MathF.Min(u1, localU1 - 0.04f);
        }

        var height = u1 - u0;
        if (height < 0.2f)
            return;
        var cu = (u0, u1).GetCenter();
        var hy = width * 0.5f;
        var hu = height * 0.5f;
        if (bowAligned && cy < -0.02f)
            return;
        if (!TryEmitPlateVoxel(
                entities, loa, frameId, ref cellIndex, fabPartId,
                cy, cu, hy, hu, halfDepthZ, zStem, ring, holeLoops, coarse: true))
            return;
        if (bowAligned && cy > 0.02f)
        {
            TryEmitPlateVoxel(
                entities, loa, frameId, ref cellIndex, fabPartId,
                -cy, cu, hy, hu, halfDepthZ, zStem, ring, holeLoops, coarse: true);
        }
    }

    /// <summary>Vertical chord of the IML octagon at athwart <paramref name="y"/>.</summary>
    private static bool TryOctagonUpSpanAtY((float Y, float Up)[] ring, float y, out float u0, out float u1)
    {
        u0 = float.PositiveInfinity;
        u1 = float.NegativeInfinity;
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var dy = b.Y - a.Y;
            if (MathF.Abs(dy) < 1e-5f)
                continue;
            var t = (y - a.Y) / dy;
            if (t < -0.02f || t > 1.02f)
                continue;
            var u = a.Up + t * (b.Up - a.Up);
            u0 = MathF.Min(u0, u);
            u1 = MathF.Max(u1, u);
        }

        return u1 > u0 + 0.3f;
    }

    /// <summary>
    /// Dense Y×Up scan of the plate: abutting voxels → one steel web with holes cut through.
    /// </summary>
    private static void EmitPlateScan(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float upMin,
        float upMax,
        (float Y, float Up)[] ring,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<WebPlatePortal> hatchPortals,
        IReadOnlyList<(float U0, float U1)> portalBands,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        string fabPartId,
        float inset,
        bool bowAligned,
        bool skipShellDoor = false)
    {
        var cellIndex = 0;
        var halfCell = PlateScan * 0.48f;
        for (var up = upMin; up < upMax - 0.05f; up += PlateScan)
        {
            var up1 = MathF.Min(upMax, up + PlateScan);
            var uMid = (up, up1).GetCenter();
            var hu = (up1 - up) * 0.48f;
            if (hu < 0.05f)
                continue;

            // Narrowest half-beam across the scan row so AABB corners stay inside the taper.
            var halfY = MathF.Min(
                CalypsoLockGenerator.HalfBeamInsidePublic(zStem, up, inset),
                MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, uMid, inset),
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, up1, inset)));
            halfY -= web * 0.55f;
            if (halfY < 0.15f)
                continue;

            var bandGaps = GapsForBand(
                athwartGaps, hatchPortals, portalBands, uMid, halfY, bowAligned, skipShellDoor);
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, bandGaps))
            {
                for (var y = ya; y < yb - 0.04f; y += PlateScan)
                {
                    var y1 = MathF.Min(yb, y + PlateScan);
                    var cy = (y, y1).GetCenter();
                    var hy = MathF.Min(halfCell, (y1 - y) * 0.48f);
                    if (hy < 0.05f || hu < 0.05f)
                        continue;
                    // Bow bulkheads: emit stbd half only, then mirror — hard spine symmetry.
                    if (bowAligned && cy < -0.02f)
                        continue;
                    if (!TryEmitPlateVoxel(
                            entities, loa, frameId, ref cellIndex, fabPartId,
                            cy, uMid, hy, hu, halfDepthZ, zStem, ring, holeLoops))
                        continue;
                    if (bowAligned && cy > 0.02f)
                    {
                        TryEmitPlateVoxel(
                            entities, loa, frameId, ref cellIndex, fabPartId,
                            -cy, uMid, hy, hu, halfDepthZ, zStem, ring, holeLoops);
                    }
                }
            }
        }
    }

    private static bool TryEmitPlateVoxel(
        List<CadEntity> entities,
        float loa,
        string frameId,
        ref int cellIndex,
        string fabPartId,
        float cy,
        float uMid,
        float hy,
        float hu,
        float halfDepthZ,
        float zStem,
        (float Y, float Up)[] ring,
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        bool coarse = false)
    {
        if (CenterInAnyHole(cy, uMid, holeLoops))
            return false;
        if (!CalypsoLockGenerator.InsideHullPublic(cy, uMid, zStem, -0.05f))
            return false;

        if (coarse)
        {
            // Center-only nest: corner rejection stair-steps the IML into voxel soup.
            // Coarse bars are already clamped by HalfBeamInside; slight taper overshoot is OK.
        }
        else
        {
            if (!CellInsideOctagon(cy, uMid, hy, hu, ring))
                return false;
            if (!CornersInsideHull(cy, uMid, hy, hu, halfDepthZ, zStem))
                return false;
        }

        CalypsoLockGenerator.TryAddStructureBar(
            entities,
            loa,
            $"{frameId}-web-solid-{cellIndex++}",
            cy,
            uMid,
            zStem,
            hy,
            hu,
            halfDepthZ,
            string.IsNullOrEmpty(fabPartId) ? null : fabPartId);
        return true;
    }

    private static bool CornersInsideHull(
        float cy, float cu, float hy, float hu, float hz, float zStem)
    {
        for (var ix = -1; ix <= 1; ix += 2)
        for (var iy = -1; iy <= 1; iy += 2)
        {
            if (!CalypsoLockGenerator.InsideHullPublic(cy + ix * hy, cu + iy * hu, zStem, -0.02f))
                return false;
            if (!CalypsoLockGenerator.InsideHullPublic(cy + ix * hy, cu + iy * hu, zStem + hz, -0.02f))
                return false;
            if (!CalypsoLockGenerator.InsideHullPublic(cy + ix * hy, cu + iy * hu, zStem - hz, -0.02f))
                return false;
        }

        return true;
    }

    private static List<List<(float X, float Y)>> BuildHoles(
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<WebPlatePortal> hatchPortals,
        IReadOnlyList<(float U0, float U1)> portalBands,
        float upMin,
        float upMax,
        float zStem,
        float inset,
        IReadOnlyList<float> decks,
        float sectionY,
        bool bowAligned,
        bool skipLightening = false,
        bool openWebFrame = false)
    {
        _ = sectionY;
        var holes = new List<List<(float X, float Y)>>();

        if (openWebFrame)
        {
            // Open frame: circulation clearances span nearly the full deck bay (not door slits).
            var bayBands = BuildElevationBands(upMin, upMax, decks);
            if (bayBands.Count == 0)
                bayBands.Add((upMin, upMax));
            foreach (var (y0, y1) in athwartGaps)
            {
                if (y1 <= y0 + 0.05f)
                    continue;
                foreach (var (u0, u1) in bayBands)
                {
                    var clearU0 = u0 + 0.28f; // leave thin deck chord
                    var clearU1 = u1 - 0.28f;
                    if (clearU1 > clearU0 + 0.8f)
                        holes.Add(RectHole(y0, y1, clearU0, clearU1));
                }
            }
        }
        else
        {
            // Pressure BH: shirt-sleeves portals only.
            foreach (var (y0, y1) in athwartGaps)
            {
                if (y1 <= y0 + 0.05f)
                    continue;
                foreach (var (u0, u1) in portalBands)
                    holes.Add(RectHole(y0, y1, u0, u1));
            }
        }

        foreach (var p in hatchPortals)
        {
            if (p.Y1 <= p.Y0 + 0.05f || p.U1 <= p.U0 + 0.4f)
                continue;
            holes.Add(RectHole(p.Y0, p.Y1, p.U0, p.U1));
        }

        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, (upMin, upMax).GetCenter(), inset);
        // Frame shell-door relief; BH skips (pressure continuity on wings).
        if (!bowAligned && !skipLightening)
        {
            holes.Add(RectHole(halfY - ShellDoorReliefM, halfY + 0.05f, ShellDoorBandUp0, ShellDoorBandUp1));
            holes.Add(RectHole(-halfY - 0.05f, -halfY + ShellDoorReliefM, ShellDoorBandUp0, ShellDoorBandUp1));
        }

        // Lightening between decks — open frames use aggressive cuts; BH skips entirely.
        if (bowAligned || skipLightening)
            return holes;

        var border = openWebFrame ? 0.28f : 0.38f;
        var topBot = openWebFrame ? 0.22f : 0.32f;
        var bands = BuildElevationBands(upMin, upMax, decks);
        foreach (var (u0, u1) in bands)
        {
            var uMid = (u0, u1).GetCenter();
            var bandHalfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, uMid, inset) - 0.35f;
            if (bandHalfY < 0.6f || u1 - u0 < 1.05f)
                continue;
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-bandHalfY, bandHalfY, athwartGaps))
            {
                var width = yb - ya;
                var height = u1 - u0;
                if (width < 1.0f)
                    continue;
                var holeY0 = ya + MathF.Min(border, width * (openWebFrame ? 0.12f : 0.18f));
                var holeY1 = yb - MathF.Min(border, width * (openWebFrame ? 0.12f : 0.18f));
                var holeU0 = u0 + MathF.Min(topBot, height * (openWebFrame ? 0.12f : 0.22f));
                var holeU1 = u1 - MathF.Min(topBot, height * (openWebFrame ? 0.12f : 0.22f));
                if (holeY1 - holeY0 < 0.55f || holeU1 - holeU0 < 0.45f)
                    continue;
                holes.Add(RectHole(holeY0, holeY1, holeU0, holeU1));
            }
        }

        return holes;
    }

    /// <summary>
    /// Shirt-sleeves portal elevation bands: sill at each deck plate, head at clear height
    /// (clamped below the next deck so a steel header remains).
    /// </summary>
    private static List<(float U0, float U1)> BuildPassagePortalBands(
        float upMin,
        float upMax,
        IReadOnlyList<float> decks)
    {
        var floors = new List<float> { upMin };
        foreach (var d in decks)
        {
            if (d > upMin + 0.15f && d < upMax - 0.4f)
                floors.Add(d);
        }

        floors = floors.Distinct().OrderBy(u => u).ToList();
        var bands = new List<(float U0, float U1)>();
        for (var i = 0; i < floors.Count; i++)
        {
            var sill = floors[i] + 0.02f;
            var next = i + 1 < floors.Count ? floors[i + 1] : upMax;
            var head = MathF.Min(sill + PassagePortalClearH, next - 0.12f);
            if (head > sill + 1.2f)
                bands.Add((sill, head));
        }

        return bands;
    }

    private static List<(float U0, float U1)> BuildElevationBands(
        float upMin,
        float upMax,
        IReadOnlyList<float> decks)
        => BuildBandsFromCuts(upMin, upMax, decks, extraCuts: null, minHeight: 0.2f);

    /// <summary>
    /// Deck plate cuts plus portal sill/head so coarse emit can keep a solid lintel above each opening.
    /// </summary>
    private static List<(float U0, float U1)> BuildElevationBandsWithPortals(
        float upMin,
        float upMax,
        IReadOnlyList<float> decks,
        IReadOnlyList<(float U0, float U1)> portalBands)
    {
        var extra = new List<float>();
        foreach (var (u0, u1) in portalBands)
        {
            extra.Add(u0);
            extra.Add(u1);
        }

        return BuildBandsFromCuts(upMin, upMax, decks, extra, minHeight: 0.12f);
    }

    private static List<(float U0, float U1)> BuildBandsFromCuts(
        float upMin,
        float upMax,
        IReadOnlyList<float> decks,
        IReadOnlyList<float>? extraCuts,
        float minHeight)
    {
        var cuts = new List<float> { upMin };
        foreach (var d in decks)
        {
            if (d > upMin + 0.2f && d < upMax - 0.2f)
                cuts.Add(d);
        }

        if (extraCuts is not null)
        {
            foreach (var c in extraCuts)
            {
                if (c > upMin + 0.05f && c < upMax - 0.05f)
                    cuts.Add(c);
            }
        }

        cuts.Add(upMax);
        cuts = cuts.Distinct().OrderBy(c => c).ToList();
        var bands = new List<(float U0, float U1)>();
        for (var i = 0; i < cuts.Count - 1; i++)
        {
            var a = cuts[i];
            var b = cuts[i + 1];
            if (i > 0)
                a += 0.02f;
            if (i < cuts.Count - 2)
                b -= 0.02f;
            if (b > a + minHeight)
                bands.Add((a, b));
        }

        return bands;
    }

    private static List<(float X, float Y)> RectHole(float y0, float y1, float u0, float u1) =>
        [(y0, u0), (y1, u0), (y1, u1), (y0, u1)];

    private static List<(float Y0, float Y1)> GapsForBand(
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        IReadOnlyList<WebPlatePortal> hatchPortals,
        IReadOnlyList<(float U0, float U1)> portalBands,
        float up,
        float halfY,
        bool bowAligned,
        bool skipShellDoor = false)
    {
        var gaps = new List<(float Y0, float Y1)>();
        // Passageway Y cuts only inside a portal elevation band (leave lintel/header steel).
        if (portalBands.Any(b => up >= b.U0 - 0.02f && up <= b.U1 + 0.02f))
            gaps.AddRange(athwartGaps);

        foreach (var p in hatchPortals)
        {
            if (up >= p.U0 - 0.02f && up <= p.U1 + 0.02f)
                gaps.Add((p.Y0, p.Y1));
        }

        if (!bowAligned && !skipShellDoor && up >= ShellDoorBandUp0 && up <= ShellDoorBandUp1)
        {
            gaps.Add((halfY - ShellDoorReliefM, halfY + 0.05f));
            gaps.Add((-halfY - 0.05f, -halfY + ShellDoorReliefM));
        }

        return MergeGaps(gaps);
    }

    private static List<(float Y0, float Y1)> MergeGaps(List<(float Y0, float Y1)> gaps)
    {
        gaps.Sort((a, b) => a.Y0.CompareTo(b.Y0));
        var merged = new List<(float Y0, float Y1)>();
        foreach (var g in gaps)
        {
            if (merged.Count == 0 || g.Y0 > merged[^1].Y1 + 0.05f)
                merged.Add(g);
            else
                merged[^1] = (merged[^1].Y0, MathF.Max(merged[^1].Y1, g.Y1));
        }

        return merged;
    }

    private static bool CenterInAnyHole(float localY, float up, IReadOnlyList<List<(float X, float Y)>> holes)
    {
        foreach (var hole in holes)
        {
            if (hole.Count < 3)
                continue;
            var minY = hole.Min(p => p.X);
            var maxY = hole.Max(p => p.X);
            var minU = hole.Min(p => p.Y);
            var maxU = hole.Max(p => p.Y);
            if (localY >= minY && localY <= maxY && up >= minU && up <= maxU)
                return true;
        }

        return false;
    }

    private static bool CellInsideOctagon(float cy, float cu, float hy, float hu, (float Y, float Up)[] ring)
    {
        Span<(float Y, float Up)> corners =
        [
            (cy - hy, cu - hu),
            (cy + hy, cu - hu),
            (cy - hy, cu + hu),
            (cy + hy, cu + hu),
        ];
        foreach (var (y, up) in corners)
        {
            if (!PointInOctagon(y, up, ring))
                return false;
        }

        return true;
    }

    private static bool PointInOctagon(float y, float up, (float Y, float Up)[] ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var yi = ring[i].Y;
            var ui = ring[i].Up;
            var yj = ring[j].Y;
            var uj = ring[j].Up;
            if ((ui > up) != (uj > up)
                && y < (yj - yi) * (up - ui) / (uj - ui + 1e-8f) + yi)
                inside = !inside;
        }

        return inside;
    }

    private static string FabPartId(string partId, string prefix)
    {
        var id = partId;
        foreach (var p in new[] { "frame-", "bh-" })
        {
            if (id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                id = id[p.Length..];
                break;
            }
        }

        return $"{prefix}-{id.Replace('.', '-')}-WEB";
    }
}
