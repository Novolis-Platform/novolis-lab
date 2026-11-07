using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Two-bay midbody proof assembly (constant-width hull): open perimeter frames,
/// longitudinals, stiffened shell strips, cellular DK0 panels, geometric joints.
/// No pressure BH walls, no rooms/outfit. Gate before full-ship structure.
/// </summary>
internal static class StructuralBayAssembly
{
    public const float BayPitchM = 4f;
    public const float Bay0Z0 = 24f;
    public const float Bay0Z1 = 28f;
    public const float Bay1Z1 = 32f;

    public static (CadDocument Doc, List<StructuralJoint> Joints, List<CadEntity> Entities) Emit(
        float loa,
        float web,
        float tPlate,
        float deckUp)
    {
        var entities = new List<CadEntity>();
        var joints = new List<StructuralJoint>();
        var halfDepth = web * 0.5f;
        var stations = new[]
        {
            ("frame-F24", Bay0Z0),
            ("frame-F28", Bay0Z1),
            ("frame-F32", Bay1Z1),
        };

        foreach (var (frameId, z) in stations)
            EmitOpenFrame(entities, joints, loa, frameId, z, web, halfDepth, deckUp);

        EmitBayLongitudinals(entities, joints, loa, Bay0Z0, Bay0Z1, web, deckUp, tPlate, "bay0", "frame-F24", "frame-F28");
        EmitBayLongitudinals(entities, joints, loa, Bay0Z1, Bay1Z1, web, deckUp, tPlate, "bay1", "frame-F28", "frame-F32");

        EmitShellStrips(entities, joints, loa, Bay0Z0, Bay0Z1, web, "bay0", "frame-F24", "frame-F28");
        EmitShellStrips(entities, joints, loa, Bay0Z1, Bay1Z1, web, "bay1", "frame-F28", "frame-F32");

        EmitDeckBay(entities, joints, loa, Bay0Z0, Bay0Z1, web, tPlate, deckUp, "bay0", "frame-F24", "frame-F28");
        EmitDeckBay(entities, joints, loa, Bay0Z1, Bay1Z1, web, tPlate, deckUp, "bay1", "frame-F28", "frame-F32");

        // Shared frame at bay boundary — longitudinal continuity across F28.
        joints.Add(Fillet(
            "j-bay-boundary-F28", "frame-F28", "girder-keel",
            0f, deckUp - tPlate - web * 0.35f, Bay0Z1, "bay0↔bay1 at F28"));

        var stamp = DateTime.UtcNow.ToString("o");
        var cad = new CadDocument
        {
            Name = "Calypso — two-bay structural proof (F24–F32)",
            Generator = new CadGenerator { Name = "CalypsoCad.StructuralBayAssembly", Version = "2026.1.0" },
            CreatedAt = stamp,
            ModifiedAt = stamp,
            LayersDocument = "calypso.cadlayers.json",
            ShapesDocument = "calypso.cadshapejson",
            Layers =
            [
                new() { Id = "S-HULL", Name = "S-HULL", Color = [0.42f, 0.48f, 0.55f] },
                new() { Id = "S-STRUCT", Name = "S-STRUCT", Color = [0.55f, 0.58f, 0.62f] },
            ],
            Entities = entities,
            Camera = new CadCamera
            {
                Distance = 22f,
                Target = [0f, deckUp, loa * 0.5f - 28f],
                Yaw = 0.9f,
                Pitch = 0.42f,
            },
            Properties = new Dictionary<string, JsonElement>
            {
                ["structuralBay"] = JsonSerializer.SerializeToElement(new
                {
                    stations = new[] { Bay0Z0, Bay0Z1, Bay1Z1 },
                    bayPitchM = BayPitchM,
                    deckUp,
                    proof = "two-bay-F24-F32",
                }),
            },
        };
        StructuralAssemblyGraph.WriteJointsToDocument(cad, joints);
        return (cad, joints, entities);
    }

    private static void EmitOpenFrame(
        List<CadEntity> entities,
        List<StructuralJoint> joints,
        float loa,
        string frameId,
        float zStem,
        float web,
        float halfDepth,
        float deckUp)
    {
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset;
        CalypsoShipbuildingFrames.AddPerimeterRingPublic(
            entities, loa, frameId, zStem, web, halfDepth, inset);

        // Deck chord — exact span between port/stbd ring points at deck elevation (no shrink loop).
        var ring = CalypsoShipbuildingFrames.InnerOctagonRing(zStem, inset + web * 1.05f + 0.18f);
        var atDeck = ring
            .Where(p => MathF.Abs(p.Up - deckUp) < 1.8f)
            .OrderBy(p => p.Y)
            .ToList();
        if (atDeck.Count < 2)
            return;

        var yPort = atDeck.First().Y;
        var yStbd = atDeck.Last().Y;
        var cy = (yPort + yStbd) * 0.5f;
        var hy = MathF.Abs(yStbd - yPort) * 0.5f - web * 0.15f;
        if (hy < 0.4f)
            return;

        var chordHalf = MathF.Max(0.08f, web * 0.45f);
        var chordName = $"{frameId}-chord-dk0";
        // Pull center inboard if needed — never shorten the chord span.
        var placed = false;
        for (var pull = 0f; pull <= 0.6f && !placed; pull += 0.08f)
        {
            placed = CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, chordName, cy, deckUp - pull * 0.02f, zStem,
                hy, chordHalf, halfDepth,
                meta: new StructuralSolidMeta(
                    StructuralRoles.TransverseFrame, FrameAsm(frameId),
                    StationZ: zStem, DeckUp: deckUp, Joins: [frameId]));
        }

        if (!placed)
            return;

        joints.Add(Fillet($"j-{chordName}-ring", chordName, frameId, cy, deckUp, zStem, "chord×ring"));
    }

    private static void EmitBayLongitudinals(
        List<CadEntity> entities,
        List<StructuralJoint> joints,
        float loa,
        float z0,
        float z1,
        float web,
        float deckUp,
        float tPlate,
        string bayTag,
        string foreFrame,
        string aftFrame)
    {
        var zc = (z0 + z1) * 0.5f;
        var halfLenZ = (z1 - z0) * 0.5f + web * 0.45f;
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset + web * 1.05f + 0.18f;
        var ring = CalypsoShipbuildingFrames.InnerOctagonRing(zc, inset);
        var (cy, cu) = ring.GetSectionCenter();
        var stiffHy = MathF.Max(0.05f, web * 0.28f);
        var stiffHu = MathF.Max(0.055f, web * 0.3f);

        for (var i = 0; i < ring.Length; i++)
        {
            var placed = false;
            for (var pullStep = 0; pullStep < 10 && !placed; pullStep++)
            {
                var y = ring[i].Y;
                var up = ring[i].Up;
                var inwardY = cy - y;
                var inwardU = cu - up;
                var inwardLen = MathF.Sqrt(inwardY * inwardY + inwardU * inwardU);
                if (inwardLen > 1e-4f)
                {
                    var pull = MathF.Min(stiffHy * (1.2f + pullStep * 0.35f), inwardLen * 0.9f);
                    y += inwardY / inwardLen * pull;
                    up += inwardU / inwardLen * pull;
                }

                var name = $"stringer-v{i}-{bayTag}";
                placed = CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, name, y, up, zc, stiffHy, stiffHu, halfLenZ,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.LongitudinalGirder, "stringer-skin",
                        StationZ: zc, DeckUp: up, Joins: [foreFrame, aftFrame]));
                if (!placed)
                    continue;

                joints.Add(Fillet($"j-{name}-fore", name, foreFrame, y, up, z0, "stringer×frame"));
                joints.Add(Fillet($"j-{name}-aft", name, aftFrame, y, up, z1, "stringer×frame"));
            }
        }

        // Deck girders — exact bay span with web overlap into frames.
        var webH = web * 0.35f;
        var upG = deckUp - tPlate - webH;
        for (var insetExtra = 0.28f; insetExtra <= 1.6f; insetExtra += 0.12f)
        {
            var girderInset = CalypsoShipbuildingFrames.DefaultSkinInset + insetExtra;
            var halfB = MathF.Min(
                CalypsoLockGenerator.HalfBeamInsidePublic(z0, deckUp, girderInset),
                MathF.Min(
                    CalypsoLockGenerator.HalfBeamInsidePublic(zc, deckUp, girderInset),
                    CalypsoLockGenerator.HalfBeamInsidePublic(z1, deckUp, girderInset)));
            var ySide = halfB - web * 0.85f;
            if (ySide < 0.3f)
                continue;

            var joins = new[] { foreFrame, aftFrame };
            var keel = $"girder-keel-{bayTag}";
            var port = $"girder-port-{bayTag}";
            var stbd = $"girder-stbd-{bayTag}";
            var keelOk = CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, keel, 0f, upG, zc, MathF.Min(0.12f, web), webH, halfLenZ,
                meta: new StructuralSolidMeta(StructuralRoles.LongitudinalGirder, "girder-keel", StationZ: zc, DeckUp: deckUp, Joins: joins));
            var portOk = CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, port, -ySide, upG, zc, web * 0.4f, webH, halfLenZ,
                meta: new StructuralSolidMeta(StructuralRoles.LongitudinalGirder, "girder-port", StationZ: zc, DeckUp: deckUp, Joins: joins));
            var stbdOk = CalypsoLockGenerator.TryAddStructureBar(
                entities, loa, stbd, ySide, upG, zc, web * 0.4f, webH, halfLenZ,
                meta: new StructuralSolidMeta(StructuralRoles.LongitudinalGirder, "girder-stbd", StationZ: zc, DeckUp: deckUp, Joins: joins));
            if (!(keelOk && portOk && stbdOk))
                continue;

            joints.Add(Fillet($"j-{keel}-fore", keel, foreFrame, 0f, upG, z0, "girder×frame"));
            joints.Add(Fillet($"j-{keel}-aft", keel, aftFrame, 0f, upG, z1, "girder×frame"));
            joints.Add(Fillet($"j-{port}-fore", port, foreFrame, -ySide, upG, z0, "girder×frame"));
            joints.Add(Fillet($"j-{port}-aft", port, aftFrame, -ySide, upG, z1, "girder×frame"));
            joints.Add(Fillet($"j-{stbd}-fore", stbd, foreFrame, ySide, upG, z0, "girder×frame"));
            joints.Add(Fillet($"j-{stbd}-aft", stbd, aftFrame, ySide, upG, z1, "girder×frame"));
            break;
        }
    }

    private static void EmitShellStrips(
        List<CadEntity> entities,
        List<StructuralJoint> joints,
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
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset + 0.06f;
        var ring = CalypsoShipbuildingFrames.InnerOctagonRing(zc, inset);
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
            var name = $"shell-panel-{bayTag}-e{i}";
            var ok = CalypsoLockGenerator.TryAddOrientedStructureBar(
                entities, loa, name, y, up, zc,
                halfAlongEdge: len * 0.48f,
                halfFlange: t * 0.5f,
                halfDepthZ: halfLenZ,
                orientZ: angle,
                meta: new StructuralSolidMeta(
                    StructuralRoles.ShellPanel, "bay-shell",
                    StationZ: zc, DeckUp: up, Joins: [foreFrame, aftFrame]));
            if (!ok)
                continue;
            joints.Add(Fillet($"j-{name}-fore", name, foreFrame, y, up, z0, "shell×frame"));
            joints.Add(Fillet($"j-{name}-aft", name, aftFrame, y, up, z1, "shell×frame"));
        }
    }

    private static void EmitDeckBay(
        List<CadEntity> entities,
        List<StructuralJoint> joints,
        float loa,
        float z0,
        float z1,
        float web,
        float tPlate,
        float deckUp,
        string bayTag,
        string foreFrame,
        string aftFrame)
    {
        var zc = (z0 + z1) * 0.5f;
        var halfLenZ = (z1 - z0) * 0.5f + web * 0.45f;
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset + 0.12f;
        var halfY = MathF.Min(
            CalypsoLockGenerator.HalfBeamInsidePublic(z0, deckUp, inset),
            MathF.Min(
                CalypsoLockGenerator.HalfBeamInsidePublic(zc, deckUp, inset),
                CalypsoLockGenerator.HalfBeamInsidePublic(z1, deckUp, inset)));
        if (halfY < 0.5f)
            return;

        var baseName = $"deck-sandwich-dk0-{bayTag}";
        CalypsoShipbuildingFrames.EmitSandwichPanelPublic(
            entities, loa, baseName, 0f, deckUp, tPlate, zc, halfY * 0.92f, halfLenZ,
            "deck-dk0", foreFrame, aftFrame);

        joints.Add(Fillet($"j-{baseName}-fore", baseName, foreFrame, 0f, deckUp, z0, "deck×frame"));
        joints.Add(Fillet($"j-{baseName}-aft", baseName, aftFrame, 0f, deckUp, z1, "deck×frame"));
    }

    private static string FrameAsm(string frameId)
    {
        var segs = frameId.Split('-');
        return segs.Length >= 2 ? $"frame-{segs[1]}" : frameId;
    }

    private static StructuralJoint Fillet(
        string id, string a, string b, float y, float up, float z, string notes) =>
        new(
            id, a, b, StructuralJointType.FilletWeld,
            new JointInterface(y, up, z, 0, 0, 1, ToleranceM: 0.12f),
            new JointSpecification("fillet", notes),
            GeometryValid: true);
}
