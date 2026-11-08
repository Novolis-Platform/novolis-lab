using CalypsoCad.Structure;

namespace CalypsoCad.Model;

/// <summary>
/// Bridges legacy <see cref="StructuralRoles"/> strings onto typed semantic identity.
/// </summary>
internal static class StructuralRoleMap
{
    public static (CadSystem System, CadPartKind Kind, PartFunction Functions) FromLegacyRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return (CadSystem.Transverse, CadPartKind.Unknown, PartFunction.None);

        return role switch
        {
            StructuralRoles.TransverseFrame =>
                (CadSystem.Transverse, CadPartKind.RingFrame, PartFunction.LoadBearing | PartFunction.HullAttached),
            StructuralRoles.PressureBulkhead =>
                (CadSystem.Transverse, CadPartKind.BulkheadPlate,
                    PartFunction.LoadBearing | PartFunction.PressureTight | PartFunction.HullAttached),
            StructuralRoles.LongitudinalGirder =>
                (CadSystem.Longitudinal, CadPartKind.LongitudinalGirder, PartFunction.LoadBearing),
            StructuralRoles.StructuralDeck =>
                (CadSystem.Deck, CadPartKind.DeckPanel, PartFunction.LoadBearing),
            StructuralRoles.ThrustStructure =>
                (CadSystem.Longitudinal, CadPartKind.ThrustMember, PartFunction.LoadBearing),
            StructuralRoles.ShellPanel =>
                (CadSystem.Hull, CadPartKind.ShellPanel,
                    PartFunction.LoadBearing | PartFunction.HullAttached | PartFunction.PressureTight),
            StructuralRoles.ShellStiffener =>
                (CadSystem.Hull, CadPartKind.HullStringer, PartFunction.LoadBearing | PartFunction.HullAttached),
            StructuralRoles.StructuralBracket =>
                (CadSystem.Transverse, CadPartKind.StructuralBracket, PartFunction.LoadBearing),
            _ => (CadSystem.Transverse, CadPartKind.Unknown, PartFunction.None),
        };
    }

    /// <summary>
    /// Classify from entity display name when stamped semantic properties are absent.
    /// Returns null when the name is not recognized as primary structure (caller reports unclassified).
    /// </summary>
    public static (CadSystem System, CadPartKind Kind, PartFunction Functions, string LegacyRole, string Assembly)?
        TryClassifyFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = AssemblyFromBhName(name);
            var plate = name.Contains("-plate", StringComparison.OrdinalIgnoreCase);
            return plate
                ? (CadSystem.Transverse, CadPartKind.BulkheadPlate,
                    PartFunction.LoadBearing | PartFunction.PressureTight | PartFunction.HullAttached,
                    StructuralRoles.PressureBulkhead, asm)
                : (CadSystem.Transverse, CadPartKind.StructuralBracket, PartFunction.LoadBearing,
                    StructuralRoles.StructuralBracket, asm);
        }

        if (name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = AssemblyFromFrameName(name);
            if (name.Contains("-join-", StringComparison.OrdinalIgnoreCase)
                || name.Contains("-deckseat-", StringComparison.OrdinalIgnoreCase))
            {
                return (CadSystem.Transverse, CadPartKind.StructuralBracket, PartFunction.LoadBearing,
                    StructuralRoles.StructuralBracket, asm);
            }

            if (name.Contains("-deckbeam-", StringComparison.OrdinalIgnoreCase))
            {
                return (CadSystem.Transverse, CadPartKind.DeckBeam, PartFunction.LoadBearing,
                    StructuralRoles.TransverseFrame, asm);
            }

            return (CadSystem.Transverse, CadPartKind.RingFrame,
                PartFunction.LoadBearing | PartFunction.HullAttached,
                StructuralRoles.TransverseFrame, asm);
        }

        if (name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = name.StartsWith("girder-keel", StringComparison.OrdinalIgnoreCase) ? "girder-keel"
                : name.StartsWith("girder-port", StringComparison.OrdinalIgnoreCase) ? "girder-port"
                : name.StartsWith("girder-stbd", StringComparison.OrdinalIgnoreCase) ? "girder-stbd"
                : "girder";
            return (CadSystem.Longitudinal, CadPartKind.LongitudinalGirder, PartFunction.LoadBearing,
                StructuralRoles.LongitudinalGirder, asm);
        }

        if (name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = AssemblyFromDeckName(name);
            return (CadSystem.Deck, CadPartKind.DeckPanel, PartFunction.LoadBearing,
                StructuralRoles.StructuralDeck, asm);
        }

        if (name.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("shell-stiffener-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Hull, CadPartKind.HullStringer,
                PartFunction.LoadBearing | PartFunction.HullAttached,
                StructuralRoles.ShellStiffener, "plating-join");
        }

        if (name.StartsWith("shell-panel-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Hull, CadPartKind.ShellPanel,
                PartFunction.LoadBearing | PartFunction.HullAttached | PartFunction.PressureTight,
                StructuralRoles.ShellPanel, "bay-shell");
        }

        if (name.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ramp-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Longitudinal, CadPartKind.ThrustMember, PartFunction.LoadBearing,
                StructuralRoles.ThrustStructure, "coaming-aft");
        }

        return null;
    }

    public static string LegacyRoleOf(CadPartKind kind, PartFunction functions) =>
        kind switch
        {
            CadPartKind.BulkheadPlate => StructuralRoles.PressureBulkhead,
            CadPartKind.RingFrame or CadPartKind.WebFrame or CadPartKind.DeckBeam =>
                StructuralRoles.TransverseFrame,
            CadPartKind.LongitudinalGirder => StructuralRoles.LongitudinalGirder,
            CadPartKind.DeckPanel => StructuralRoles.StructuralDeck,
            CadPartKind.ThrustMember => StructuralRoles.ThrustStructure,
            CadPartKind.ShellPanel => StructuralRoles.ShellPanel,
            CadPartKind.HullStringer => StructuralRoles.ShellStiffener,
            CadPartKind.StructuralBracket => StructuralRoles.StructuralBracket,
            _ when functions.HasFlag(PartFunction.PressureTight) => StructuralRoles.PressureBulkhead,
            _ => StructuralRoles.TransverseFrame,
        };

    public static string AssemblyFromBhName(string name)
    {
        // bh-ENG-AFT-plate → bh-ENG-AFT
        if (name.EndsWith("-plate", StringComparison.OrdinalIgnoreCase))
            return name[..^"-plate".Length];
        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
            return string.Join('-', parts.Take(3));
        return name;
    }

    public static string AssemblyFromFrameName(string name)
    {
        // frame-F32-ring-0 → frame-F32
        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0]}-{parts[1]}";
        return name;
    }

    public static string AssemblyFromDeckName(string name)
    {
        // deck-sandwich-dk0-… → deck-dk0
        if (name.Contains("-dkm1-", StringComparison.OrdinalIgnoreCase)
            || name.Contains("-dk-1-", StringComparison.OrdinalIgnoreCase))
            return "deck-dkm1";
        if (name.Contains("-dk0-", StringComparison.OrdinalIgnoreCase))
            return "deck-dk0";
        if (name.Contains("-dk1-", StringComparison.OrdinalIgnoreCase))
            return "deck-dk1";
        return "deck";
    }

    public static PartId PartIdFromEntity(string displayName, CadPartKind kind, string assembly)
    {
        if (kind == CadPartKind.BulkheadPlate)
            return PartId.Of($"{assembly.ToUpperInvariant()}-PLATE");
        return PartId.Of(displayName);
    }

    public static AssemblyId NormalizeAssemblyId(string assembly, CadSystem system)
    {
        _ = system;
        if (assembly.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
            return AssemblyId.Of("BH-" + assembly[3..].ToUpperInvariant());

        if (assembly.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
            return AssemblyId.Of("FRAME-" + assembly["frame-".Length..].ToUpperInvariant());

        if (assembly.StartsWith("BH-", StringComparison.OrdinalIgnoreCase)
            || assembly.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase))
            return AssemblyId.Of(assembly.ToUpperInvariant());

        // Legacy joint keys (girder-keel, deck-dk0, …) stay as emitted for graph compat.
        return AssemblyId.Of(assembly);
    }
}

