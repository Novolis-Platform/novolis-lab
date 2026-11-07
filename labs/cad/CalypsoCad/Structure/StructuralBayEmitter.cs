using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// One midbody structural bay (piece-by-piece): stiffened shell strips, two open
/// frames, longitudinals, one DK0 sandwich, join stubs. No rooms/outfit.
/// Canonical: F24–F28 (z 24–28). See calypso-shipbuilding-101.
/// </summary>
internal static class StructuralBayEmitter
{
    public const float CanonicalZ0 = 24f;
    public const float CanonicalZ1 = 28f;
    public const float AdjacentZ1 = 32f;
    public const float HoldBayZ0 = 52f;
    public const float HoldBayZ1 = 56f;

    public sealed record BaySpec(float Z0, float Z1, string ForeFrameId, string AftFrameId, bool HoldPerimeter);

    public static BaySpec Canonical => new(CanonicalZ0, CanonicalZ1, "frame-F24", "frame-F28", HoldPerimeter: false);
    public static BaySpec Adjacent => new(CanonicalZ1, AdjacentZ1, "frame-F28", "frame-F32", HoldPerimeter: false);
    public static BaySpec HoldBay => new(HoldBayZ0, HoldBayZ1, "frame-F52", "frame-F56", HoldPerimeter: true);

    /// <summary>
    /// Emit one or two midbody bays (and optional HOLD perimeter bay). Caller must have prepared loft stations.
    /// </summary>
    public static void Emit(
        List<CadEntity> entities,
        float loa,
        float web,
        float tPlate,
        IReadOnlyList<float> deckUps,
        Func<float, float, List<(float Y0, float Y1)>> passageGapsAt,
        bool includeAdjacentBay = false,
        bool includeHoldBay = false,
        Func<float, float, List<FrameWebPlateBuilder.WebPlatePortal>>? hatchPortalsAt = null)
    {
        EmitBay(entities, loa, web, tPlate, deckUps, passageGapsAt, Canonical, hatchPortalsAt: hatchPortalsAt);
        if (includeAdjacentBay)
            EmitBay(entities, loa, web, tPlate, deckUps, passageGapsAt, Adjacent, skipSharedForeFrame: true, hatchPortalsAt: hatchPortalsAt);
        if (includeHoldBay)
            EmitBay(entities, loa, web, tPlate, deckUps, passageGapsAt, HoldBay, hatchPortalsAt: hatchPortalsAt);
    }

    private static void EmitBay(
        List<CadEntity> entities,
        float loa,
        float web,
        float tPlate,
        IReadOnlyList<float> deckUps,
        Func<float, float, List<(float Y0, float Y1)>> passageGapsAt,
        BaySpec bay,
        bool skipSharedForeFrame = false,
        Func<float, float, List<FrameWebPlateBuilder.WebPlatePortal>>? hatchPortalsAt = null)
    {
        var halfDepthZ = web * 0.5f;
        var zMid = (bay.Z0 + bay.Z1) * 0.5f;
        var halfLenZ = (bay.Z1 - bay.Z0) * 0.5f;
        var dk0 = deckUps.Count > 1 ? deckUps[1] : 4.4f;
        var floorUp = deckUps.Count > 0 ? deckUps[0] : 0.5f;

        // 1) Stiffened shell panel strips (IML faces) for this bay length.
        EmitShellPanels(entities, loa, bay, zMid, halfLenZ, web);

        // 2) Open transverse frames at bay ends (HOLD = perimeter + floor only).
        void Place(string id, float zStem)
        {
            CalypsoLockGenerator.SectionAtPublic(zStem, out var halfB, out var halfH, out _, out _, out _);
            if (halfB < 0.5f || halfH < 0.5f)
                return;
            if (bay.HoldPerimeter)
            {
                CalypsoShipbuildingFrames.AddHoldPerimeterFrame(
                    entities, loa, id, zStem, web, halfDepthZ, deckUps, tPlate);
                return;
            }

            var gaps = passageGapsAt(zStem, 0.28f);
            // Open frames: CORR track gaps only — hatch punches belong on pressure BHs (one model).
            _ = hatchPortalsAt;
            CalypsoShipbuildingFrames.AddTransverseFrameWeb(
                entities, loa, id, zStem, web, halfDepthZ, gaps, bowAligned: false, deckUps, hatchPortals: null);
            CalypsoShipbuildingFrames.AddTransverseDeckBeams(
                entities, loa, zStem, id, web, halfDepthZ, deckUps, gaps, tPlate, openAtriumSpans: null);
        }

        if (!skipSharedForeFrame)
            Place(bay.ForeFrameId, bay.Z0);
        Place(bay.AftFrameId, bay.Z1);

        // 3) Longitudinal girders — HOLD: floor deck only.
        var girderDecks = bay.HoldPerimeter ? (IReadOnlyList<float>)[floorUp] : deckUps;
        CalypsoShipbuildingFrames.AddDeckLongitudinalGirders(
            entities, loa, bay.Z0, bay.Z1, web, bay.Z1 - bay.Z0, girderDecks, tPlate, openAtriumSpans: null);

        // 3b) Frame ↔ girder join stubs.
        var stations = new List<(string Id, float ZStem, bool BowAligned)>();
        if (!skipSharedForeFrame)
            stations.Add((bay.ForeFrameId, bay.Z0, false));
        stations.Add((bay.AftFrameId, bay.Z1, false));
        CalypsoShipbuildingFrames.AddFrameGirderJoinStubs(
            entities, loa, stations, web, halfDepthZ, girderDecks, tPlate,
            z => bay.HoldPerimeter, openAtriumSpans: null);

        // 4) Cellular sandwich — midbody DK0; HOLD floor only (DK−1).
        if (bay.HoldPerimeter)
        {
            CalypsoShipbuildingFrames.AddCellularSandwichDecks(
                entities, loa, bay.Z0, bay.Z1, bay.Z1 - bay.Z0,
                [(-1, floorUp)], tPlate, openAtriumSpans: null);
        }
        else
        {
            CalypsoShipbuildingFrames.AddCellularSandwichDecks(
                entities, loa, bay.Z0, bay.Z1, bay.Z1 - bay.Z0,
                [(0, dk0)], tPlate, openAtriumSpans: null);
        }
    }

    private static void EmitShellPanels(
        List<CadEntity> entities,
        float loa,
        BaySpec bay,
        float zMid,
        float halfLenZ,
        float web)
    {
        CalypsoLockGenerator.SectionAtPublic(zMid, out var halfB, out var halfH, out var mid, out _, out _);
        var t = MathF.Max(0.04f, CalypsoLockGenerator.TShellPublic * 8f); // visible panel thickness for bay viz
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset;
        var yFace = halfB - inset - t * 0.5f;
        var uBot = mid - halfH + inset + t * 0.5f;
        var uTop = mid + halfH - inset - t * 0.5f;
        var uMid = mid;
        var huSide = (uTop - uBot) * 0.48f;
        if (yFace < 0.4f || huSide < 0.4f)
            return;

        var shellAsm = $"bay-shell-{bay.Z0:0}-{bay.Z1:0}";
        var frameJoins = new[] { bay.ForeFrameId, bay.AftFrameId };

        // Side shell as three elevation bands (survive IML clip better than one tall plate).
        var bandHu = MathF.Max(0.35f, huSide * 0.28f);
        for (var i = -1; i <= 1; i++)
        {
            var up = uMid + i * (huSide * 0.55f);
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"shell-panel-port-{i}-z{bay.Z0:0}",
                -yFace, up, zMid, t * 0.55f, bandHu, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellPanel, shellAsm,
                    StationZ: zMid, DeckUp: up, Joins: frameJoins));
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"shell-panel-stbd-{i}-z{bay.Z0:0}",
                yFace, up, zMid, t * 0.55f, bandHu, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellPanel, shellAsm,
                    StationZ: zMid, DeckUp: up, Joins: frameJoins));
        }

        // Bottom / top face strips (athwart).
        var halfYBot = MathF.Min(yFace * 0.9f, CalypsoLockGenerator.HalfBeamInsidePublic(zMid, uBot, inset) - t);
        if (halfYBot > 0.5f)
        {
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"shell-panel-bot-z{bay.Z0:0}",
                0f, uBot, zMid, halfYBot, t * 0.55f, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellPanel, shellAsm,
                    StationZ: zMid, DeckUp: uBot, Joins: frameJoins));
        }

        var halfYTop = MathF.Min(yFace * 0.9f, CalypsoLockGenerator.HalfBeamInsidePublic(zMid, uTop, inset) - t);
        if (halfYTop > 0.5f)
        {
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"shell-panel-top-z{bay.Z0:0}",
                0f, uTop, zMid, halfYTop, t * 0.55f, halfLenZ,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellPanel, shellAsm,
                    StationZ: zMid, DeckUp: uTop, Joins: frameJoins));
        }

        // Longitudinal stiffeners on port/stbd inner faces (welded to panels).
        var stiffHu = MathF.Max(0.06f, web * 0.4f);
        var stiffHy = MathF.Max(0.05f, web * 0.3f);
        for (var i = -1; i <= 1; i++)
        {
            var up = uMid + i * (huSide * 0.55f);
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"shell-stiffener-port-{i}-z{bay.Z0:0}",
                -yFace + t * 0.75f, up, zMid, stiffHy, stiffHu, halfLenZ * 0.95f,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellStiffener, shellAsm,
                    StationZ: zMid, DeckUp: up, Joins: [shellAsm, .. frameJoins]));
            CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, $"shell-stiffener-stbd-{i}-z{bay.Z0:0}",
                yFace - t * 0.75f, up, zMid, stiffHy, stiffHu, halfLenZ * 0.95f,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellStiffener, shellAsm,
                    StationZ: zMid, DeckUp: up, Joins: [shellAsm, .. frameJoins]));
        }
    }

    public static void TagDocument(CadDocument cad, bool includeAdjacent, bool includeHold = false)
    {
        cad.Properties ??= new Dictionary<string, JsonElement>();
        var stations = new List<string> { "F24", "F28" };
        if (includeAdjacent)
            stations.Add("F32");
        if (includeHold)
        {
            stations.Add("F52");
            stations.Add("F56");
        }

        cad.Properties["structuralBay"] = JsonSerializer.SerializeToElement(new
        {
            z0 = CanonicalZ0,
            z1 = includeAdjacent ? AdjacentZ1 : CanonicalZ1,
            holdZ0 = includeHold ? HoldBayZ0 : (float?)null,
            holdZ1 = includeHold ? HoldBayZ1 : (float?)null,
            stations,
            adjacent = includeAdjacent,
            holdBay = includeHold,
        });
        cad.Properties["maturity"] = JsonSerializer.SerializeToElement("structural-bay");
    }

    public static bool IsHoldBayEntity(CadEntity e)
    {
        if (e.Properties is not null
            && e.Properties.TryGetValue("stationZ", out var sz)
            && sz.ValueKind == JsonValueKind.Number)
        {
            var z = (float)sz.GetDouble();
            if (z >= HoldBayZ0 - 0.5f && z <= HoldBayZ1 + 0.5f)
                return true;
        }

        var n = e.Name ?? "";
        return n.Contains("F52", StringComparison.OrdinalIgnoreCase)
               || n.Contains("F56", StringComparison.OrdinalIgnoreCase)
               || n.Contains("-holdfloor", StringComparison.OrdinalIgnoreCase)
               || (n.Contains("-z52", StringComparison.OrdinalIgnoreCase)
                   || n.Contains("-z56", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsShellEntity(CadEntity e)
    {
        var n = e.Name ?? "";
        if (n.StartsWith("shell-panel-", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("shell-stiffener-", StringComparison.OrdinalIgnoreCase))
            return true;
        if (e.Properties is not null
            && e.Properties.TryGetValue("structuralRole", out var r)
            && r.ValueKind == JsonValueKind.String)
        {
            var role = r.GetString() ?? "";
            return string.Equals(role, StructuralRoles.ShellPanel, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(role, StructuralRoles.ShellStiffener, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public static bool IsFrameOrGirder(CadEntity e)
    {
        var n = e.Name ?? "";
        return n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
               || n.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
               || n.Contains("-deckbeam-", StringComparison.OrdinalIgnoreCase)
               || n.Contains("-join-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDeck(CadEntity e)
    {
        var n = e.Name ?? "";
        return n.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase);
    }

    public static HashSet<Guid> EntityIdsForTest(CadDocument doc, char test)
    {
        var ids = new HashSet<Guid>();
        foreach (var e in doc.Entities ?? [])
        {
            var include = test switch
            {
                'A' => IsShellEntity(e),
                'B' => IsShellEntity(e) || IsFrameOrGirder(e),
                'C' or 'D' or 'E' => IsShellEntity(e) || IsFrameOrGirder(e) || IsDeck(e),
                _ => true,
            };
            if (include)
                ids.Add(e.Id);
        }

        return ids;
    }
}
