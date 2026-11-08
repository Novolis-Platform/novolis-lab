namespace CalypsoCad.Constraints;

/// <summary>Authoritative design constraints for one generate pass.</summary>
internal sealed class DesignConstraints
{
    public IReadOnlyList<ClearanceVolume> Clearances { get; init; } = [];
    public IReadOnlyList<PressureCompartment> PressureCompartments { get; init; } = [];
    public IReadOnlyList<StructuralStation> Stations { get; init; } = [];
    public IReadOnlyList<OpeningDefinition> Openings { get; init; } = [];

    /// <summary>Aft door band excluded from open HOLD cell (matches <c>StructuralTopology.HoldDoorBand</c>).</summary>
    public float HoldDoorBand { get; init; } = 1.5f;

    public ClearanceVolume? CargoHold =>
        Clearances.FirstOrDefault(c => c.Kind == ClearanceKind.CargoClearance);

    /// <summary>Open HOLD cargo void — same bounds as <c>StructuralTopology.InOpenHoldCell</c>.</summary>
    public bool InCargoClearance(float zStem)
    {
        var hold = CargoHold;
        if (hold is null)
            return false;
        return zStem > hold.Z0Stem + 0.05f && zStem < hold.Z1Stem - HoldDoorBand;
    }

    public bool InMachineryClearance(float zStem) =>
        Clearances.Any(c => c.Kind == ClearanceKind.MachineryClearance && InteriorContains(c, zStem));

    /// <summary>
    /// Intermediate deck through an open atrium/cargo cell (above cell floor). Floor plates stay.
    /// </summary>
    public bool IsOpenAtriumDeck(float zStem, float plateTop)
    {
        foreach (var c in Clearances)
        {
            if (c.Kind is not (ClearanceKind.MachineryClearance or ClearanceKind.CargoClearance))
                continue;
            if (!InteriorContains(c, zStem))
                continue;
            if (c.Up0 is float floor && plateTop > floor + 0.15f)
                return true;
        }

        return false;
    }

    public bool IsHoldFloor(float deckUp, IReadOnlyList<float> deckUps) =>
        deckUps.Count > 0 && MathF.Abs(deckUp - deckUps[0]) < 0.08f;

    public IEnumerable<StructuralStation> BulkheadStations =>
        Stations.Where(s => s.Kind == StructuralStationKind.HeavyBulkhead);

    public IEnumerable<StructuralStation> FrameStations =>
        Stations.Where(s => s.Kind == StructuralStationKind.RingFrame);

    public IEnumerable<OpeningDefinition> OpeningsForAssembly(string assemblyId) =>
        Openings.Where(o => string.Equals(o.OwnerAssemblyId, assemblyId, StringComparison.OrdinalIgnoreCase));

    private static bool InteriorContains(ClearanceVolume c, float zStem) =>
        zStem > c.Z0Stem + 0.05f && zStem < c.Z1Stem - 0.05f;
}
