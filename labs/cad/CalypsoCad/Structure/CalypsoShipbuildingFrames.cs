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
    /// Cargo-hold station: perimeter ring + cargo-floor chords only. No intermediate decks or
    /// cross-hold webs obstructing the cargo envelope.
    /// </summary>
    public static void AddHoldPerimeterFrame(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        IReadOnlyList<float> deckUps,
        float tPlate)
    {
        var inset = DefaultSkinInset;
        AddPerimeterRingPublic(entities, loa, frameId, zStem, web, halfDepthZ, inset);
        if (deckUps.Count == 0)
            return;
        var floorUp = deckUps[0];
        var chordHalf = MathF.Max(0.08f, web * 0.45f);
        var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(zStem, floorUp, inset) - 0.15f;
        if (halfY < 0.35f)
            return;
        // Floor chord only — cargo void above remains clear.
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, $"{frameId}-chord-{floorUp:0.#}-holdfloor",
            0f, floorUp, zStem, halfY * 0.5f, chordHalf, halfDepthZ,
            meta: new StructuralSolidMeta(
                StructuralRoles.TransverseFrame, frameId.Contains('-')
                    ? string.Join('-', frameId.Split('-').Take(2))
                    : frameId,
                StationZ: zStem, DeckUp: floorUp));
        _ = tPlate;
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
        // Polygonal ring: one oriented member per IML octagon edge (not AABB axis approximations).
        // Extra skin inset so side/bilge edges survive BoxFits at midbody (was dropping 6/8 bars).
        var ring = InnerOctagonRing(zStem, inset + web * 0.85f + 0.12f);
        var (cy0, cu0) = ring.GetSectionCenter();
        var flangeHalf = MathF.Max(0.05f, web * 0.32f);
        var asm = frameId.Contains('-')
            ? string.Join('-', frameId.Split('-').Take(2))
            : frameId;
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var dy = b.Y - a.Y;
            var du = b.Up - a.Up;
            var len = MathF.Sqrt(dy * dy + du * du);
            if (len < 0.12f)
                continue;
            var edgeY = (a.Y + b.Y) * 0.5f;
            var edgeU = (a.Up + b.Up) * 0.5f;
            // Pull inboard until the flange AABB nests (midbody side edges used to vanish).
            var inwardY = cy0 - edgeY;
            var inwardU = cu0 - edgeU;
            var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
            var ny = inwardLen > 1e-4f ? inwardY / inwardLen : 0f;
            var nu = inwardLen > 1e-4f ? inwardU / inwardLen : 0f;
            var halfLen = len * 0.5f;
            var angle = MathF.Atan2(du, dy);
            var hf = flangeHalf;
            var placed = false;
            for (var attempt = 0; attempt < 8 && !placed; attempt++)
            {
                var pull = MathF.Min(hf * (1.1f + attempt * 0.35f), inwardLen * 0.85f);
                var y = edgeY + ny * pull;
                var u = edgeU + nu * pull;
                var ha = halfLen * (attempt < 3 ? 1f : MathF.Max(0.55f, 1f - attempt * 0.08f));
                placed = CalypsoLockGenerator.TryAddOrientedStructureBar(
                    entities, loa, $"{frameId}-{namePrefix}-{i}",
                    y, u, zStem,
                    halfAlongEdge: ha,
                    halfFlange: hf,
                    halfDepthZ: halfDepthZ,
                    orientZ: angle,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.TransverseFrame, asm,
                        StationZ: zStem, DeckUp: u,
                        Joins: [$"{frameId}-{namePrefix}-{(i + ring.Length - 1) % ring.Length}",
                                $"{frameId}-{namePrefix}-{(i + 1) % ring.Length}"]));
                if (!placed)
                    hf = MathF.Max(0.035f, hf * 0.82f);
            }

            // Last resort: axis-aligned stub so the cage never loses a corner (OuterSkeleton completeness).
            if (!placed)
            {
                var pull = MathF.Min(0.35f, inwardLen * 0.5f);
                var y = edgeY + ny * pull;
                var u = edgeU + nu * pull;
                var hy = MathF.Max(0.06f, MathF.Abs(dy) * 0.45f);
                var hu = MathF.Max(0.06f, MathF.Abs(du) * 0.45f);
                if (hy < 0.08f && hu < 0.08f)
                {
                    hy = flangeHalf;
                    hu = flangeHalf;
                }

                CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, $"{frameId}-{namePrefix}-{i}",
                    y, u, zStem, hy, hu, halfDepthZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.TransverseFrame, asm, StationZ: zStem, DeckUp: u));
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

    /// <summary>
    /// Skin stringers along the IML octagon — longitudinals at ring vertices + three-tier
    /// port/stbd side runs (HOLD bay-noshell outer cage). Bay segments meet at stations.
    /// </summary>
    public static void AddOuterHullStringers(
        List<CadEntity> entities,
        float loa,
        float zStart,
        float zEnd,
        float web,
        float frameBay)
    {
        if (zEnd <= zStart + 1f)
            return;

        // Deep inset so bow taper + midbody sides both nest (was skipping stem bays entirely).
        var inset = DefaultSkinInset + web * 0.9f + 0.15f;
        var stiffHy = MathF.Max(0.045f, web * 0.24f);
        var stiffHu = MathF.Max(0.05f, web * 0.28f);
        var bayIndex = 0;
        for (var z = zStart; z < zEnd - 0.35f; z += frameBay)
        {
            var z0 = z;
            var z1 = MathF.Min(z + frameBay, zEnd);
            var zc = (z0 + z1) * 0.5f;
            var halfLenZ = (z1 - z0) * 0.5f;
            if (halfLenZ < 0.2f)
                break;

            CalypsoLockGenerator.SectionAtPublic(zc, out var halfB, out var halfH, out var mid, out _, out _);
            if (halfB < 0.35f || halfH < 0.35f)
            {
                bayIndex++;
                continue;
            }

            // Narrowest section along the bay so ends never poke past taper.
            CalypsoLockGenerator.SectionAtPublic(z0 + 0.05f, out var halfB0, out var halfH0, out _, out _, out _);
            CalypsoLockGenerator.SectionAtPublic(z1 - 0.05f, out var halfB1, out var halfH1, out _, out _, out _);
            var useHalfB = MathF.Min(halfB, MathF.Min(halfB0, halfB1));
            var useHalfH = MathF.Min(halfH, MathF.Min(halfH0, halfH1));
            _ = useHalfH;

            // 1) Vertex stringers — one longitudinal at each IML octagon corner.
            var ring = InnerOctagonRing(zc, inset);
            // Scale ring into the narrowest bay half-beam so stem segments still connect.
            CalypsoLockGenerator.SectionAtPublic(zc, out var refB, out _, out _, out _, out _);
            var scale = refB > 0.2f ? MathF.Min(1f, (useHalfB - inset) / MathF.Max(0.2f, refB - inset)) : 1f;
            scale = Math.Clamp(scale, 0.55f, 1f);
            var (cy, cu) = ring.GetSectionCenter();
            for (var i = 0; i < ring.Length; i++)
            {
                var y = cy + (ring[i].Y - cy) * scale;
                var up = cu + (ring[i].Up - cu) * scale;
                var inwardY = cy - y;
                var inwardU = cu - up;
                var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
                if (inwardLen > 1e-4f)
                {
                    var pull = MathF.Min(stiffHy * 1.6f, inwardLen * 0.4f);
                    y += inwardY / inwardLen * pull;
                    up += inwardU / inwardLen * pull;
                }

                CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, $"stringer-v{i}-d{bayIndex}",
                    y, up, zc, stiffHy, stiffHu, halfLenZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.ShellStiffener, "stringer-skin",
                        StationZ: zc, DeckUp: up));
            }

            // 2) Mid-side tiers (port/stbd) — same three-band read as bay shell stiffeners.
            var yFace = useHalfB - inset - stiffHy;
            var uBot = mid - halfH + inset + stiffHu;
            var uTop = mid + halfH - inset - stiffHu;
            var huSide = (uTop - uBot) * 0.48f;
            if (yFace >= 0.25f && huSide >= 0.3f)
            {
                for (var i = -1; i <= 1; i++)
                {
                    var up = mid + i * (huSide * 0.55f);
                    CalypsoLockGenerator.TryAddStructureBar(
                        entities, loa, $"stringer-port-{i}-d{bayIndex}",
                        -yFace, up, zc, stiffHy, stiffHu, halfLenZ * 0.98f,
                        meta: new StructuralSolidMeta(
                            StructuralRoles.ShellStiffener, "stringer-skin",
                            StationZ: zc, DeckUp: up));
                    CalypsoLockGenerator.TryAddStructureBar(
                        entities, loa, $"stringer-stbd-{i}-d{bayIndex}",
                        yFace, up, zc, stiffHy, stiffHu, halfLenZ * 0.98f,
                        meta: new StructuralSolidMeta(
                            StructuralRoles.ShellStiffener, "stringer-skin",
                            StationZ: zc, DeckUp: up));
                }
            }

            bayIndex++;
        }
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
            // half = bay/2 so adjacent bay segments meet at station planes (continuous load path).
            var halfLenZ = MathF.Min(frameBay * 0.5f, zEnd - zc);
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

        var floorDeck = deckUps.Count > 0 ? deckUps[0] : 0.5f;
        foreach (var (id, zStem, _) in frameStations)
        {
            var hold = inOpenHoldCell(zStem);
            var frameName = id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id : $"frame-{id}";
            var frameAsm = frameName.Contains('-')
                ? string.Join('-', frameName.Split('-').Take(2))
                : frameName;

            foreach (var deckUp in deckUps)
            {
                // HOLD: join stubs only at cargo floor (perimeter framing ↔ floor girders).
                if (hold && deckUp > floorDeck + 0.15f)
                    continue;
                if (!hold && IsOpenAtriumDeck(zStem, deckUp, openAtriumSpans))
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
                            StructuralRoles.StructuralBracket,
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
            // Meet neighbors at bay planes (fabrication segments; assembled continuity).
            var halfLenZ = MathF.Min(frameBay * 0.5f, zEnd - zc);
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

    /// <summary>
    /// Cellular sandwich: face-top + face-bot + internal orthogonal ribs + perimeter.
    /// Total stack thickness equals <paramref name="tPlate"/> (no overlapping solid "core" slab).
    /// </summary>
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
        // Faces thin; residual height = cellular cavity for ribs. faceHalf ≥ TryAdd minHalf (0.012).
        var faceT = Math.Clamp(tPlate * 0.24f, 0.026f, tPlate * 0.35f);
        var coreH = MathF.Max(0.02f, tPlate - 2f * faceT);
        var faceHalf = faceT * 0.5f;
        var topCenter = plateTop - faceHalf;
        var botCenter = plateTop - tPlate + faceHalf;
        var ribUp = (topCenter - faceHalf + botCenter + faceHalf) * 0.5f;
        var ribHu = coreH * 0.48f;

        void Face(string suffix, float up, IReadOnlyList<string>? joins)
        {
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-{suffix}",
                y, up, zc, halfY, faceHalf, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zc, DeckUp: plateTop, Joins: joins),
                minHalf: 0.012f);
        }

        Face("face-top", topCenter, [$"{baseName}-rib", $"{baseName}-face-bot"]);
        Face("face-bot", botCenter, [$"{baseName}-rib", $"{baseName}-face-top"]);

        // Orthogonal cellular webs inside the cavity (athwart + longitudinal).
        const float CellPitch = 1.15f;
        var ribT = MathF.Max(0.025f, faceT * 0.9f);
        var ribHalfT = ribT * 0.5f;
        var y0 = y - halfY * 0.92f;
        var y1 = y + halfY * 0.92f;
        var z0 = zc - halfLenZ * 0.92f;
        var z1 = zc + halfLenZ * 0.92f;
        var ribJoins = new[] { $"{baseName}-face-top", $"{baseName}-face-bot" };

        for (var yy = y0 + CellPitch * 0.5f; yy < y1 - 0.1f; yy += CellPitch)
        {
            // Longitudinal rib (runs fore–aft).
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-rib-long-y{yy:0.#}",
                yy, ribUp, zc, ribHalfT, ribHu, halfLenZ * 0.9f,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
                minHalf: 0.012f);
        }

        for (var zz = z0 + CellPitch * 0.5f; zz < z1 - 0.1f; zz += CellPitch)
        {
            // Athwart rib (runs port–stbd) at stem station zz.
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-rib-ath-z{zz:0.#}",
                y, ribUp, zz, halfY * 0.9f, ribHu, ribHalfT,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zz, DeckUp: plateTop, Joins: ribJoins),
                minHalf: 0.012f);
        }

        // Perimeter closure (four edges of the panel).
        var periT = MathF.Max(0.03f, ribT);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, $"{baseName}-peri-port",
            y - halfY + periT * 0.5f, ribUp, zc, periT * 0.5f, ribHu, halfLenZ * 0.95f,
            meta: new StructuralSolidMeta(StructuralRoles.StructuralDeck, assembly, StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
            minHalf: 0.012f);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, $"{baseName}-peri-stbd",
            y + halfY - periT * 0.5f, ribUp, zc, periT * 0.5f, ribHu, halfLenZ * 0.95f,
            meta: new StructuralSolidMeta(StructuralRoles.StructuralDeck, assembly, StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
            minHalf: 0.012f);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, $"{baseName}-peri-fore",
            y, ribUp, zc + halfLenZ - periT * 0.5f, halfY * 0.95f, ribHu, periT * 0.5f,
            meta: new StructuralSolidMeta(StructuralRoles.StructuralDeck, assembly, StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
            minHalf: 0.012f);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, $"{baseName}-peri-aft",
            y, ribUp, zc - halfLenZ + periT * 0.5f, halfY * 0.95f, ribHu, periT * 0.5f,
            meta: new StructuralSolidMeta(StructuralRoles.StructuralDeck, assembly, StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
            minHalf: 0.012f);
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
