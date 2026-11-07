using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Transverse frame webs (z-thin plate with cutouts), deck beams, and longitudinals.
/// </summary>
internal static class CalypsoShipbuildingFrames
{
    internal const float DefaultSkinInset = 0.18f;
    /// <summary>IML inset for outer-hull plating joins (octagon knuckles) — shared by frames + join stringers.</summary>
    internal const float PlatingJoinInset = DefaultSkinInset + 0.06f;
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
        var inset = PlatingJoinInset;
        AddPerimeterRingAtInset(entities, loa, frameId, zStem, web, halfDepthZ, inset);
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

    /// <summary>
    /// Perimeter ring on an exact IML inset (no extra web pad) — use so frame knuckles coincide
    /// with outer-hull plating joins / knuckle stringers.
    /// </summary>
    public static void AddPerimeterRingAtInset(
        List<CadEntity> entities,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepthZ,
        float exactInset)
        => EmitRingBars(
            entities, loa, frameId, zStem, web, halfDepthZ, exactInset,
            namePrefix: "ring", exactInsetOnly: true);

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
        string namePrefix,
        bool exactInsetOnly = false)
    {
        // Oriented polygonal ring along IML (Shipbuilding 101) — not axis-aligned AABB stubs.
        // Pull inboard until the flange nests; never shorten the edge (continuity).
        CalypsoLockGenerator.SectionAtPublic(zStem, out var halfBCheck, out var halfHCheck, out _, out _, out _);
        if (halfBCheck < 1.45f || halfHCheck < 1.45f)
            return;

        var ringInset = exactInsetOnly ? inset : inset + web * 1.05f + 0.18f;
        var ring = InnerOctagonRing(zStem, ringInset);
        var (cy0, cu0) = ring.GetSectionCenter();
        var flangeHalf = MathF.Max(0.05f, web * 0.34f);
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
            var inwardY = cy0 - edgeY;
            var inwardU = cu0 - edgeU;
            var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
            var ny = inwardLen > 1e-4f ? inwardY / inwardLen : 0f;
            var nu = inwardLen > 1e-4f ? inwardU / inwardLen : 0f;
            var halfLen = len * 0.5f;
            var angle = MathF.Atan2(du, dy);
            var placed = false;
            for (var attempt = 0; attempt < 10 && !placed; attempt++)
            {
                var pull = MathF.Min(flangeHalf * (1.2f + attempt * 0.45f), inwardLen * 0.92f);
                var y = edgeY + ny * pull;
                var u = edgeU + nu * pull;
                var hf = MathF.Max(0.035f, flangeHalf * MathF.Pow(0.9f, attempt));
                placed = CalypsoLockGenerator.TryAddOrientedStructureBar(
                    entities, loa, $"{frameId}-{namePrefix}-{i}",
                    y, u, zStem,
                    halfAlongEdge: halfLen,
                    halfFlange: hf,
                    halfDepthZ: halfDepthZ,
                    orientZ: angle,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.TransverseFrame, asm,
                        StationZ: zStem, DeckUp: u,
                        Joins:
                        [
                            $"{frameId}-{namePrefix}-{(i + ring.Length - 1) % ring.Length}",
                            $"{frameId}-{namePrefix}-{(i + 1) % ring.Length}",
                        ]));
            }
            // Fail closed — do not invent a disconnected AABB stub (Shipbuilding 102).
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
    /// Outer-hull plating-join longitudinals: one knuckle stringer at each of the 8 IML octagon
    /// vertices between consecutive frame stations (same construction as the two-bay proof).
    /// </summary>
    public static void AddOuterHullStringers(
        List<CadEntity> entities,
        float loa,
        float zStart,
        float zEnd,
        float web,
        float frameBay,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)>? frameStations = null)
    {
        if (frameStations is { Count: >= 2 })
        {
            for (var i = 0; i < frameStations.Count - 1; i++)
            {
                var (id0, z0, _) = frameStations[i];
                var (id1, z1, _) = frameStations[i + 1];
                if (z1 - z0 < 0.4f)
                    continue;
                var fore = id0.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id0 : $"frame-{id0}";
                var aft = id1.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id1 : $"frame-{id1}";
                AddPlatingJoinStringersBetweenFrames(
                    entities, loa, z0, z1, web, $"b{i}", fore, aft);
                AddShellPlatingStripsBetweenFrames(
                    entities, loa, z0, z1, web, $"b{i}", fore, aft);
            }

            return;
        }

        // Fallback: uniform bay pitch when frame schedule unavailable.
        if (zEnd <= zStart + 1f)
            return;
        var bayIndex = 0;
        for (var z = zStart; z < zEnd - 0.35f; z += frameBay)
        {
            var z0 = z;
            var z1 = MathF.Min(z + frameBay, zEnd);
            if (z1 - z0 < 0.4f)
                break;
            AddPlatingJoinStringersBetweenFrames(
                entities, loa, z0, z1, web, $"d{bayIndex}", $"frame-F{z0:0}", $"frame-F{z1:0}");
            AddShellPlatingStripsBetweenFrames(
                entities, loa, z0, z1, web, $"d{bayIndex}", $"frame-F{z0:0}", $"frame-F{z1:0}");
            bayIndex++;
        }
    }

    /// <summary>
    /// Longitudinal at each outer-hull plating join (octagon vertex) spanning one frame bay.
    /// Outer face coincides with the plate seam — shrink section before pulling into the volume.
    /// </summary>
    public static void AddPlatingJoinStringersBetweenFrames(
        List<CadEntity> entities,
        float loa,
        float z0,
        float z1,
        float web,
        string bayTag,
        string foreFrame,
        string aftFrame)
    {
        var zc = (z0 + z1) * 0.5f;
        var halfLenZ = (z1 - z0) * 0.5f + web * 0.45f;
        var ring = InnerOctagonRing(zc, PlatingJoinInset);
        var (cy, cu) = ring.GetSectionCenter();
        for (var i = 0; i < ring.Length; i++)
        {
            var vx = ring[i].Y;
            var vu = ring[i].Up;
            var inwardY = cy - vx;
            var inwardU = cu - vu;
            var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
            var ny = inwardLen > 1e-4f ? inwardY / inwardLen : 0f;
            var nu = inwardLen > 1e-4f ? inwardU / inwardLen : 0f;
            var stiffHy = MathF.Max(0.04f, web * 0.22f);
            var stiffHu = MathF.Max(0.045f, web * 0.24f);
            var name = $"stringer-join-v{i}-{bayTag}";
            for (var shrink = 0; shrink < 8; shrink++)
            {
                var hy = stiffHy * MathF.Pow(0.88f, shrink);
                var hu = stiffHu * MathF.Pow(0.88f, shrink);
                if (hy < 0.028f || hu < 0.028f)
                    break;
                var pull = MathF.Max(hy, hu) * 1.42f;
                var y = vx + ny * pull;
                var up = vu + nu * pull;
                var before = entities.Count;
                if (!CalypsoLockGenerator.TryAddStructureBar(
                        entities, loa, name, y, up, zc, hy, hu, halfLenZ,
                        meta: new StructuralSolidMeta(
                            StructuralRoles.LongitudinalGirder, "plating-join",
                            StationZ: zc, DeckUp: up, Joins: [foreFrame, aftFrame])))
                    continue;
                for (var e = before; e < entities.Count; e++)
                {
                    var props = entities[e].Properties
                                ?? new Dictionary<string, System.Text.Json.JsonElement>();
                    props["platingJoinVertex"] =
                        System.Text.Json.JsonSerializer.SerializeToElement(i);
                    props["platingJoin"] =
                        System.Text.Json.JsonSerializer.SerializeToElement(true);
                    entities[e].Properties = props;
                }

                break;
            }
        }
    }

    /// <summary>Shell plate facets between plating-join vertices for one frame bay.</summary>
    public static void AddShellPlatingStripsBetweenFrames(
        List<CadEntity> entities,
        float loa,
        float z0,
        float z1,
        float web,
        string bayTag,
        string foreFrame,
        string aftFrame)
    {
        var zc = (z0 + z1) * 0.5f;
        var halfLenZ = (z1 - z0) * 0.5f + web * 0.2f;
        var ring = InnerOctagonRing(zc, PlatingJoinInset);
        var (cy, cu) = ring.GetSectionCenter();
        var t = MathF.Max(0.04f, web * 0.22f);
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var dy = b.Y - a.Y;
            var du = b.Up - a.Up;
            var len = MathF.Sqrt(dy * dy + du * du);
            if (len < 0.2f)
                continue;
            var y = (a.Y + b.Y) * 0.5f;
            var up = (a.Up + b.Up) * 0.5f;
            var inwardY = cy - y;
            var inwardU = cu - up;
            var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
            if (inwardLen > 1e-4f)
            {
                y += inwardY / inwardLen * (t * 0.6f);
                up += inwardU / inwardLen * (t * 0.6f);
            }

            var angle = MathF.Atan2(du, dy);
            CalypsoLockGenerator.TryAddOrientedStructureBar(
                entities, loa, $"shell-panel-{bayTag}-e{i}", y, up, zc,
                halfAlongEdge: len * 0.48f,
                halfFlange: t * 0.5f,
                halfDepthZ: halfLenZ,
                orientZ: angle,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellPanel, "bay-shell",
                    StationZ: zc, DeckUp: up, Joins: [foreFrame, aftFrame]));
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
            // Overlap into frame webs so adjacent bay girders form a continuous load path.
            var halfLenZ = MathF.Min(frameBay * 0.5f + web * 0.45f, zEnd - zc);
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
    /// Bay edges land on frame stations (overlap into web) and join nearby BH assemblies.
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
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans = null,
        float web = 0.15f,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)>? frameStations = null,
        IReadOnlyList<(string Id, float ZStem)>? bulkheadStations = null)
    {
        if (zEnd <= zStart + 0.5f || decks.Count == 0 || tPlate < 0.02f)
            return;

        var stations = frameStations is { Count: >= 2 }
            ? frameStations.Select(f => f.ZStem).OrderBy(z => z).ToList()
            : BuildUniformBayEdges(zStart, zEnd, frameBay);

        var bayIndex = 0;
        for (var i = 0; i < stations.Count - 1; i++)
        {
            var z0 = stations[i];
            var z1 = stations[i + 1];
            if (z1 - z0 < 0.4f)
                continue;

            var zc = (z0 + z1) * 0.5f;
            // Overlap into both frame webs so peri edges seat on chords (critical continuity).
            var halfLenZ = (z1 - z0) * 0.5f + web * 0.45f;
            if (halfLenZ < 0.2f)
                continue;

            var foreFrame = FrameAsmAt(frameStations, z0);
            var aftFrame = FrameAsmAt(frameStations, z1);
            var bhJoins = NearbyBulkheads(bulkheadStations, z0, z1);

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
                var deckAsm = $"deck-dk{tag}";
                if (IsOpenAtriumDeck(zc, plateTop, openAtriumSpans))
                {
                    // ENG atrium / HOLD cargo: keep shirt-sleeves CORR decks; void only the center cell.
                    if (IsHoldCargoStem(zc, openAtriumSpans))
                        continue; // HOLD = open bay, no upper plates at all
                    EmitCorridorDeckStrips(
                        entities, loa, tag, bayIndex, plateTop, tPlate, zc, halfLenZ, halfY,
                        foreFrame, aftFrame, bhJoins);
                    continue;
                }

                EmitSandwichPanel(
                    entities, loa, $"deck-sandwich-dk{tag}-z{z0:0}-b{bayIndex}",
                    0f, plateTop, tPlate, zc, halfY * 0.98f, halfLenZ, deckAsm,
                    foreFrame, aftFrame, bhJoins);
            }

            bayIndex++;
        }
    }

    private static List<float> BuildUniformBayEdges(float zStart, float zEnd, float frameBay)
    {
        var list = new List<float>();
        for (var z = zStart; z <= zEnd + 0.01f; z += frameBay)
            list.Add(MathF.Min(z, zEnd));
        if (list.Count == 0 || list[^1] < zEnd - 0.05f)
            list.Add(zEnd);
        return list;
    }

    private static string? FrameAsmAt(
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)>? frames, float zStem)
    {
        if (frames is null || frames.Count == 0)
            return null;
        var best = frames
            .Select(f => (f.Id, Dist: MathF.Abs(f.ZStem - zStem)))
            .OrderBy(f => f.Dist)
            .First();
        if (best.Dist > 0.35f)
            return null;
        var id = best.Id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? best.Id : $"frame-{best.Id}";
        var segs = id.Split('-');
        return segs.Length >= 2 ? $"frame-{segs[1]}" : id;
    }

    private static IReadOnlyList<string> NearbyBulkheads(
        IReadOnlyList<(string Id, float ZStem)>? bulkheads, float z0, float z1)
    {
        if (bulkheads is null || bulkheads.Count == 0)
            return [];
        return bulkheads
            .Where(b => b.ZStem >= z0 - 0.05f && b.ZStem <= z1 + 0.05f)
            .Select(b => $"bh-{b.Id}")
            .ToList();
    }

    /// <summary>
    /// Cellular sandwich: face-top + face-bot + internal orthogonal ribs + perimeter.
    /// Total stack thickness equals <paramref name="tPlate"/> (no overlapping solid "core" slab).
    /// </summary>
    /// <summary>Public sandwich emit for the two-bay structural proof.</summary>
    public static void EmitSandwichPanelPublic(
        List<CadEntity> entities,
        float loa,
        string baseName,
        float y,
        float plateTop,
        float tPlate,
        float zc,
        float halfY,
        float halfLenZ,
        string assembly,
        string? foreFrameAsm = null,
        string? aftFrameAsm = null,
        IReadOnlyList<string>? bulkheadAsms = null) =>
        EmitSandwichPanel(
            entities, loa, baseName, y, plateTop, tPlate, zc, halfY, halfLenZ, assembly,
            foreFrameAsm, aftFrameAsm, bulkheadAsms);

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
        string assembly,
        string? foreFrameAsm = null,
        string? aftFrameAsm = null,
        IReadOnlyList<string>? bulkheadAsms = null)
    {
        // Faces thin; residual height = cellular cavity for ribs. faceHalf ≥ TryAdd minHalf (0.012).
        var faceT = Math.Clamp(tPlate * 0.24f, 0.026f, tPlate * 0.35f);
        var coreH = MathF.Max(0.02f, tPlate - 2f * faceT);
        var faceHalf = faceT * 0.5f;
        var topCenter = plateTop - faceHalf;
        var botCenter = plateTop - tPlate + faceHalf;
        var ribUp = (topCenter - faceHalf + botCenter + faceHalf) * 0.5f;
        var ribHu = coreH * 0.48f;

        var stationJoins = new List<string> { $"{baseName}-rib", $"{baseName}-face-bot" };
        if (foreFrameAsm is not null) stationJoins.Add(foreFrameAsm);
        if (aftFrameAsm is not null) stationJoins.Add(aftFrameAsm);
        if (bulkheadAsms is not null) stationJoins.AddRange(bulkheadAsms);

        void Face(string suffix, float up, IReadOnlyList<string>? joins)
        {
            var before = entities.Count;
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-{suffix}",
                y, up, zc, halfY, faceHalf, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zc, DeckUp: plateTop, Joins: joins),
                minHalf: 0.012f);
            TagDeckSurface(entities, before, "face");
        }

        Face("face-top", topCenter, stationJoins);
        Face("face-bot", botCenter, [$"{baseName}-rib", $"{baseName}-face-top", ..stationJoins.Skip(2)]);

        // Orthogonal cellular webs inside the cavity (athwart + longitudinal).
        const float CellPitch = 1.15f;
        var ribT = MathF.Max(0.025f, faceT * 0.9f);
        var ribHalfT = ribT * 0.5f;
        var y0 = y - halfY * 0.92f;
        var y1 = y + halfY * 0.92f;
        var z0 = zc - halfLenZ * 0.92f;
        var z1 = zc + halfLenZ * 0.92f;
        var ribJoins = new List<string> { $"{baseName}-face-top", $"{baseName}-face-bot" };
        if (foreFrameAsm is not null) ribJoins.Add(foreFrameAsm);
        if (aftFrameAsm is not null) ribJoins.Add(aftFrameAsm);

        for (var yy = y0 + CellPitch * 0.5f; yy < y1 - 0.1f; yy += CellPitch)
        {
            var before = entities.Count;
            // Longitudinal rib (runs fore–aft).
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-rib-long-y{yy:0.#}",
                yy, ribUp, zc, ribHalfT, ribHu, halfLenZ * 0.9f,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
                minHalf: 0.012f);
            TagDeckSurface(entities, before, "core");
        }

        for (var zz = z0 + CellPitch * 0.5f; zz < z1 - 0.1f; zz += CellPitch)
        {
            var before = entities.Count;
            // Athwart rib (runs port–stbd) at stem station zz.
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"{baseName}-rib-ath-z{zz:0.#}",
                y, ribUp, zz, halfY * 0.9f, ribHu, ribHalfT,
                meta: new StructuralSolidMeta(
                    StructuralRoles.StructuralDeck, assembly,
                    StationZ: zz, DeckUp: plateTop, Joins: ribJoins),
                minHalf: 0.012f);
            TagDeckSurface(entities, before, "core");
        }

        // Perimeter closure — fore/aft edges join the bounding frames (critical continuity).
        var periT = MathF.Max(0.03f, ribT);
        var periSideJoins = ribJoins;
        var periAftJoins = new List<string>(ribJoins);
        if (foreFrameAsm is not null) periAftJoins.Add(foreFrameAsm);
        var periForeJoins = new List<string>(ribJoins);
        if (aftFrameAsm is not null) periForeJoins.Add(aftFrameAsm);
        if (bulkheadAsms is not null)
        {
            periAftJoins.AddRange(bulkheadAsms);
            periForeJoins.AddRange(bulkheadAsms);
        }

        void Peri(string name, float py, float pu, float pz, float phy, float phu, float phz, IReadOnlyList<string> joins)
        {
            var before = entities.Count;
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, name, py, pu, pz, phy, phu, phz,
                meta: new StructuralSolidMeta(StructuralRoles.StructuralDeck, assembly, StationZ: pz, DeckUp: plateTop, Joins: joins),
                minHalf: 0.012f);
            TagDeckSurface(entities, before, "core");
        }

        Peri($"{baseName}-peri-port", y - halfY + periT * 0.5f, ribUp, zc, periT * 0.5f, ribHu, halfLenZ * 0.95f, periSideJoins);
        Peri($"{baseName}-peri-stbd", y + halfY - periT * 0.5f, ribUp, zc, periT * 0.5f, ribHu, halfLenZ * 0.95f, periSideJoins);
        Peri($"{baseName}-peri-fore", y, ribUp, zc + halfLenZ - periT * 0.5f, halfY * 0.95f, ribHu, periT * 0.5f, periForeJoins);
        Peri($"{baseName}-peri-aft", y, ribUp, zc - halfLenZ + periT * 0.5f, halfY * 0.95f, ribHu, periT * 0.5f, periAftJoins);
    }

    private static void TagDeckSurface(List<CadEntity> entities, int beforeCount, string surface)
    {
        for (var i = beforeCount; i < entities.Count; i++)
        {
            var e = entities[i];
            e.Properties ??= new Dictionary<string, System.Text.Json.JsonElement>();
            e.Properties["deckSurface"] = System.Text.Json.JsonSerializer.SerializeToElement(surface);
        }
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
        float halfY,
        string? foreFrameAsm = null,
        string? aftFrameAsm = null,
        IReadOnlyList<string>? bulkheadAsms = null)
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
            yPort, plateTop, tPlate, zc, hy, halfLenZ, asm, foreFrameAsm, aftFrameAsm, bulkheadAsms);
        EmitSandwichPanel(
            entities, loa, $"deck-sandwich-dk{deckTag}-corrS-b{bayIndex}",
            yStbd, plateTop, tPlate, zc, hy, halfLenZ, asm, foreFrameAsm, aftFrameAsm, bulkheadAsms);
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
