using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Model;

/// <summary>
/// Builds the canonical ownership tree and attaches leaf assemblies under system groups.
/// Does not invent geometry — only semantic ownership links.
/// </summary>
internal static class ShipHierarchyBuilder
{
    public static void EnsureCanonicalHierarchy(ShipPartRegistry registry, CadDocument? doc = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        EnsureOrg(registry, ShipHierarchyIds.ShipRoot, CadSystem.Organization, CadAssemblyKind.ShipRoot,
            "Calypso", parent: null);

        EnsureOrg(registry, ShipHierarchyIds.ReferenceGeometry, CadSystem.Reference, CadAssemblyKind.ReferenceGeometry,
            "Reference geometry", ShipHierarchyIds.ShipRoot);
        EnsureOrg(registry, ShipHierarchyIds.HullLoft, CadSystem.Reference, CadAssemblyKind.SystemGroup,
            "Hull loft", ShipHierarchyIds.ReferenceGeometry);
        EnsureOrg(registry, ShipHierarchyIds.StructuralStations, CadSystem.Reference, CadAssemblyKind.SystemGroup,
            "Structural stations", ShipHierarchyIds.ReferenceGeometry);
        EnsureOrg(registry, ShipHierarchyIds.DeckElevations, CadSystem.Reference, CadAssemblyKind.SystemGroup,
            "Deck elevations", ShipHierarchyIds.ReferenceGeometry);
        EnsureOrg(registry, ShipHierarchyIds.CompartmentEnvelopes, CadSystem.Reference, CadAssemblyKind.SystemGroup,
            "Compartment envelopes", ShipHierarchyIds.ReferenceGeometry);
        EnsureOrg(registry, ShipHierarchyIds.ClearanceVolumes, CadSystem.Reference, CadAssemblyKind.SystemGroup,
            "Clearance volumes", ShipHierarchyIds.ReferenceGeometry);

        EnsureOrg(registry, ShipHierarchyIds.PhysicalStructure, CadSystem.Organization, CadAssemblyKind.PhysicalStructure,
            "Physical structure", ShipHierarchyIds.ShipRoot);

        EnsureOrg(registry, ShipHierarchyIds.Hull, CadSystem.Hull, CadAssemblyKind.SystemGroup,
            "Hull", ShipHierarchyIds.PhysicalStructure);
        EnsureOrg(registry, ShipHierarchyIds.PressureHullShell, CadSystem.Hull, CadAssemblyKind.SystemGroup,
            "Pressure hull shell", ShipHierarchyIds.Hull);
        EnsureOrg(registry, ShipHierarchyIds.KnuckleStringers, CadSystem.Hull, CadAssemblyKind.SystemGroup,
            "Knuckle stringers", ShipHierarchyIds.Hull);
        EnsureOrg(registry, ShipHierarchyIds.ShellStiffeners, CadSystem.Hull, CadAssemblyKind.SystemGroup,
            "Additional shell stiffeners", ShipHierarchyIds.Hull);

        EnsureOrg(registry, ShipHierarchyIds.Transverse, CadSystem.Transverse, CadAssemblyKind.SystemGroup,
            "Transverse structure", ShipHierarchyIds.PhysicalStructure);
        EnsureOrg(registry, ShipHierarchyIds.RingFrames, CadSystem.Transverse, CadAssemblyKind.SystemGroup,
            "Ring frames", ShipHierarchyIds.Transverse);
        EnsureOrg(registry, ShipHierarchyIds.HeavyBulkheads, CadSystem.Transverse, CadAssemblyKind.SystemGroup,
            "Heavy bulkheads", ShipHierarchyIds.Transverse);

        EnsureOrg(registry, ShipHierarchyIds.Longitudinal, CadSystem.Longitudinal, CadAssemblyKind.SystemGroup,
            "Longitudinal structure", ShipHierarchyIds.PhysicalStructure);
        EnsureOrg(registry, ShipHierarchyIds.PrimaryHullGirders, CadSystem.Longitudinal, CadAssemblyKind.SystemGroup,
            "Primary hull girders", ShipHierarchyIds.Longitudinal);
        EnsureOrg(registry, ShipHierarchyIds.ThrustStructure, CadSystem.Longitudinal, CadAssemblyKind.SystemGroup,
            "Propulsion thrust structure", ShipHierarchyIds.Longitudinal);

        EnsureOrg(registry, ShipHierarchyIds.DeckStructure, CadSystem.Deck, CadAssemblyKind.SystemGroup,
            "Deck structure", ShipHierarchyIds.PhysicalStructure);
        EnsureOrg(registry, ShipHierarchyIds.DeckM1, CadSystem.Deck, CadAssemblyKind.SystemGroup,
            "Deck -1", ShipHierarchyIds.DeckStructure);
        EnsureOrg(registry, ShipHierarchyIds.Deck0, CadSystem.Deck, CadAssemblyKind.SystemGroup,
            "Deck 0", ShipHierarchyIds.DeckStructure);
        EnsureOrg(registry, ShipHierarchyIds.DeckP1, CadSystem.Deck, CadAssemblyKind.SystemGroup,
            "Deck +1", ShipHierarchyIds.DeckStructure);

        EnsureOrg(registry, ShipHierarchyIds.Closures, CadSystem.Closures, CadAssemblyKind.SystemGroup,
            "Closures and access", ShipHierarchyIds.ShipRoot);
        EnsureOrg(registry, ShipHierarchyIds.Machinery, CadSystem.Machinery, CadAssemblyKind.SystemGroup,
            "Machinery and equipment", ShipHierarchyIds.ShipRoot);
        EnsureOrg(registry, ShipHierarchyIds.Interior, CadSystem.Interior, CadAssemblyKind.SystemGroup,
            "Interior outfitting", ShipHierarchyIds.ShipRoot);
        EnsureOrg(registry, ShipHierarchyIds.Partitions, CadSystem.Interior, CadAssemblyKind.PartitionGroup,
            "Partitions", ShipHierarchyIds.Interior);
        EnsureOrg(registry, ShipHierarchyIds.Furniture, CadSystem.Interior, CadAssemblyKind.OutfitGroup,
            "Furniture and fixtures", ShipHierarchyIds.Interior);

        EnsureOrg(registry, ShipHierarchyIds.CargoSystems, CadSystem.Cargo, CadAssemblyKind.SystemGroup,
            "Cargo systems", ShipHierarchyIds.ShipRoot);
        EnsureOrg(registry, ShipHierarchyIds.LoadedCargo, CadSystem.Cargo, CadAssemblyKind.CargoStack,
            "Loaded cargo", ShipHierarchyIds.ShipRoot);

        EnsureOrg(registry, ShipHierarchyIds.Diagnostics, CadSystem.Diagnostics, CadAssemblyKind.DiagnosticsGroup,
            "Diagnostics", ShipHierarchyIds.ShipRoot);

        AttachLeafAssemblies(registry);
        RegisterDocumentReferences(registry, doc);
        RegisterUnclassifiedBucket(registry);
    }

    public static ShipExplorerNode BuildExplorerTree(ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        EnsureCanonicalHierarchy(registry);

        var rootId = AssemblyId.Of(ShipHierarchyIds.ShipRoot);
        return BuildAssemblyNode(registry, rootId);
    }

    private static ShipExplorerNode BuildAssemblyNode(ShipPartRegistry registry, AssemblyId id)
    {
        registry.TryGetAssembly(id, out var asm);
        var node = new ShipExplorerNode
        {
            Id = id.Value,
            Title = asm?.DisplayName ?? id.Value,
            Kind = string.Equals(id.Value, ShipHierarchyIds.ShipRoot, StringComparison.OrdinalIgnoreCase)
                ? ShipExplorerNodeKind.Root
                : string.Equals(id.Value, ShipHierarchyIds.Diagnostics, StringComparison.OrdinalIgnoreCase)
                    ? ShipExplorerNodeKind.Diagnostics
                    : ShipExplorerNodeKind.Assembly,
            AssemblyId = id,
        };

        foreach (var child in registry.ChildAssemblies(id).OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase))
            node.Children.Add(BuildAssemblyNode(registry, child.Id));

        foreach (var part in registry.PartsOf(id).OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            node.Children.Add(new ShipExplorerNode
            {
                Id = part.Id.Value,
                Title = part.DisplayName,
                Kind = ShipExplorerNodeKind.Part,
                PartId = part.Id,
                AssemblyId = part.Assembly,
            });
        }

        if (string.Equals(id.Value, ShipHierarchyIds.HullLoft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id.Value, ShipHierarchyIds.CompartmentEnvelopes, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id.Value, ShipHierarchyIds.StructuralStations, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id.Value, ShipHierarchyIds.DeckElevations, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id.Value, ShipHierarchyIds.ClearanceVolumes, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id.Value, ShipHierarchyIds.Diagnostics, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var r in registry.References
                         .Where(r => r.ParentAssemblyId is { } p
                                     && string.Equals(p.Value, id.Value, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                node.Children.Add(new ShipExplorerNode
                {
                    Id = r.Id,
                    Title = r.DisplayName,
                    Kind = ShipExplorerNodeKind.Reference,
                    ReferenceId = r.Id,
                    AssemblyId = r.ParentAssemblyId,
                });
            }
        }

        return node;
    }

    private static void AttachLeafAssemblies(ShipPartRegistry registry)
    {
        foreach (var asm in registry.Assemblies.ToList())
        {
            if (IsOrganizational(asm.Kind))
                continue;
            if (asm.ParentAssemblyId is not null)
                continue;

            var parent = ResolveParent(asm);
            registry.RegisterAssembly(asm with { ParentAssemblyId = AssemblyId.Of(parent) });
        }
    }

    private static string ResolveParent(CadAssembly asm)
    {
        if (asm.Kind == CadAssemblyKind.HullKnuckleStringer
            || asm.Id.Value.StartsWith("knuckle-v", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.KnuckleStringers;

        if (asm.Kind is CadAssemblyKind.SolidBulkhead or CadAssemblyKind.PressureBulkhead or CadAssemblyKind.RingBulkhead
            || asm.Id.Value.StartsWith("BH-", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.HeavyBulkheads;

        if (asm.Kind is CadAssemblyKind.OrdinaryRingFrame or CadAssemblyKind.DeepWebRingFrame
                or CadAssemblyKind.ReinforcedRingFrame
            || asm.Id.Value.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.RingFrames;

        if (asm.Id.Value.StartsWith("deck-dkm1", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.StartsWith("cargo-floor-", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.DeckM1;
        if (asm.Id.Value.StartsWith("deck-dk0", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.Contains("-dk0", StringComparison.OrdinalIgnoreCase)
               && asm.System == CadSystem.Deck)
            return ShipHierarchyIds.Deck0;
        if (asm.Id.Value.StartsWith("deck-dk1", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.Contains("-dk1", StringComparison.OrdinalIgnoreCase)
               && asm.System == CadSystem.Deck)
            return ShipHierarchyIds.DeckP1;

        if (asm.Kind == CadAssemblyKind.DeckBay
            || asm.Id.Value.StartsWith("deck-", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.DeckStructure;

        if (asm.Id.Value.StartsWith("primary-keel", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.StartsWith("girder-keel", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.StartsWith("girder-port", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.StartsWith("girder-stbd", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.PrimaryHullGirders;
        if (asm.Id.Value.StartsWith("thrust-spine", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.StartsWith("coaming", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.StartsWith("ramp", StringComparison.OrdinalIgnoreCase)
            || (asm.Kind == CadAssemblyKind.LongitudinalRun && asm.System == CadSystem.Longitudinal))
            return ShipHierarchyIds.ThrustStructure;

        if (asm.System == CadSystem.Hull
            || asm.Id.Value.Contains("shell", StringComparison.OrdinalIgnoreCase)
            || asm.Id.Value.Contains("plating", StringComparison.OrdinalIgnoreCase))
            return ShipHierarchyIds.PressureHullShell;

        if (asm.System == CadSystem.Closures)
            return ShipHierarchyIds.Closures;
        if (asm.System == CadSystem.Interior)
            return ShipHierarchyIds.Interior;
        if (asm.System == CadSystem.Cargo)
            return ShipHierarchyIds.LoadedCargo;
        if (asm.System == CadSystem.Machinery)
            return ShipHierarchyIds.Machinery;
        if (asm.System == CadSystem.Longitudinal)
            return ShipHierarchyIds.Longitudinal;
        if (asm.System == CadSystem.Transverse)
            return ShipHierarchyIds.Transverse;
        if (asm.System == CadSystem.Deck)
            return ShipHierarchyIds.DeckStructure;

        return ShipHierarchyIds.Diagnostics;
    }

    private static bool IsOrganizational(CadAssemblyKind kind) =>
        kind is CadAssemblyKind.ShipRoot or CadAssemblyKind.ReferenceGeometry
            or CadAssemblyKind.PhysicalStructure or CadAssemblyKind.SystemGroup
            or CadAssemblyKind.DiagnosticsGroup or CadAssemblyKind.PartitionGroup
            or CadAssemblyKind.OutfitGroup or CadAssemblyKind.CargoStack;

    private static void EnsureOrg(
        ShipPartRegistry registry,
        string id,
        CadSystem system,
        CadAssemblyKind kind,
        string displayName,
        string? parent)
    {
        registry.RegisterAssembly(new CadAssembly(
            AssemblyId.Of(id),
            system,
            displayName,
            Kind: kind,
            ParentAssemblyId: parent is null ? null : AssemblyId.Of(parent)));
    }

    private static void RegisterDocumentReferences(ShipPartRegistry registry, CadDocument? doc)
    {
        if (doc?.Properties is null)
            return;

        if (doc.Properties.ContainsKey("hullLoft"))
        {
            registry.RegisterReference(new CadReferenceObject(
                "ref-hull-loft",
                CadReferenceKind.HullLoft,
                "Hull loft (lock authority)",
                AssemblyId.Of(ShipHierarchyIds.HullLoft)));
            registry.RegisterReference(new CadReferenceObject(
                "ref-oml",
                CadReferenceKind.OuterMoldedLine,
                "Outer molded line (OML)",
                AssemblyId.Of(ShipHierarchyIds.HullLoft)));
            registry.RegisterReference(new CadReferenceObject(
                "ref-iml",
                CadReferenceKind.InnerMoldedLine,
                "Inner molded line (IML)",
                AssemblyId.Of(ShipHierarchyIds.HullLoft)));
            registry.RegisterReference(new CadReferenceObject(
                "ref-oml-edge-guide",
                CadReferenceKind.RenderingGuide,
                "OML edge guide",
                AssemblyId.Of(ShipHierarchyIds.HullLoft)));
        }

        if (doc.Properties.TryGetValue("decks", out var decksEl)
            && decksEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in decksEl.EnumerateObject())
            {
                registry.RegisterReference(new CadReferenceObject(
                    $"ref-deck-elev-{prop.Name}",
                    CadReferenceKind.DeckElevation,
                    $"Deck elevation · {prop.Name}",
                    AssemblyId.Of(ShipHierarchyIds.DeckElevations),
                    DeckUp: prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetSingle() : null));
            }
        }

        foreach (var space in doc.Entities ?? [])
        {
            if (!string.Equals(space.Kind, "space", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(space.Name))
                continue;
            registry.RegisterReference(new CadReferenceObject(
                $"ref-space-{space.Name}",
                CadReferenceKind.CompartmentEnvelope,
                $"Compartment · {space.Name}",
                AssemblyId.Of(ShipHierarchyIds.CompartmentEnvelopes),
                EntityId: space.Id,
                DeckUp: space.Deck,
                CompartmentId: space.Name));
        }
    }

    private static void RegisterUnclassifiedBucket(ShipPartRegistry registry)
    {
        foreach (var name in registry.Unclassified)
        {
            registry.RegisterReference(new CadReferenceObject(
                $"diag-unclassified-{name}",
                CadReferenceKind.Other,
                $"Unclassified · {name}",
                AssemblyId.Of(ShipHierarchyIds.Diagnostics)));
        }
    }
}
