using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Transverse frame webs (z-thin plate with cutouts), deck beams, and longitudinals.
/// </summary>
internal static class CalypsoShipbuildingFrames
{
    internal const float DefaultSkinInset = 0.18f;
    /// <summary>Solid transverse web: plate on inner octagon with per-deck passageway/hatch portals.</summary>
    public static void AddTransverseFrameWeb(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        bool bowAligned = false,
        IReadOnlyList<float>? deckUps = null,
        IReadOnlyList<FrameWebPlateBuilder.WebPlatePortal>? hatchPortals = null)
    {
        CalypsoLockGenerator.SectionAtPublic(zStem, out _, out var halfH, out var mid, out _, out _);
        var inset = DefaultSkinInset;
        var upMin = mid - halfH + inset + web * 0.5f;
        var upMax = mid + halfH - inset - web * 0.5f;
        if (upMax <= upMin + 0.2f)
            return;

        FrameWebPlateBuilder.Emit(
            entities, loa, frameId, zStem, web, halfDepthZ, athwartGaps, bowAligned, deckUps, hatchPortals);
    }

    /// <summary>
    /// Open-frame perimeter ring on the IML octagon — primary closed load path of a transverse web frame.
    /// </summary>
    public static void AddPerimeterRingPublic(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float inset)
        => EmitRingBars(entities, loa, frameId, zStem, web, halfDepthZ, inset, namePrefix: "ring");

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
        EmitRingBars(entities, loa, frameId, zStem, web, halfDepthZ, inset, namePrefix: "flange");
    }

    private static void EmitRingBars(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float inset,
        string namePrefix)
    {
        // Ring sits slightly inboard of the web plate so AABB corners stay inside IML.
        var ring = InnerOctagonRing(zStem, inset + web * 0.6f);
        var (cy0, cu0) = ring.GetSectionCenter();
        var flangeHalf = MathF.Max(0.06f, web * 0.4f);
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
                    entities, loa, $"{frameId}-{namePrefix}-{i}",
                    edgeY, edgeU, zStem, len * 0.42f, flangeHalf, halfDepthZ);
            }
            else
            {
                CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, $"{frameId}-{namePrefix}-{i}",
                    edgeY, edgeU, zStem, flangeHalf, len * 0.42f, halfDepthZ);
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
        IReadOnlyList<float> deckUps,
        float tPlate = 0.1f,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans = null)
    {
        if (zEnd <= zStart + 1f)
            return;

        var bayIndex = 0;
        // Start at zStart — HalfBeamInside + ySide<0.35 already taper-clips the extreme stem.
        var girderStart = zStart;
        var floorDeck = deckUps.Count > 0 ? deckUps[0] : 0.5f;
        for (var z = girderStart; z < zEnd - 0.5f; z += frameBay)
        {
            var zc = MathF.Min(z + frameBay * 0.5f, zEnd - 0.2f);
            var halfLenZ = MathF.Min(frameBay * 0.48f, zEnd - zc);
            if (halfLenZ < 0.25f)
                break;

            var inHold = IsHoldCargoStem(zc, openAtriumSpans);
            foreach (var deckUp in deckUps)
            {
                // HOLD: keep continuous keel + side girders on the cargo floor only —
                // no upper deck girder tiers across the open bay.
                if (inHold && deckUp > floorDeck + 0.15f)
                    continue;
                if (!inHold && IsOpenAtriumDeck(zc, deckUp, openAtriumSpans))
                    continue; // ENG atrium — no floating girders across the void

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
                // Sit under plate underside (plate top = deckUp); not a second floating floor.
                // HOLD floor run uses a deeper web box for continuous side/keel strength.
                var webH = inHold ? web * 0.55f : web * 0.35f;
                var up = deckUp - tPlate - webH;

                var keelAsm = "girder-keel";
                var portAsm = "girder-port";
                var stbdAsm = "girder-stbd";
                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-keel-d{bayIndex}-{deckUp:0.#}",
                    0f,
                    up,
                    zc,
                    MathF.Min(0.12f, web),
                    webH,
                    halfLenZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.LongitudinalGirder, keelAsm, StationZ: zc, DeckUp: deckUp));

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-port-d{bayIndex}-{deckUp:0.#}",
                    -ySide,
                    up,
                    zc,
                    web * 0.4f,
                    webH,
                    halfLenZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.LongitudinalGirder, portAsm, StationZ: zc, DeckUp: deckUp));

                CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    $"girder-stbd-d{bayIndex}-{deckUp:0.#}",
                    ySide,
                    up,
                    zc,
                    web * 0.4f,
                    webH,
                    halfLenZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.LongitudinalGirder, stbdAsm, StationZ: zc, DeckUp: deckUp));
            }

            bayIndex++;
        }
    }

    /// <summary>
    /// Short bracket stubs at frame × keel/port/stbd girder crossings (weld tags via <c>joins</c>).
    /// </summary>
    public static void AddFrameGirderJoinStubs(
        List<CadEntity> entities,
        float loa,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> frameStations,
        float web,
        float halfDepthZ,
        IReadOnlyList<float> deckUps,
        float tPlate,
        Func<float, bool> inOpenHoldCell,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans = null)
    {
        var stubHy = MathF.Max(0.06f, web * 0.35f);
        var stubHu = MathF.Max(0.06f, web * 0.28f);
        var stubHz = MathF.Max(halfDepthZ, web * 0.45f);

        foreach (var (id, zStem, _) in frameStations)
        {
            if (inOpenHoldCell(zStem))
                continue;
            var frameName = id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id : $"frame-{id}";
            var frameAsm = frameName.Contains('-')
                ? string.Join('-', frameName.Split('-').Take(2))
                : frameName;

            foreach (var deckUp in deckUps)
            {
                if (IsOpenAtriumDeck(zStem, deckUp, openAtriumSpans))
                    continue;

                var inset = DefaultSkinInset + 0.28f;
                var halfB = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, deckUp, inset);
                var ySide = halfB - web * 0.85f;
                if (ySide < 0.35f)
                    continue;
                var up = deckUp - tPlate - web * 0.35f;

                void Stub(string side, float y, string girderAsm)
                {
                    CalypsoLockGenerator.TryAddStructureBar(
                        entities,
                        loa,
                        $"{frameName}-join-{side}-d{deckUp:0.#}",
                        y,
                        up,
                        zStem,
                        stubHy,
                        stubHu,
                        stubHz,
                        meta: new StructuralSolidMeta(
                            StructuralRoles.TransverseFrame,
                            frameAsm,
                            StationZ: zStem,
                            DeckUp: deckUp,
                            Joins: [girderAsm, frameAsm]));
                }

                Stub("keel", 0f, "girder-keel");
                Stub("port", -ySide, "girder-port");
                Stub("stbd", ySide, "girder-stbd");
            }
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
        IReadOnlyList<(float Y0, float Y1)> athwartGaps,
        float tPlate = 0.1f,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans = null)
    {
        foreach (var deckUp in deckUps)
        {
            if (IsOpenAtriumDeck(zStem, deckUp, openAtriumSpans))
                continue;

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
                    deckUp - tPlate - web * 0.35f,
                    zStem,
                    hy,
                    web * 0.35f,
                    halfDepthZ * 0.8f);
            }
        }
    }

    /// <summary>
    /// Cellular sandwich deck panels: top face + core + bottom face per bay.
    /// Names <c>deck-sandwich-dk{n}-*-face-top|core|face-bot</c>; plate top = lock elevation.
    /// Upper decks do not cross ENG atrium / HOLD cargo (floor plate at <c>FloorTop</c> remains).
    /// </summary>
    public static void AddCellularSandwichDecks(
        List<CadEntity> entities,
        float loa,
        float zStart,
        float zEnd,
        float frameBay,
        IReadOnlyList<(int DeckNum, float PlateTop)> decks,
        float tPlate,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans = null)
    {
        if (zEnd <= zStart + 0.5f || decks.Count == 0 || tPlate < 0.02f)
            return;

        var bayIndex = 0;
        for (var z = zStart; z < zEnd - 0.25f; z += frameBay)
        {
            var zc = MathF.Min(z + frameBay * 0.5f, zEnd - 0.15f);
            var halfLenZ = MathF.Min(frameBay * 0.48f, zEnd - zc);
            if (halfLenZ < 0.2f)
                break;

            foreach (var (deckNum, plateTop) in decks)
            {
                var zA = zc - halfLenZ;
                var zB = zc + halfLenZ;
                var inset = DefaultSkinInset + 0.12f;
                var halfY = MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zA, plateTop, inset),
                    MathF.Min(
                        CalypsoLockGenerator.HalfBeamInsidePublic(zc, plateTop, inset),
                        CalypsoLockGenerator.HalfBeamInsidePublic(zB, plateTop, inset)));
                if (halfY < 0.4f)
                    continue;

                var tag = deckNum < 0 ? "m1" : deckNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (IsOpenAtriumDeck(zc, plateTop, openAtriumSpans))
                {
                    // ENG atrium / HOLD cargo: keep shirt-sleeves CORR decks; void only the center cell.
                    if (IsHoldCargoStem(zc, openAtriumSpans))
                        continue; // HOLD = open bay, no upper plates at all
                    EmitCorridorDeckStrips(
                        entities, loa, tag, bayIndex, plateTop, tPlate, zc, halfLenZ, halfY);
                    continue;
                }

                EmitSandwichPanel(
                    entities, loa, $"deck-sandwich-dk{tag}-b{bayIndex}",
                    0f, plateTop, tPlate, zc, halfY * 0.98f, halfLenZ, $"deck-dk{tag}");
            }

            bayIndex++;
        }
    }

    /// <summary>Three stacked solids: face-top (full footprint), core (inset), face-bot.</summary>
    private static void EmitSandwichPanel(
        List<CadEntity> entities,
        float loa,
        string baseName,
        float y,
        float plateTop,
        float tPlate,
        float zc,
        float halfY,
        float halfLenZ,
        string assembly)
    {
        // Face half-extent must stay ≥ TryAddStructureBar min (0.02 m).
        var faceT = MathF.Max(0.045f, tPlate * 0.28f);
        var coreT = MathF.Max(0.04f, tPlate);
        var faceHalf = MathF.Max(0.022f, faceT * 0.5f);
        var coreHalf = MathF.Max(0.022f, coreT * 0.5f);
        var topCenter = plateTop - faceHalf;
        var botCenter = plateTop - tPlate + faceHalf;
        var coreCenter = (topCenter - faceHalf + botCenter + faceHalf) * 0.5f;

        void Add(string suffix, float up, float hy, float hu, IReadOnlyList<string>? joins = null)
        {
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-{suffix}",
                y, up, zc, hy, hu, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zc, DeckUp: plateTop, Joins: joins));
        }

        Add("face-top", topCenter, halfY, faceHalf, [$"{baseName}-core"]);
        Add("core", coreCenter, halfY * 0.92f, coreHalf, [$"{baseName}-face-top", $"{baseName}-face-bot"]);
        Add("face-bot", botCenter, halfY, faceHalf, [$"{baseName}-core"]);
    }

    /// <summary>
    /// DK-001 CORR_P/S deck strips through an open atrium — passageway floors stay when the
    /// center cell is voided.
    /// </summary>
    private static void EmitCorridorDeckStrips(
        List<CadEntity> entities,
        float loa,
        string deckTag,
        int bayIndex,
        float plateTop,
        float tPlate,
        float zc,
        float halfLenZ,
        float halfY)
    {
        // Lock circulation defaults: CORR inner/outer ±5…±8 m.
        const float CorrInner = 5f;
        const float CorrOuter = 8f;
        var yOut = MathF.Min(CorrOuter, halfY * 0.98f);
        var yIn = MathF.Min(CorrInner, yOut - 0.6f);
        if (yOut - yIn < 0.5f)
            return;

        var hy = (yOut - yIn) * 0.5f;
        var yPort = -(yIn + yOut) * 0.5f;
        var yStbd = -yPort;
        var asm = $"deck-dk{deckTag}";
        EmitSandwichPanel(
            entities, loa, $"deck-sandwich-dk{deckTag}-corrP-b{bayIndex}",
            yPort, plateTop, tPlate, zc, hy, halfLenZ, asm);
        EmitSandwichPanel(
            entities, loa, $"deck-sandwich-dk{deckTag}-corrS-b{bayIndex}",
            yStbd, plateTop, tPlate, zc, hy, halfLenZ, asm);
    }

    /// <summary>
    /// True when <paramref name="plateTop"/> is an intermediate deck through an open atrium/cargo cell
    /// (above the cell floor). Floor plates at <c>FloorTop</c> stay.
    /// </summary>
    internal static bool IsOpenAtriumDeck(
        float zStem,
        float plateTop,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans)
    {
        if (openAtriumSpans is null || openAtriumSpans.Count == 0)
            return false;
        foreach (var (z0, z1, floorTop) in openAtriumSpans)
        {
            if (zStem <= z0 + 0.05f || zStem >= z1 - 0.05f)
                continue;
            if (plateTop > floorTop + 0.15f)
                return true;
        }

        return false;
    }

    /// <summary>HOLD cargo cell stem (long aft atrium ≥ 12 m).</summary>
    internal static bool IsHoldCargoStem(
        float zStem,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans)
    {
        if (openAtriumSpans is null)
            return false;
        foreach (var (z0, z1, _) in openAtriumSpans)
        {
            // HOLD is the long aft span; ENG is short midbody — treat spans ≥ 12 m as cargo.
            if (z1 - z0 < 12f)
                continue;
            if (zStem > z0 + 0.05f && zStem < z1 - 0.05f)
                return true;
        }

        return false;
    }
}
