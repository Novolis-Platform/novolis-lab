using CalypsoCad.Generation.Fabrication;
using Novolis.Cad.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

/// <summary>Transverse frame web as octagon plate with full-height passage holes (CAL-MFG-002).</summary>
internal static class FrameWebPlateBuilder
{
    private const float ShellDoorBandUp0 = 3.6f;
    private const float ShellDoorBandUp1 = 7.4f;
    private const float ShellDoorReliefM = 1.15f;
    private const float GridStep = 1.1f;

    public static void Emit(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out _, out var halfH, out var mid, out _, out _);
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset;
        var upMin = mid - halfH + inset + web * 0.5f;
        var upMax = mid + halfH - inset - web * 0.5f;
        if (upMax <= upMin + 0.2f)
            return;

        var fabPartId = FrameFabPartId(frameId);
        var ring = CalypsoShipbuildingFrames.InnerOctagonRing(zStem, inset);
        var outerPlate = ring.Select(v => (v.Y, v.Up)).ToList();
        var holeLoops = BuildFullHeightHoles(athwartGaps, upMin, upMax, zStem, inset);

        var outerArea = HullFaceUnfolder.PolygonAreaM2(outerPlate);
        var holeArea = holeLoops.Sum(h => HullFaceUnfolder.PolygonAreaM2(h));
        var netArea = MathF.Max(0.01f, outerArea - holeArea);

        var profile = new FabCutProfile
        {
            PartId = fabPartId,
            OuterLoop = outerPlate.Select(p => (p.Item1, p.Item2)).ToList(),
            Holes = holeLoops.Select(h => h.ToList()).ToList(),
            BendAllowed = false,
            AreaM2 = netArea,
        };
        FrameWebPlateRegistry.Register(
            fabPartId,
            new FrameWebPlateRegistry.Entry(profile, zStem, web, "CAL-INT-GA-001.json"));

        EmitSolidCells(entities, loa, frameId, zStem, web, halfDepthZ, upMin, upMax, ring, athwartGaps, fabPartId);

        if (!bowAligned)
            CalypsoShipbuildingFrames.AddFlangeStiffenerPublic(
                entities, loa, frameId, zStem, web, halfDepthZ, upMin, upMax, inset);
    }

    private static void EmitSolidCells(
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
        string fabPartId)
    {
        var cellIndex = 0;
        for (var up = upMin; up < upMax; up += GridStep)
        {
            var up1 = MathF.Min(upMax, up + GridStep);
            var upMid = (up + up1) * 0.5f;
            var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, upMid, CalypsoShipbuildingFrames.DefaultSkinInset) - web * 0.35f;
            if (halfY < 0.2f)
                continue;

            var gaps = GapsForBand(athwartGaps, upMid, halfY);
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, gaps))
            {
                for (var y = ya; y < yb; y += GridStep)
                {
                    var y1 = MathF.Min(yb, y + GridStep);
                    var cy = (y + y1) * 0.5f;
                    var cu = (up + up1) * 0.5f;
                    var hy = (y1 - y) * 0.48f;
                    var hu = (up1 - up) * 0.48f;
                    if (hy < 0.04f || hu < 0.04f)
                        continue;
                    if (!PointInOctagon(cy, cu, ring))
                        continue;
                    if (IsInsideAnyHole(cy, cu, hy, hu, athwartGaps, upMin, upMax, upMid, halfY))
                        continue;

                    CalypsoLockGenerator.TryAddStructureBar(
                        entities,
                        loa,
                        $"{frameId}-web-solid-{cellIndex++}",
                        cy,
                        cu,
                        zStem,
                        hy,
                        hu,
                        halfDepthZ,
                        fabPartId);
                }
            }
        }
    }

    private static List<List<(float X, float Y)>> BuildFullHeightHoles(
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        float upMin,
        float upMax,
        float zStem,
        float inset)
    {
        var holes = new List<List<(float X, float Y)>>();
        foreach (var (y0, y1) in athwartGaps)
        {
            if (y1 <= y0 + 0.05f)
                continue;
            holes.Add(RectHole(y0, y1, upMin, upMax));
        }

        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, (upMin + upMax) * 0.5f, inset);
        holes.Add(RectHole(halfY - ShellDoorReliefM, halfY + 0.05f, ShellDoorBandUp0, ShellDoorBandUp1));
        holes.Add(RectHole(-halfY - 0.05f, -halfY + ShellDoorReliefM, ShellDoorBandUp0, ShellDoorBandUp1));
        return holes;
    }

    private static List<(float X, float Y)> RectHole(float y0, float y1, float u0, float u1) =>
        [(y0, u0), (y1, u0), (y1, u1), (y0, u1)];

    private static List<(float Y0, float Y1)> GapsForBand(
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        float up,
        float halfY)
    {
        var gaps = new List<(float Y0, float Y1)>(athwartGaps);
        if (up >= ShellDoorBandUp0 && up <= ShellDoorBandUp1)
        {
            gaps.Add((halfY - ShellDoorReliefM, halfY + 0.05f));
            gaps.Add((-halfY - 0.05f, -halfY + ShellDoorReliefM));
        }

        gaps.Sort((a, b) => a.Y0.CompareTo(b.Y0));
        var merged = new List<(float Y0, float Y1)>();
        foreach (var g in gaps)
        {
            if (merged.Count == 0 || g.Y0 > merged[^1].Y1 + 0.04f)
                merged.Add(g);
            else
                merged[^1] = (merged[^1].Y0, MathF.Max(merged[^1].Y1, g.Y1));
        }

        return merged;
    }

    private static bool IsInsideAnyHole(
        float cy,
        float cu,
        float hy,
        float hu,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        float upMin,
        float upMax,
        float upMid,
        float halfY)
    {
        var y0 = cy - hy;
        var y1 = cy + hy;
        var u0 = cu - hu;
        var u1 = cu + hu;
        foreach (var (g0, g1) in athwartGaps)
        {
            if (Overlaps(y0, y1, g0, g1) && Overlaps(u0, u1, upMin, upMax))
                return true;
        }

        if (upMid >= ShellDoorBandUp0 && upMid <= ShellDoorBandUp1)
        {
            if (Overlaps(y0, y1, halfY - ShellDoorReliefM, halfY + 0.05f)
                || Overlaps(y0, y1, -halfY - 0.05f, -halfY + ShellDoorReliefM))
                return true;
        }

        return false;
    }

    private static bool Overlaps(float a0, float a1, float b0, float b1) => a1 > b0 + 0.02f && a0 < b1 - 0.02f;

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

    private static string FrameFabPartId(string frameId)
    {
        var id = frameId.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
            ? frameId["frame-".Length..]
            : frameId;
        return $"P-FRAME-{id.Replace('.', '-')}-WEB";
    }
}
