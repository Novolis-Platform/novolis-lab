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
        // Floor chord only — full athwart span; cargo void above remains clear.
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, $"{frameId}-chord-{floorUp:0.#}-holdfloor",
            0f, floorUp, zStem, halfY, chordHalf, halfDepthZ,
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
        // Connected annular plate-web from the authoritative hull section — not independent bars.
        CalypsoLockGenerator.SectionAtPublic(zStem, out var halfBCheck, out var halfHCheck, out _, out _, out _);
        if (halfBCheck < 1.45f || halfHCheck < 1.45f)
            return;

        var ringInset = exactInsetOnly ? inset : inset + web * 1.05f + 0.18f;
        var outer = InnerOctagonRing(zStem, ringInset);
        if (outer.Length < 3)
        {
            StructurePlacementLog.Record(
                frameId, 0f, 0f, zStem, 0f, 0f, halfDepthZ,
                "hull section ring collapsed after geometric inset");
            return;
        }

        var webWidth = MathF.Max(0.08f, web);
        var inner = PolygonInset2D.Inset(outer, webWidth);
        if (inner.Length != outer.Length)
        {
            StructurePlacementLog.Record(
                frameId, 0f, 0f, zStem, 0f, 0f, halfDepthZ,
                $"annular inset collapsed (webWidth={webWidth:F3})");
            return;
        }

        var zCad = loa * 0.5f - zStem;
        EditableMesh mesh;
        try
        {
            mesh = AnnularPlateExtrude.ExtrudeAlongZ(
                outer, inner, zCad, halfDepthZ, $"{frameId}-{namePrefix}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL {frameId}-{namePrefix} annular extrude: {ex.Message}");
            StructurePlacementLog.Record(
                frameId, 0f, 0f, zStem, 0f, 0f, halfDepthZ,
                $"annular extrude failed: {ex.Message}");
            return;
        }

        var deviationMm = HullSectionAuthority.MaxRingDeviation(outer, zStem, ringInset) * 1000f;
        FrameContourEmitLog.Record(
            $"{frameId}-{namePrefix}", zStem, ringInset, deviationMm, mesh.TriangleCount, mesh.VertexCount);

        CalypsoLockGenerator.TryAddStructureMesh(
            entities,
            loa,
            $"{frameId}-{namePrefix}-annulus",
            zStem,
            mesh,
            fabPartId: null,
            meta: new StructuralSolidMeta(
                StructuralRoles.TransverseFrame,
                frameId.Contains('-')
                    ? string.Join('-', frameId.Split('-').Take(2))
                    : frameId,
                StationZ: zStem));

        if (deviationMm > 25f)
        {
            Console.Error.WriteLine(
                $"  WARN {frameId}: frame outer ring max deviation {deviationMm:0.0} mm from hull section");
        }
    }

    /// <summary>
    /// Authoritative transverse hull section at <paramref name="zStem"/> (OML octagon from loft metrics).
    /// </summary>
    public static (float Y, float Up)[] OuterOctagonRing(float zStem) =>
        Geometry.HullGeometry.RequireActive().OuterOctagonRing(zStem);

    /// <summary>
    /// Structural ring: geometric inward offset of the authoritative hull section.
    /// Does not shrink half-beam / half-height independently (that distorts chamfer facets).
    /// </summary>
    public static (float Y, float Up)[] InnerOctagonRing(float zStem, float skinInset) =>
        Geometry.HullGeometry.RequireActive().InnerOctagonRing(zStem, skinInset);

    /// <summary>
    /// Path-swept shell plating between structural joint stations (same Z set as knuckle paths).
    /// Each facet is a ruled thin plate between consecutive knuckles at z0 and z1.
    /// </summary>
    public static void AddOuterHullShellPlating(
        List<CadEntity> entities,
        float loa,
        float web,
        IReadOnlyList<float> jointZsStem)
    {
        if (jointZsStem.Count < 2)
            return;
        var zs = jointZsStem.OrderBy(z => z).ToList();
        for (var i = 0; i < zs.Count - 1; i++)
        {
            var z0 = zs[i];
            var z1 = zs[i + 1];
            if (z1 - z0 < 0.35f)
                continue;
            AddShellPlatingStripsBetweenFrames(
                entities, loa, z0, z1, web, $"s{i}",
                $"z{z0:0.#}", $"z{z1:0.#}");
        }
    }

    /// <summary>
    /// Obsolete mid-bay axis-aligned knuckle bars — retained only for StructuralBayAssembly proof
    /// callers. Prefer <c>HullKnuckleStringerGenerator</c> for whole-ship emit.
    /// </summary>
    [Obsolete("Use HullKnuckleStringerGenerator / HullKnucklePath — mid-bay boxes do not follow 3D diagonals.")]
    public static void AddOuterHullStringers(
        List<CadEntity> entities,
        float loa,
        float zStart,
        float zEnd,
        float web,
        float frameBay,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)>? frameStations = null)
    {
        _ = zStart;
        _ = zEnd;
        _ = frameBay;
        var zs = (frameStations ?? []).Select(f => f.ZStem).ToList();
        AddOuterHullShellPlating(entities, loa, web, zs);
    }

    /// <summary>
    /// Obsolete mid-bay plating-join bar. Prefer path-swept knuckle segments via
    /// <c>HullKnuckleStringerGenerator</c>. No-op — kept so old call sites compile until removed.
    /// </summary>
    [Obsolete("Use HullKnuckleStringerGenerator — mid-bay boxes cannot follow knuckle diagonals.")]
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
        _ = entities;
        _ = loa;
        _ = z0;
        _ = z1;
        _ = web;
        _ = bayTag;
        _ = foreFrame;
        _ = aftFrame;
    }

    /// <summary>
    /// Shell plate facets as ruled meshes between knuckle edges at z0 and z1
    /// (follows hull taper — not a mid-bay axis-aligned box).
    /// </summary>
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
        var ring0 = InnerOctagonRing(z0, PlatingJoinInset);
        var ring1 = InnerOctagonRing(z1, PlatingJoinInset);
        if (ring0.Length != ring1.Length || ring0.Length < 3)
            return;
        var (cy0, cu0) = ring0.GetSectionCenter();
        var (cy1, cu1) = ring1.GetSectionCenter();
        var t = MathF.Max(0.04f, web * 0.22f);
        var zc = (z0 + z1) * 0.5f;
        for (var i = 0; i < ring0.Length; i++)
        {
            var a0 = ring0[i];
            var b0 = ring0[(i + 1) % ring0.Length];
            var a1 = ring1[i];
            var b1 = ring1[(i + 1) % ring1.Length];
            var edge0 = MathF.Sqrt((b0.Y - a0.Y) * (b0.Y - a0.Y) + (b0.Up - a0.Up) * (b0.Up - a0.Up));
            if (edge0 < 0.15f)
                continue;

            var midY0 = (a0.Y + b0.Y) * 0.5f;
            var midU0 = (a0.Up + b0.Up) * 0.5f;
            var midY1 = (a1.Y + b1.Y) * 0.5f;
            var midU1 = (a1.Up + b1.Up) * 0.5f;
            var inY = ((cy0 - midY0) + (cy1 - midY1)) * 0.5f;
            var inU = ((cu0 - midU0) + (cu1 - midU1)) * 0.5f;
            var inLen = MathF.Sqrt(inY * inY + inU * inU);
            var ny = inLen > 1e-4f ? inY / inLen : 0f;
            var nu = inLen > 1e-4f ? inU / inLen : 0f;

            var wa0 = CalypsoLockGenerator.LockToWorldPublic(a0.Y, a0.Up, z0, loa);
            var wb0 = CalypsoLockGenerator.LockToWorldPublic(b0.Y, b0.Up, z0, loa);
            var wa1 = CalypsoLockGenerator.LockToWorldPublic(a1.Y, a1.Up, z1, loa);
            var wb1 = CalypsoLockGenerator.LockToWorldPublic(b1.Y, b1.Up, z1, loa);
            // Inward in lock Y/Up maps to CAD (+X, +Y); Z component unused for section normal.
            var inward = new System.Numerics.Vector3(ny, nu, 0f);
            if (inward.LengthSquared() < 1e-8f)
                inward = System.Numerics.Vector3.UnitX;

            var mesh = ShellPanelMesh.Build(wa0, wb0, wa1, wb1, inward, t);
            var name = $"shell-panel-{bayTag}-e{i}";
            var meta = new StructuralSolidMeta(
                StructuralRoles.ShellPanel, "bay-shell",
                StationZ: zc, DeckUp: (midU0 + midU1) * 0.5f, Joins: [foreFrame, aftFrame]);
            if (!CalypsoLockGenerator.TryAddStructureMesh(
                    entities, loa, name, zc, mesh, fabPartId: $"P-SHELL-{bayTag}-E{i}", meta: meta))
                continue;
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
        // Passage clears live in the web/BH plate — deck beams stay one full-athwart member.
        _ = athwartGaps;
        foreach (var deckUp in deckUps)
        {
            if (IsOpenAtriumDeck(zStem, deckUp, openAtriumSpans))
                continue;

            var deckInset = DefaultSkinInset + 0.28f;
            var halfUp = web * 0.35f;
            var beamUp = deckUp - tPlate - halfUp;
            // Nest across beam depth so ends clear the IML chamfer without chopping for passages.
            var halfY = MathF.Min(
                CalypsoLockGenerator.HalfBeamInsidePublic(zStem, beamUp, deckInset),
                MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, beamUp - halfUp, deckInset),
                    CalypsoLockGenerator.HalfBeamInsidePublic(zStem, beamUp + halfUp, deckInset))) - web * 1.25f;
            if (halfY < 0.35f)
                continue;
            CalypsoLockGenerator.TryAddStructureBar(
                entities,
                loa,
                $"{frameId}-deckbeam-{deckUp:0.#}",
                0f,
                beamUp,
                zStem,
                halfY,
                halfUp,
                halfDepthZ * 0.8f);
        }
    }

    /// <summary>
    /// Cellular sandwich decks from hull-conforming footprints.
    /// Cuts at exact ENG/HOLD bounds; each longitudinal sample uses local half-beam
    /// (never the bay minimum rectangle).
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

        var frameZs = frameStations is { Count: >= 2 }
            ? frameStations.Select(f => f.ZStem).OrderBy(z => z).ToList()
            : BuildUniformBayEdges(zStart, zEnd, frameBay);
        var stations = HullDeckGeometry.BuildDeckCutStations(frameZs, openAtriumSpans, zStart, zEnd);

        var panelIndex = 0;
        for (var i = 0; i < stations.Count - 1; i++)
        {
            var z0 = stations[i];
            var z1 = stations[i + 1];
            if (z1 - z0 < 0.35f)
                continue;

            var foreFrame = FrameAsmAt(frameStations, z0);
            var aftFrame = FrameAsmAt(frameStations, z1);
            var bhJoins = NearbyBulkheads(bulkheadStations, z0, z1);

            foreach (var (deckNum, plateTop) in decks)
            {
                var tag = deckNum < 0 ? "m1" : deckNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var deckAsm = $"deck-dk{tag}";
                var mode = HullDeckGeometry.ClassifySpan(z0, z1, plateTop, openAtriumSpans);
                if (mode == HullDeckGeometry.DeckSpanMode.Skip)
                    continue;

                var segments = HullDeckGeometry.HullConformingSegments(z0, z1, plateTop, web);
                if (segments.Count == 0)
                {
                    // Bow: lock deck elevation can sit below the lofted IML floor — skip, not a placement bug.
                    var zMid = (z0 + z1) * 0.5f;
                    var floor = CalypsoLockGenerator.MinUpInsidePublic(zMid, 0f, 0f);
                    if (plateTop + 0.02f >= floor)
                    {
                        StructurePlacementLog.Record(
                            $"deck-sandwich-dk{tag}-z{z0:0}-{z1:0}",
                            0f, plateTop, zMid, 0f, 0f, 0f,
                            "no hull-conforming half-beam along span");
                    }

                    continue;
                }

                var segI = 0;
                foreach (var (za, zb, zc, halfY) in segments)
                {
                    // Abutting footprints only — do not inflate halfLenZ into the next panel.
                    var halfLenZ = (zb - za) * 0.5f;
                    if (halfLenZ < 0.15f)
                        continue;

                    if (mode == HullDeckGeometry.DeckSpanMode.CorridorOnly)
                    {
                        EmitCorridorDeckStrips(
                            entities, loa, tag, panelIndex * 10 + segI, plateTop, tPlate, zc, halfLenZ, halfY,
                            foreFrame, aftFrame, bhJoins);
                    }
                    else
                    {
                        EmitSandwichPanel(
                            entities, loa,
                            $"deck-sandwich-dk{tag}-z{za:0.#}-s{segI}-b{panelIndex}",
                            0f, plateTop, tPlate, zc, halfY, halfLenZ, deckAsm,
                            foreFrame, aftFrame, bhJoins);
                    }

                    segI++;
                }
            }

            panelIndex++;
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
        // Fit stack under plateTop — DK−1 sole often has no bilge depth for a full tPlate.
        var stack = FitSandwichStack(y, plateTop, tPlate, zc, halfY, halfLenZ);
        if (stack is null)
        {
            EmitThinSolePlate(
                entities, loa, baseName, y, plateTop, zc, halfY, halfLenZ, assembly,
                foreFrameAsm, aftFrameAsm, bulkheadAsms);
            return;
        }

        var (faceT, coreH, faceHalf, topCenter, botCenter, ribUp, ribHu, fitHalfY, fitHalfZ) = stack.Value;

        // One shared footprint for faces, peri, and cores (no independent rib widths).
        var fpHy = fitHalfY;
        var fpHz = fitHalfZ;
        if (!CalypsoLockGenerator.TryFitBoxHalfExtents(y, topCenter, zc, ref fpHy, faceHalf, ref fpHz))
        {
            StructurePlacementLog.Record(baseName + "-footprint", y, topCenter, zc, fitHalfY, faceHalf, fitHalfZ,
                "shared footprint fit failed");
            return;
        }

        var hyBot = fpHy;
        var hzBot = fpHz;
        if (!CalypsoLockGenerator.TryFitBoxHalfExtents(y, botCenter, zc, ref hyBot, faceHalf, ref hzBot))
        {
            StructurePlacementLog.Record(baseName + "-footprint-bot", y, botCenter, zc, fpHy, faceHalf, fpHz,
                "shared footprint bot fit failed");
            return;
        }

        fpHy = MathF.Min(fpHy, hyBot);
        fpHz = MathF.Min(fpHz, hzBot);

        var stationJoins = new List<string> { $"{baseName}-rib", $"{baseName}-face-bot" };
        if (foreFrameAsm is not null) stationJoins.Add(foreFrameAsm);
        if (aftFrameAsm is not null) stationJoins.Add(aftFrameAsm);
        if (bulkheadAsms is not null) stationJoins.AddRange(bulkheadAsms);

        void Face(string suffix, float up, IReadOnlyList<string>? joins)
        {
            var before = entities.Count;
            var id = $"{baseName}-{suffix}";
            if (!CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, id,
                    y, up, zc, fpHy, faceHalf, fpHz,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.StructuralDeck, assembly,
                        StationZ: zc, DeckUp: plateTop, Joins: joins),
                    minHalf: 0.012f))
            {
                StructurePlacementLog.Record(id, y, up, zc, fpHy, faceHalf, fpHz);
                return;
            }

            TagDeckSurface(entities, before, "face");
        }

        Face("face-top", topCenter, stationJoins);
        Face("face-bot", botCenter, [$"{baseName}-rib", $"{baseName}-face-top", ..stationJoins.Skip(2)]);

        const float CellPitch = 1.15f;
        var ribT = MathF.Max(0.025f, faceT * 0.9f);
        var ribHalfT = ribT * 0.5f;
        var periT = MathF.Max(0.03f, ribT);
        var ribJoins = new List<string> { $"{baseName}-face-top", $"{baseName}-face-bot" };
        if (foreFrameAsm is not null) ribJoins.Add(foreFrameAsm);
        if (aftFrameAsm is not null) ribJoins.Add(aftFrameAsm);

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
            // No further athwart/long shrink — stay on the shared footprint.
            if (!CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, name, py, pu, pz, phy, phu, phz,
                    meta: new StructuralSolidMeta(StructuralRoles.StructuralDeck, assembly, StationZ: pz, DeckUp: plateTop, Joins: joins),
                    minHalf: 0.012f))
            {
                StructurePlacementLog.Record(name, py, pu, pz, phy, phu, phz);
                return;
            }

            TagDeckSurface(entities, before, "peri");
        }

        const float FrameSeat = 0.06f;
        var periHz = MathF.Max(0.08f, fpHz - periT);
        var periHy = MathF.Max(0.2f, fpHy - periT);
        Peri($"{baseName}-peri-port", y - fpHy + periT * 0.5f, ribUp, zc, periT * 0.5f, ribHu, periHz, periSideJoins);
        Peri($"{baseName}-peri-stbd", y + fpHy - periT * 0.5f, ribUp, zc, periT * 0.5f, ribHu, periHz, periSideJoins);
        Peri($"{baseName}-peri-fore", y, ribUp, zc + fpHz - periT * 0.5f + FrameSeat * 0.5f,
            periHy, ribHu, periT * 0.5f + FrameSeat * 0.5f, periForeJoins);
        Peri($"{baseName}-peri-aft", y, ribUp, zc - fpHz + periT * 0.5f - FrameSeat * 0.5f,
            periHy, ribHu, periT * 0.5f + FrameSeat * 0.5f, periAftJoins);

        // Cores strictly inside peri — same footprint, inset by peri thickness.
        var coreHalfY = MathF.Max(0.2f, fpHy - periT - 0.02f);
        var coreHalfZ = MathF.Max(0.12f, fpHz - periT - 0.02f);
        var y0 = y - coreHalfY;
        var y1 = y + coreHalfY;
        var z0 = zc - coreHalfZ;
        var z1 = zc + coreHalfZ;
        var ribI = 0;

        for (var yy = y0 + CellPitch * 0.5f; yy < y1 - 0.05f; yy += CellPitch)
        {
            var before = entities.Count;
            var id = $"{baseName}-rib-long-y{yy:0.#}";
            if (!CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, id,
                    yy, ribUp, zc, ribHalfT, ribHu, coreHalfZ * 0.98f,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.StructuralDeck, assembly,
                        StationZ: zc, DeckUp: plateTop, Joins: ribJoins),
                    minHalf: 0.012f))
            {
                StructurePlacementLog.Record(id, yy, ribUp, zc, ribHalfT, ribHu, coreHalfZ);
                continue;
            }

            TagDeckSurface(entities, before, "core", inspectSample: ribI % 3 == 0);
            ribI++;
        }

        for (var zz = z0 + CellPitch * 0.5f; zz < z1 - 0.05f; zz += CellPitch)
        {
            var before = entities.Count;
            var id = $"{baseName}-rib-ath-z{zz:0.#}";
            if (!CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, id,
                    y, ribUp, zz, coreHalfY * 0.98f, ribHu, ribHalfT,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.StructuralDeck, assembly,
                        StationZ: zz, DeckUp: plateTop, Joins: ribJoins),
                    minHalf: 0.012f))
            {
                StructurePlacementLog.Record(id, y, ribUp, zz, coreHalfY, ribHu, ribHalfT);
                continue;
            }

            TagDeckSurface(entities, before, "core", inspectSample: ribI % 3 == 0);
            ribI++;
        }
    }

    /// <summary>
    /// Single thin sole when bilge clearance forbids a cellular sandwich (bow DK−1).
    /// Still records a face-top + face-bot pair for parity / inspection layers.
    /// </summary>
    private static void EmitThinSolePlate(
        List<CadEntity> entities,
        float loa,
        string baseName,
        float y,
        float plateTop,
        float zc,
        float halfY,
        float halfLenZ,
        string assembly,
        string? foreFrameAsm,
        string? aftFrameAsm,
        IReadOnlyList<string>? bulkheadAsms)
    {
        // Bilge clearance zero: sit the sole ON the lock plate elevation (bottom = plateTop).
        const float FaceHalf = 0.013f;
        var botCenter = plateTop + FaceHalf;
        var topCenter = plateTop + FaceHalf * 3f;
        var hy = halfY;
        var hz = MathF.Min(halfLenZ, 0.35f);
        if (!CalypsoLockGenerator.InsideHullPublic(y, plateTop + 0.02f, zc, 0.02f))
            return; // elevation outside loft — caller already skipped when possible
        if (!CalypsoLockGenerator.TryFitBoxHalfExtents(y, botCenter, zc, ref hy, FaceHalf, ref hz)
            || hy < 0.35f)
        {
            StructurePlacementLog.Record(
                baseName, y, plateTop, zc, halfY, FaceHalf, halfLenZ,
                "thin sole does not fit on plateTop inside IML");
            return;
        }

        var joins = new List<string>();
        if (foreFrameAsm is not null) joins.Add(foreFrameAsm);
        if (aftFrameAsm is not null) joins.Add(aftFrameAsm);
        if (bulkheadAsms is not null) joins.AddRange(bulkheadAsms);

        void Face(string suffix, float up, IReadOnlyList<string>? j)
        {
            var before = entities.Count;
            var id = $"{baseName}-{suffix}";
            var fHy = hy;
            var fHz = hz;
            if (!CalypsoLockGenerator.TryFitBoxHalfExtents(y, up, zc, ref fHy, FaceHalf, ref fHz)
                || !CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, id, y, up, zc, fHy, FaceHalf, fHz,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.StructuralDeck, assembly,
                        StationZ: zc, DeckUp: plateTop, Joins: j),
                    minHalf: 0.012f))
            {
                StructurePlacementLog.Record(id, y, up, zc, hy, FaceHalf, hz);
                return;
            }

            TagDeckSurface(entities, before, "face");
        }

        Face("face-top", topCenter, joins);
        Face("face-bot", botCenter, joins);
    }

    /// <summary>
    /// Reduce sandwich depth under <paramref name="plateTop"/> until the AABB fits the IML
    /// (DK−1 sole often cannot host a full <paramref name="tPlate"/> drop into the bilge).
    /// </summary>
    private static (float FaceT, float CoreH, float FaceHalf, float TopCenter, float BotCenter,
        float RibUp, float RibHu, float HalfY, float HalfZ)? FitSandwichStack(
        float y,
        float plateTop,
        float tPlate,
        float zc,
        float halfY,
        float halfLenZ)
    {
        var hy = halfY;
        var hz = halfLenZ;
        for (var drop = tPlate; drop >= 0.052f; drop -= 0.004f)
        {
            var faceT = MathF.Min(drop * 0.35f, MathF.Max(0.026f, drop * 0.24f));
            if (faceT * 2f >= drop - 0.014f)
                faceT = MathF.Max(0.026f, (drop - 0.016f) * 0.35f);
            var faceHalf = faceT * 0.5f;
            if (faceHalf < 0.012f)
                continue;
            var coreH = MathF.Max(0.014f, drop - 2f * faceT);
            var topCenter = plateTop - faceHalf;
            var botCenter = plateTop - drop + faceHalf;
            var ribUp = (topCenter - faceHalf + botCenter + faceHalf) * 0.5f;
            var ribHu = coreH * 0.48f;
            hy = halfY;
            hz = halfLenZ;
            if (!CalypsoLockGenerator.TryFitBoxHalfExtents(y, topCenter, zc, ref hy, faceHalf, ref hz))
                continue;
            var hyBot = hy;
            var hzBot = hz;
            if (!CalypsoLockGenerator.TryFitBoxHalfExtents(y, botCenter, zc, ref hyBot, faceHalf, ref hzBot))
                continue;
            hy = MathF.Min(hy, hyBot);
            hz = MathF.Min(hz, hzBot);
            if (hy < 0.35f || hz < 0.12f)
                continue;
            return (faceT, coreH, faceHalf, topCenter, botCenter, ribUp, ribHu, hy, hz);
        }

        return null;
    }

    private static void TagDeckSurface(
        List<CadEntity> entities,
        int beforeCount,
        string surface,
        bool inspectSample = false)
    {
        for (var i = beforeCount; i < entities.Count; i++)
        {
            var e = entities[i];
            e.Properties ??= new Dictionary<string, System.Text.Json.JsonElement>();
            e.Properties["deckSurface"] = System.Text.Json.JsonSerializer.SerializeToElement(surface);
            if (inspectSample)
                e.Properties["deckInspectSample"] = System.Text.Json.JsonSerializer.SerializeToElement(true);
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
