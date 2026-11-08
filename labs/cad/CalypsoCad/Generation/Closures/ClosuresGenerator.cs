using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Closures;

/// <summary>Phase-3 scaffold — aft coaming/ramp thrust members.</summary>
internal static class ClosuresGenerator
{
    public static void GenerateAftCoaming(
        List<CadEntity> entities,
        float loa,
        float web,
        float tShell,
        float doorW,
        float doorH,
        float sill,
        float apronD)
    {
        var stern = loa - tShell - web * 0.5f - 0.04f;
        var halfW = doorW * 0.5f;
        var coamingMeta = new StructuralSolidMeta(StructuralRoles.ThrustStructure, "coaming-aft", StationZ: stern);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, "coaming-aft-sill", 0f, sill + web * 0.5f, stern, halfW, web * 0.5f, web * 0.5f,
            meta: coamingMeta);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, "coaming-aft-head", 0f, sill + doorH - web * 0.5f, stern, halfW, web * 0.5f, web * 0.5f,
            meta: coamingMeta);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, "coaming-aft-port", -halfW, sill + doorH * 0.5f, stern, web * 0.5f, doorH * 0.5f - web, web * 0.5f,
            meta: coamingMeta);
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, "coaming-aft-stbd", halfW, sill + doorH * 0.5f, stern, web * 0.5f, doorH * 0.5f - web, web * 0.5f,
            meta: coamingMeta);
        var rampHalf = MathF.Max(0.3f, (apronD - 0.2f) * 0.5f);
        var rampZ = stern - web - rampHalf;
        CalypsoLockGenerator.TryAddStructureBar(
            entities, loa, "ramp-aft", 0f, sill + 0.06f, rampZ, halfW, 0.04f, rampHalf,
            meta: new StructuralSolidMeta(StructuralRoles.ThrustStructure, "coaming-aft", StationZ: rampZ));
    }
}
