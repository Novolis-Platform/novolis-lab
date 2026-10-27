using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>
/// Transverse frame webs (z-thin plate with cutouts), deck beams, and longitudinals.
/// </summary>
internal static class CalypsoShipbuildingFrames
{
    internal const float DefaultSkinInset = 0.13f;
    /// <summary>Solid transverse web: scan-filled plate on inner octagon with corridor/hatch/door voids.</summary>
    public static void AddTransverseFrameWeb(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned = false)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out _, out var halfH, out var mid, out _, out _);
        var inset = DefaultSkinInset;
        var upMin = mid - halfH + inset + web * 0.5f;
        var upMax = mid + halfH - inset - web * 0.5f;
        if (upMax <= upMin + 0.2f)
            return;

        FrameWebPlateBuilder.Emit(
            entities, loa, frameId, zStem, web, halfDepthZ, athwartGaps, bowAligned);
    }

    /// <summary>Continuous athwart stiffener on outer ring (reads as frame flange against shell).</summary>
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
        var ring = InnerOctagonRing(zStem, inset);
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var dy = b.Y - a.Y;
            var du = b.Up - a.Up;
            var len = MathF.Sqrt(dy * dy + du * du);
            if (len < 0.12f)
                continue;
            var cy = (a.Y + b.Y) * 0.5f;
            var cu = (a.Up + b.Up) * 0.5f;
            if (MathF.Abs(dy) >= MathF.Abs(du))
            {
                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"{frameId}-flange-{i}",
                    cy,
                    cu,
                    zStem,
                    len * 0.48f,
                    web * 0.55f,
                    halfDepthZ);
            }
            else
            {
                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"{frameId}-flange-{i}",
                    cy,
                    cu,
                    zStem,
                    web * 0.55f,
                    len * 0.48f,
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
        for (var z = zStart; z < zEnd - 0.5f; z += frameBay)
        {
            var zc = MathF.Min(z + frameBay * 0.5f, zEnd - 0.2f);
            var halfLenZ = MathF.Min(frameBay * 0.48f, zEnd - zc);
            if (halfLenZ < 0.25f)
                break;

            foreach (var deckUp in deckUps)
            {
                CalypsoLockGenerator.SectionAtPublic(zc, out var halfB, out _, out _, out _, out _);
                var skin = DefaultSkinInset;
                var ySide = halfB - skin - web * 0.5f;
                var up = deckUp + web * 0.35f;

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-keel-d{bayIndex}-{deckUp:0.#}",
                    0f,
                    up,
                    zc,
                    MathF.Min(0.14f, web),
                    web * 0.4f,
                    halfLenZ);

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-port-d{bayIndex}-{deckUp:0.#}",
                    -ySide,
                    up,
                    zc,
                    web * 0.45f,
                    web * 0.4f,
                    halfLenZ);

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-stbd-d{bayIndex}-{deckUp:0.#}",
                    ySide,
                    up,
                    zc,
                    web * 0.45f,
                    web * 0.4f,
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
            var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, deckUp, DefaultSkinInset) - web;
            if (halfY < 0.35f)
                continue;
            foreach (var (ya, yb) in CalypsoLockGenerator.SolidSpansPublic(-halfY, halfY, athwartGaps))
            {
                var cy = (ya + yb) * 0.5f;
                var hy = (yb - ya) * 0.5f;
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
                    web * 0.45f,
                    halfDepthZ * 0.85f);
            }
        }
    }
}
