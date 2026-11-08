using CalypsoCad.Constraints;
using CalypsoCad.Generation.Closures;
using CalypsoCad.Generation.Decks;
using CalypsoCad.Generation.Longitudinal;
using CalypsoCad.Generation.Transverse;
using CalypsoCad.Geometry;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>
/// Explicit staged structure generation. Assemblies are defined before geometry emit.
/// </summary>
internal static class ShipGenerationPipeline
{
    public static List<StructuralJoint> RunStructure(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyDictionary<string, float> bhStationMap,
        Func<float, float, List<(float Y0, float Y1)>> passageGapsAt,
        Func<float, float, List<FrameWebPlateBuilder.WebPlatePortal>> hatchPortalsAt,
        float doorW,
        float doorH,
        float sill,
        float apronD,
        float tShell)
    {
        // Stage 1 — design model; station-driven schedule (BH anchors → fill ring frames).
        var bhSchedule = CalypsoBulkheads.BuildSchedule(loa, bhStationMap);
        var framing = FrameSchedule.Create(
            loa,
            topo.FrameBay,
            bhSchedule.Select(b => (b.Id, b.ZStem)).ToList());
        // Topology.FrameStations is already ordinary-only from BuildStructuralTopology;
        // re-derive here so pipeline stays coherent if topo was built another way.
        var ordinaryFrames = FrameSchedule.OrdinaryRingFrames(framing)
            .Select(s => (s.Id, s.ZStem, BowAligned: s.ZStem < 18f))
            .ToList();

        var constraints = DesignConstraintsBuilder.Build(
            loa, topo, bhSchedule, ordinaryFrames);

        var ship = CadDocumentAdapter.Active.Ship
                   ?? new ShipDefinition("CALYPSO", loa, 20.8f, 12.4f, topo.DeckUps, bhStationMap);
        var context = new ShipGeometryContext
        {
            Ship = ship,
            Hull = HullGeometry.RequireActive(),
            Registry = CadDocumentAdapter.Active,
            Constraints = constraints,
            Loa = loa,
            Web = topo.Web,
            TBh = topo.TBh,
            TPlate = topo.TPlate,
            DeckUps = topo.DeckUps,
        };

        // Stage 3 — transverse: define BH assemblies, then emit.
        foreach (var face in bhSchedule)
        {
            var thickness = Math.Clamp(topo.TBh * face.ThicknessScale, 0.18f, 0.65f);
            var halfDepthZ = MathF.Max(0.09f, thickness * 0.5f);
            var z = Math.Clamp(face.ZStem, thickness, loa - thickness);
            if (!context.Hull.HasStructuralSection(z))
            {
                StructurePlacementLog.Record(
                    $"bh-{face.Id}", 0f, 0f, z, 0f, 0f, halfDepthZ,
                    "no structural hull section at BH station");
                continue;
            }

            var spec = new BulkheadSpec(
                AssemblyId.Of($"BH-{face.Id}"),
                z,
                thickness,
                halfDepthZ,
                face.PressureTight,
                BowAligned: z < 18f,
                AthwartGaps: passageGapsAt(z, 0.28f),
                DeckUps: topo.DeckUps,
                HatchPortals: hatchPortalsAt(z, 0.28f));

            var result = TransverseStructureGenerator.GenerateBulkhead(spec, context, entities);
            foreach (var d in result.Diagnostics)
                Console.Error.WriteLine($"  {d}");
        }

        // Stage 3b — ring frames (schedule already excludes BH stations; hold → perimeter-only).
        void PlaceFrame(string id, float zStem, bool bowAligned = false)
        {
            var skin = tShell + 0.05f;
            var halfDepthZ = topo.Web * 0.5f;
            var zMax = context.Hull.Stations[^1].Z;
            var z = Math.Clamp(zStem, topo.Web * 0.5f + skin, zMax - topo.Web * 0.5f - skin);
            if (!context.Hull.HasStructuralSection(z, minHalf: 0.5f))
            {
                StructurePlacementLog.Record(id, 0f, 0f, zStem, 0f, 0f, halfDepthZ, "no structural section");
                return;
            }

            var frameName = id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id : $"frame-{id}";
            var gaps = passageGapsAt(z, 0.28f);

            if (context.Constraints.InCargoClearance(zStem))
            {
                CalypsoShipbuildingFrames.AddHoldPerimeterFrame(
                    entities, loa, frameName, z, topo.Web, halfDepthZ, topo.DeckUps, topo.TPlate);
                return;
            }

            CalypsoShipbuildingFrames.AddTransverseFrameWeb(
                entities, loa, frameName, z, topo.Web, halfDepthZ, gaps, bowAligned, topo.DeckUps,
                hatchPortals: null);
            if (!bowAligned)
            {
                CalypsoShipbuildingFrames.AddTransverseDeckBeams(
                    entities, loa, z, frameName, topo.Web, halfDepthZ, topo.DeckUps, gaps, topo.TPlate,
                    topo.OpenAtriumSpans);
            }
        }

        foreach (var fr in ordinaryFrames)
        {
            var asmId = fr.Id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                ? AssemblyId.Of("FRAME-" + fr.Id["frame-".Length..].ToUpperInvariant())
                : AssemblyId.Of("FRAME-" + fr.Id.ToUpperInvariant());
            var spec = new RingFrameSpec(
                asmId,
                fr.Id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? fr.Id : $"frame-{fr.Id}",
                fr.ZStem,
                topo.Web,
                topo.Web * 0.5f,
                fr.BowAligned,
                HoldPerimeterOnly: context.Constraints.InCargoClearance(fr.ZStem),
                topo.DeckUps);
            _ = TransverseStructureGenerator.GenerateRingFrame(spec, context, entities, PlaceFrame);
        }

        // Stage 4 — longitudinal (path-swept knuckle stringers + deck girders)
        LongitudinalStructureGenerator.Generate(
            entities, loa, topo, ordinaryFrames, context,
            bhSchedule.Select(b => (b.Id, b.ZStem)).ToList());

        // Stage 5 — decks
        DeckStructureGenerator.Generate(entities, loa, topo, ordinaryFrames, bhSchedule, context);

        // Stage 6 — critical joints + closures
        var joints = CriticalStructureConnectivity.Connect(
            entities, loa, topo,
            bhSchedule.Select(b => (b.Id, b.ZStem, b.ThicknessScale)).ToList(),
            constraints);

        ClosuresGenerator.GenerateAftCoaming(
            entities, loa, topo.Web, tShell, doorW, doorH, sill, apronD);

        // Stage 7 — assembly validation (mesh-aware graph)
        _ = StructuralAssemblyGraph.Validate(new CadDocument { Entities = entities }, joints);

        return joints;
    }
}
