using CalypsoCad.Hierarchy;
using CalypsoCad.Model;
using CalypsoCad.Visibility;

namespace CalypsoCad.Tests;

/// <summary>Lightweight adapter checks (no full ship generate).</summary>
internal static class CalypsoHierarchyAdapterTests
{
    public static bool Run(out string detail)
    {
        var registry = new ShipPartRegistry();
        registry.RegisterAssembly(new CadAssembly(
            AssemblyId.Of(ShipHierarchyIds.ShipRoot),
            CadSystem.Organization,
            "Ship",
            Kind: CadAssemblyKind.ShipRoot));
        registry.RegisterAssembly(new CadAssembly(
            AssemblyId.Of(ShipHierarchyIds.Hull),
            CadSystem.Hull,
            "Hull",
            ParentAssemblyId: AssemblyId.Of(ShipHierarchyIds.ShipRoot),
            Kind: CadAssemblyKind.SystemGroup));
        registry.RegisterPart(new CadPart(
            PartId.Of("PART-DEMO"),
            AssemblyId.Of(ShipHierarchyIds.Hull),
            CadSystem.Hull,
            CadPartKind.ShellPanel,
            PartFunction.None,
            "Demo plate",
            EntityId: Guid.NewGuid()));

        var hierarchy = ShipHierarchyMapper.ToHierarchy(registry);
        if (!hierarchy.TryGetPart(Novolis.Cad.Hierarchy.PartId.Of("PART-DEMO"), out var part))
        {
            detail = "mapped part missing";
            return false;
        }

        if (part.Metadata is null
            || !part.Metadata.TryGetValue(CalypsoHierarchyMetadata.CadPartKind, out var kind)
            || kind != nameof(CadPartKind.ShellPanel))
        {
            detail = "cadPartKind metadata missing";
            return false;
        }

        var vis = new HierarchyVisibilityService();
        vis.BindRegistry(registry);
        vis.SetLocal(ShipHierarchyIds.Hull, LocalVisibility.Hidden, recordUndo: false);
        if (vis.IsEffectivelyVisible("PART-DEMO"))
        {
            detail = "part should be hidden under hull";
            return false;
        }

        detail = "ok";
        return true;
    }
}
