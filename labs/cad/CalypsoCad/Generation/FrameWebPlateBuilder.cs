using CalypsoCad.Generation.Fabrication;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

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

    /// <summary>Bay web frame: dense CNC plate-web with passageway + lightening cuts.</summary>
    public static void Emit(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned,
        IReadOnlyList<float>? deckUps = null)
    {
        EmitCore(
            entities, loa, frameId, zStem, web, halfDepthZ, athwartGaps, bowAligned, deckUps,
            densePlate: true, fabPrefix: "P-FRAME", skipLightening: false, useCoarseSpans: false);
    }

    /// <summary>
    /// Bulkhead: full IML octagon steel plate → boolean passageway → plate spans.
    /// Spine-centered. This is the “massive plate then cut” path.
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
        bool useCoarseSpans = false)
    {
        EmitCore(
            entities, loa, bulkheadId, zStem, thickness, halfDepthZ, athwartGaps, bowAligned, deckUps,
            densePlate: true, fabPrefix: "P-BH", skipLightening, useCoarseSpans);
    }

    private static void EmitCore(
        List<CadEntity> entities,
        float loa,
        string partId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned,
        IReadOnlyList<float>? deckUps,
        bool densePlate,
        string fabPrefix,
        bool skipLightening = false,
        bool useCoarseSpans = false)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out _, out var halfH, out var mid, out _, out _);
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset
                    + WebClearance
                    + (bowAligned ? BowExtraClearance : 0f);
        var upMin = mid - halfH + inset + web * 0.5f;
        var upMax = mid + halfH - inset - web * 0.5f;
        if (upMax <= upMin + 0.2f)
            return;

        var fabPartId = FabPartId(partId, fabPrefix);

        // Step 1 — full steel plate on IML octagon, forced onto the ship spine (y = 0).
        var ringRaw = CalypsoShipbuildingFrames.InnerOctagonRing(zStem, inset);
        var (rawCy, _) = ringRaw.GetSectionCenter();
        var ring = ringRaw.Select(v => (Y: v.Y - rawCy, Up: v.Up)).ToArray();
        const float sectionY = 0f;

        var gaps = NormalizeGaps(athwartGaps, zStem, inset, bowAligned);
        var decks = deckUps is { Count: > 0 }
            ? deckUps.Where(u => u > upMin + 0.25f && u < upMax - 0.25f).OrderBy(u => u).ToList()
            : GuessDeckBands(upMin, upMax);

        var outerLoop = ring.Select(v => (v.Y, v.Up)).ToList();
        // Step 2 — boolean cutters (passageway; lightening optional).
        var holeLoops = BuildHoles(
            gaps, upMin, upMax, zStem, inset, decks, sectionY, bowAligned, skipLightening);

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

        if (densePlate)
        {
            if (useCoarseSpans)
            {
                // One solid bar per athwart span × deck band — reads as plate, not voxel soup.
                EmitPlateSpans(
                    entities, loa, partId, zStem, web, halfDepthZ,
                    upMin, upMax, ring, gaps, holeLoops, fabPartId, inset, bowAligned, decks,
                    skipShellDoor: skipLightening);
            }
            else
            {
                EmitPlateScan(
                    entities, loa, partId, zStem, web, halfDepthZ,
                    upMin, upMax, ring, gaps, holeLoops, fabPartId, inset, bowAligned,
                    skipShellDoor: skipLightening);
            }
        }

        // Midbody flange on web frames (not bow taper; not required on every BH).
        if (!bowAligned && fabPrefix.StartsWith("P-FRAME", StringComparison.Ordinal))
        {
            CalypsoShipbuildingFrames.AddFlangeStiffenerPublic(
                entities, loa, partId, zStem, web, halfDepthZ, upMin, upMax, inset);
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

    private static List<float> GuessDeckBands(float upMin, float upMax)
    {
        var decks = new List<float>();
        foreach (var d in new[] { 0.5f, 4f, 8f })
        {
            if (d > upMin + 0.4f && d < upMax - 0.4f)
                decks.Add(d);
        }

        return decks;
    }

    /// <summary>
    /// Coarse plate: one structure bar per solid athwart span in each deck elevation band.
    /// Used for massive BH so orbit reads continuous steel, not 0.32 m voxel noise.
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
        IReadOnlyList<List<(float X, float Y)>> holeLoops,
        string fabPartId,
        float inset,
        bool bowAligned,
        IReadOnlyList<float> decks,
        bool skipShellDoor = false)
    {
        _ = web;
        var bands = BuildElevationBands(upMin, upMax, decks);
        if (bands.Count == 0)
            bands.Add((upMin, upMax));

        var cellIndex = 0;
        foreach (var (u0, u1) in bands)
        {
            var uMid = (u0, u1).GetCenter();
            var hu = (u1 - u0) * 0.48f;
            if (hu < 0.12f)
                continue;

            var halfY = MathF.Min(
                CalypsoLockGenerator.HalfBeamInsidePublic(zStem, u0, inset),
                MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, uMid, inset),
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, u1, inset)));
            halfY -= 0.12f;
            if (halfY < 0.2f)
                continue;

            var bandGaps = GapsForBand(athwartGaps, uMid, halfY, bowAligned, skipShellDoor);
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, bandGaps))
            {
                var width = yb - ya;
                if (width < 0.25f)
                    continue;
                var cy = (ya, yb).GetCenter();
                var hy = width * 0.48f;
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

            var bandGaps = GapsForBand(athwartGaps, uMid, halfY, bowAligned, skipShellDoor);
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
        IReadOnlyList<List<(float X, float Y)>> holeLoops)
    {
        if (!CellInsideOctagon(cy, uMid, hy, hu, ring))
            return false;
        if (CenterInAnyHole(cy, uMid, holeLoops))
            return false;
        if (!CalypsoLockGenerator.InsideHullPublic(cy, uMid, zStem, -0.05f))
            return false;
        if (!CornersInsideHull(cy, uMid, hy, hu, halfDepthZ, zStem))
            return false;

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
            fabPartId);
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
        float upMin,
        float upMax,
        float zStem,
        float inset,
        IReadOnlyList<float> decks,
        float sectionY,
        bool bowAligned,
        bool skipLightening = false)
    {
        _ = sectionY;
        var holes = new List<List<(float X, float Y)>>();
        foreach (var (y0, y1) in athwartGaps)
        {
            if (y1 <= y0 + 0.05f)
                continue;
            holes.Add(RectHole(y0, y1, upMin, upMax));
        }

        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, (upMin, upMax).GetCenter(), inset);
        // Pressure BH (skipLightening): passageway cuts only — no shell-door swiss-cheese on the wings.
        if (!bowAligned && !skipLightening)
        {
            holes.Add(RectHole(halfY - ShellDoorReliefM, halfY + 0.05f, ShellDoorBandUp0, ShellDoorBandUp1));
            holes.Add(RectHole(-halfY - 0.05f, -halfY + ShellDoorReliefM, ShellDoorBandUp0, ShellDoorBandUp1));
        }

        // Lightening between decks — skip on bow taper and massive ENG/BOW plates.
        if (bowAligned || skipLightening)
            return holes;

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
                if (width < 1.35f)
                    continue;
                var holeY0 = ya + MathF.Min(0.38f, width * 0.18f);
                var holeY1 = yb - MathF.Min(0.38f, width * 0.18f);
                var holeU0 = u0 + MathF.Min(0.32f, height * 0.22f);
                var holeU1 = u1 - MathF.Min(0.32f, height * 0.22f);
                if (holeY1 - holeY0 < 0.55f || holeU1 - holeU0 < 0.45f)
                    continue;
                holes.Add(RectHole(holeY0, holeY1, holeU0, holeU1));
            }
        }

        return holes;
    }

    private static List<(float U0, float U1)> BuildElevationBands(
        float upMin,
        float upMax,
        IReadOnlyList<float> decks)
    {
        var cuts = new List<float> { upMin };
        foreach (var d in decks)
        {
            if (d > upMin + 0.2f && d < upMax - 0.2f)
                cuts.Add(d);
        }

        cuts.Add(upMax);
        cuts.Sort();
        var bands = new List<(float U0, float U1)>();
        for (var i = 0; i < cuts.Count - 1; i++)
        {
            var a = cuts[i];
            var b = cuts[i + 1];
            if (i > 0)
                a += 0.06f;
            if (i < cuts.Count - 2)
                b -= 0.06f;
            if (b > a + 0.2f)
                bands.Add((a, b));
        }

        return bands;
    }

    private static List<(float X, float Y)> RectHole(float y0, float y1, float u0, float u1) =>
        [(y0, u0), (y1, u0), (y1, u1), (y0, u1)];

    private static List<(float Y0, float Y1)> GapsForBand(
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        float up,
        float halfY,
        bool bowAligned,
        bool skipShellDoor = false)
    {
        var gaps = new List<(float Y0, float Y1)>(athwartGaps);
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
