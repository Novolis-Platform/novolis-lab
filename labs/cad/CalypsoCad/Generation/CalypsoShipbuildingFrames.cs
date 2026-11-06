using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace CalypsoCad.Generation;

/// <summary>
/// Transverse frame webs (z-thin plate with cutouts), deck beams, and longitudinals.
/// </summary>
internal static class CalypsoShipbuildingFrames
{
    internal const float DefaultSkinInset = 0.18f;
    /// <summary>Solid transverse web: scan-filled plate on inner octagon with corridor/hatch/door voids.</summary>
    public static void AddTransverseFrameWeb(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned = false,
        IReadOnlyList<float>? deckUps = null)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out _, out var halfH, out var mid, out _, out _);
        var inset = DefaultSkinInset;
        var upMin = mid - halfH + inset + web * 0.5f;
        var upMax = mid + halfH - inset - web * 0.5f;
        if (upMax <= upMin + 0.2f)
            return;

        FrameWebPlateBuilder.Emit(
            entities, loa, frameId, zStem, web, halfDepthZ, athwartGaps, bowAligned, deckUps);
    }

    /// <summary>Continuous athwart stiffener on outer ring — inset INBOARD so flanges never sit outside IML.</summary>
    public static void AddFlangeStiffenerPublic(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float upMin,
        float upMax,
        float inset)
    {
        _ = upMin;
        _ = upMax;
        // Flange sits further inboard than the web plate (web already used inset).
        var ring = InnerOctagonRing(zStem, inset + web * 0.6f);
        var (cy0, cu0) = ring.GetSectionCenter();
        var flangeHalf = web * 0.35f;
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var dy = b.Y - a.Y;
            var du = b.Up - a.Up;
            var len = MathF.Sqrt(dy * dy + du * du);
            if (len < 0.12f)
                continue;
            var edgeY = (a.Y, b.Y).GetCenter();
            var edgeU = (a.Up, b.Up).GetCenter();
            // Pull flange center toward section centroid so the bar lies fully inboard of the ring.
            var inwardY = cy0 - edgeY;
            var inwardU = cu0 - edgeU;
            var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
            if (inwardLen > 1e-4f)
            {
                edgeY += inwardY / inwardLen * (flangeHalf + web * 0.25f);
                edgeU += inwardU / inwardLen * (flangeHalf + web * 0.25f);
            }

            if (MathF.Abs(dy) >= MathF.Abs(du))
            {
                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"{frameId}-flange-{i}",
                    edgeY,
                    edgeU,
                    zStem,
                    len * 0.42f,
                    flangeHalf,
                    halfDepthZ);
            }
            else
            {
                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"{frameId}-flange-{i}",
                    edgeY,
                    edgeU,
                    zStem,
                    flangeHalf,
                    len * 0.42f,
                    halfDepthZ);
            }
        }
    }

    public static (float Y, float Up)[] InnerOctagonRing(float zStem, float skinInset)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out var halfB, out var halfH, out var mid, out var cx, out var cy);
        var inset = skinInset;
        halfB -= inset;
        halfH -= inset;
        cx = MathF.Max(0.05f, cx - inset * 0.5f);
        cy = MathF.Max(0.05f, cy - inset * 0.5f);
        return
        [
            (-halfB + cx, mid + halfH),
            (halfB - cx, mid + halfH),
            (halfB, mid + halfH - cy),
            (halfB, mid - halfH + cy),
            (halfB - cx, mid - halfH),
            (-halfB + cx, mid - halfH),
            (-halfB, mid - halfH + cy),
            (-halfB, mid + halfH - cy),
        ];
    }

    public static void AddDeckLongitudinalGirders(
        List<CadEntity> entities,
        float loa,
        float zStart,
        float zEnd,
        float web,
        float frameBay,
        IReadOnlyList<float> deckUps)
    {
        if (zEnd <= zStart + 1f)
            return;

        var bayIndex = 0;
        // Longitudinal girders are not frames — keep them clear of the bow web plates.
        var girderStart = MathF.Max(zStart, 20f);
        for (var z = girderStart; z < zEnd - 0.5f; z += frameBay)
        {
            var zc = MathF.Min(z + frameBay * 0.5f, zEnd - 0.2f);
            var halfLenZ = MathF.Min(frameBay * 0.48f, zEnd - zc);
            if (halfLenZ < 0.25f)
                break;

            foreach (var deckUp in deckUps)
            {
                // Use the narrowest beam along the girder span so ends never poke past the taper.
                var zA = zc - halfLenZ;
                var zB = zc + halfLenZ;
                var girderInset = DefaultSkinInset + 0.28f;
                var halfB = MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zA, deckUp, girderInset),
                    MathF.Min(
                        CalypsoLockGenerator.HalfBeamInsidePublic(zc, deckUp, girderInset),
                        CalypsoLockGenerator.HalfBeamInsidePublic(zB, deckUp, girderInset)));
                var ySide = halfB - web * 0.85f;
                if (ySide < 0.35f)
                    continue;
                var up = deckUp + web * 0.35f;

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-keel-d{bayIndex}-{deckUp:0.#}",
                    0f,
                    up,
                    zc,
                    MathF.Min(0.12f, web),
                    web * 0.35f,
                    halfLenZ);

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-port-d{bayIndex}-{deckUp:0.#}",
                    -ySide,
                    up,
                    zc,
                    web * 0.4f,
                    web * 0.35f,
                    halfLenZ);

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-stbd-d{bayIndex}-{deckUp:0.#}",
                    ySide,
                    up,
                    zc,
                    web * 0.4f,
                    web * 0.35f,
                    halfLenZ);
            }

            bayIndex++;
        }
    }

    public static void AddTransverseDeckBeams(
        List<CadEntity> entities,
        float loa,
        float zStem,
        string frameId,
        float web,
        float halfDepthZ,
        IReadOnlyList<float> deckUps,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps)
    {
        foreach (var deckUp in deckUps)
        {
            var deckInset = DefaultSkinInset + 0.28f;
            var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, deckUp, deckInset) - web * 1.25f;
            if (halfY < 0.35f)
                continue;
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, athwartGaps))
            {
                var cy = (ya, yb).GetCenter();
                var hy = (yb - ya) * 0.45f;
                if (hy < 0.12f)
                    continue;
                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"{frameId}-deckbeam-{deckUp:0.#}-y{ya:0.#}",
                    cy,
                    deckUp + web * 0.35f,
                    zStem,
                    hy,
                    web * 0.35f,
                    halfDepthZ * 0.8f);
            }
        }
    }
}
