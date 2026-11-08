using CalypsoCad.Structure;

namespace CalypsoCad.Constraints;

/// <summary>Builds <see cref="DesignConstraints"/> from lock-derived topology and BH schedule.</summary>
internal static class DesignConstraintsBuilder
{
    public static DesignConstraints Build(
        float loa,
        StructuralTopology topo,
        IReadOnlyList<CalypsoBulkheads.Station> bulkheads,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> frames,
        IReadOnlyList<string>? pressureSpaceKeys = null)
    {
        var holdFloor = topo.OpenAtriumSpans
            .Where(s => s.Z1 - s.Z0 >= 12f)
            .Select(s => (float?)s.FloorTop)
            .FirstOrDefault()
            ?? (topo.DeckUps.Count > 0 ? topo.DeckUps[0] : 0.5f);

        var clearances = new List<ClearanceVolume>
        {
            new(
                "HOLD-CARGO",
                ClearanceKind.CargoClearance,
                topo.HoldFore,
                topo.HoldAft,
                Up0: holdFloor,
                Note: "Cargo hold loading envelope"),
        };

        foreach (var (z0, z1, floorTop) in topo.OpenAtriumSpans)
        {
            // Long aft span is cargo (already modeled as HOLD-CARGO); short midbody = machinery atrium.
            if (z1 - z0 >= 12f)
                continue;
            clearances.Add(new ClearanceVolume(
                $"ATRIUM-{z0:0.##}-{z1:0.##}",
                ClearanceKind.MachineryClearance,
                z0,
                z1,
                Up0: floorTop,
                Note: "Open atrium / machinery access"));
        }

        clearances.Add(new ClearanceVolume(
            "CORR-PASSAGE",
            ClearanceKind.PassageClearance,
            0f,
            loa,
            Note: "Ship-wide corridor/passage clearance (queried per station by generators)"));

        var stations = new List<StructuralStation>();
        foreach (var bh in bulkheads)
        {
            stations.Add(new StructuralStation(
                $"BH-{bh.Id}",
                bh.ZStem,
                StructuralStationKind.HeavyBulkhead,
                bh.PressureTight,
                bh.Note));
        }

        foreach (var fr in frames)
        {
            if (CalypsoBulkheads.OwnsFrameStation(fr.ZStem, bulkheads))
                continue;
            var id = fr.Id;
            if (id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
                id = "FRAME-" + id["frame-".Length..];
            else if (!id.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase))
                id = "FRAME-" + id;
            stations.Add(new StructuralStation(
                id.ToUpperInvariant(),
                fr.ZStem,
                StructuralStationKind.RingFrame));
        }

        var pressure = (pressureSpaceKeys ?? ["BRIDGE", "CREW_1", "ENG", "HOLD"])
            .Select(k => new PressureCompartment(k, k, Habitable: !string.Equals(k, "HOLD", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new DesignConstraints
        {
            Clearances = clearances,
            PressureCompartments = pressure,
            Stations = stations,
            Openings = [],
            HoldDoorBand = topo.HoldDoorBand,
        };
    }
}
