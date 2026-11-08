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
            StructuralRoles.PartitionAttachment =>
                (CadSystem.Transverse, CadPartKind.PartitionAttachmentPost, PartFunction.LoadBearing),
            StructuralRoles.PortalFrame =>
                (CadSystem.Transverse, CadPartKind.PortalJamb, PartFunction.LoadBearing),
            _ => (CadSystem.Transverse, CadPartKind.Unknown, PartFunction.None),
        };
    }

    // Interior / closures / cargo classification lives in TryClassifyFromName below.

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

        if (name.Contains("-int-post-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = AssemblyFromFrameName(name);
            return (CadSystem.Transverse, CadPartKind.PartitionAttachmentPost, PartFunction.LoadBearing,
                StructuralRoles.PartitionAttachment, asm);
        }

        if (name.Contains("-int-jamb-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = AssemblyFromFrameName(name);
            return (CadSystem.Transverse, CadPartKind.PortalJamb, PartFunction.LoadBearing,
                StructuralRoles.PortalFrame, asm);
        }

        if (name.Contains("-int-hdr-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = AssemblyFromFrameName(name);
            return (CadSystem.Transverse, CadPartKind.PortalHeader, PartFunction.LoadBearing,
                StructuralRoles.PortalFrame, asm);
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

            if (name.Contains("-chord-", StringComparison.OrdinalIgnoreCase))
            {
                return (CadSystem.Transverse, CadPartKind.DeckChord, PartFunction.LoadBearing,
                    StructuralRoles.TransverseFrame, asm);
            }

            if (name.Contains("-annulus", StringComparison.OrdinalIgnoreCase)
                || name.Contains("-ring-", StringComparison.OrdinalIgnoreCase))
            {
                return (CadSystem.Transverse, CadPartKind.AnnularWebPlate,
                    PartFunction.LoadBearing | PartFunction.HullAttached,
                    StructuralRoles.TransverseFrame, asm);
            }

            // Flange / unspecified frame steel — hull-attached transverse member.
            return (CadSystem.Transverse, CadPartKind.AnnularWebPlate,
                PartFunction.LoadBearing | PartFunction.HullAttached,
                StructuralRoles.TransverseFrame, asm);
        }

        if (name.StartsWith("primary-keel", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Longitudinal, CadPartKind.LongitudinalGirder, PartFunction.LoadBearing,
                StructuralRoles.LongitudinalGirder, "primary-keel");
        }

        if (name.StartsWith("thrust-spine", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Longitudinal, CadPartKind.ThrustMember, PartFunction.LoadBearing,
                StructuralRoles.ThrustStructure, "thrust-spine");
        }

        if (name.StartsWith("cargo-floor-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Deck, CadPartKind.LongitudinalGirder, PartFunction.LoadBearing,
                StructuralRoles.LongitudinalGirder, "deck-dkm1");
        }

        if (name.StartsWith("deck-edge-", StringComparison.OrdinalIgnoreCase))
        {
            var asm = name.Contains("-dk1", StringComparison.OrdinalIgnoreCase) ? "deck-dk1"
                : name.Contains("-dk0", StringComparison.OrdinalIgnoreCase) ? "deck-dk0"
                : name.Contains("-dkm1", StringComparison.OrdinalIgnoreCase) ? "deck-dkm1"
                : AssemblyFromDeckName(name);
            return (CadSystem.Deck, CadPartKind.DeckEdgeGirder, PartFunction.LoadBearing,
                StructuralRoles.StructuralDeck, asm);
        }

        if (name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy StructuralBayAssembly proof names only.
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
            if (name.Contains("-face-", StringComparison.OrdinalIgnoreCase))
                return (CadSystem.Deck, CadPartKind.DeckFaceSheet, PartFunction.LoadBearing,
                    StructuralRoles.StructuralDeck, asm);
            if (name.Contains("-rib-", StringComparison.OrdinalIgnoreCase))
                return (CadSystem.Deck, CadPartKind.DeckCoreWeb, PartFunction.LoadBearing,
                    StructuralRoles.StructuralDeck, asm);
            if (name.Contains("-peri-", StringComparison.OrdinalIgnoreCase))
                return (CadSystem.Deck, CadPartKind.DeckPerimeterMember, PartFunction.LoadBearing,
                    StructuralRoles.StructuralDeck, asm);
            return (CadSystem.Deck, CadPartKind.DeckPanel, PartFunction.LoadBearing,
                StructuralRoles.StructuralDeck, asm);
        }

        if (name.StartsWith("stringer-knuckle-v", StringComparison.OrdinalIgnoreCase))
        {
            var asm = KnuckleAssemblyFromName(name);
            return (CadSystem.Hull, CadPartKind.KnuckleStringer,
                PartFunction.LoadBearing | PartFunction.HullAttached,
                StructuralRoles.ShellStiffener, asm);
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

        if (name.StartsWith("outfit-AIRLOCK", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("outfit-hatch-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Closures, CadPartKind.AirlockStructure, PartFunction.Replaceable,
                "Closure", "closures-airlock");
        }

        if (name.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Interior, CadPartKind.Furniture, PartFunction.Replaceable,
                "Outfit", "interior-furniture");
        }

        if (name.StartsWith("C40", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Cargo, CadPartKind.CargoContainer, PartFunction.Replaceable,
                "Cargo", "loaded-cargo-c40");
        }

        if (name.StartsWith("w-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Interior, CadPartKind.Partition, PartFunction.None,
                "Partition", "interior-partitions");
        }

        if (name.StartsWith("hatch-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("door-", StringComparison.OrdinalIgnoreCase))
        {
            return (CadSystem.Closures, CadPartKind.Hatch, PartFunction.Replaceable,
                "Closure", "closures-hatches");
        }

        return null;
    }

    public static string LegacyRoleOf(CadPartKind kind, PartFunction functions) =>
        kind switch
        {
            CadPartKind.BulkheadPlate => StructuralRoles.PressureBulkhead,
            CadPartKind.AnnularWebPlate or CadPartKind.RingFlange or CadPartKind.WebStiffener
                or CadPartKind.DeckChord or CadPartKind.RingFrame or CadPartKind.WebFrame
                or CadPartKind.DeckBeam =>
                StructuralRoles.TransverseFrame,
            CadPartKind.LongitudinalGirder => StructuralRoles.LongitudinalGirder,
            CadPartKind.DeckPanel => StructuralRoles.StructuralDeck,
            CadPartKind.ThrustMember => StructuralRoles.ThrustStructure,
            CadPartKind.ShellPanel => StructuralRoles.ShellPanel,
            CadPartKind.HullStringer or CadPartKind.KnuckleStringer or CadPartKind.HullStiffener =>
                StructuralRoles.ShellStiffener,
            CadPartKind.DeckFaceSheet or CadPartKind.DeckCoreWeb or CadPartKind.DeckPerimeterMember =>
                StructuralRoles.StructuralDeck,
            CadPartKind.StructuralBracket or CadPartKind.ConnectionBracket => StructuralRoles.StructuralBracket,
            _ when functions.HasFlag(PartFunction.PressureTight) => StructuralRoles.PressureBulkhead,
            _ => StructuralRoles.TransverseFrame,
        };

    /// <summary><c>stringer-knuckle-v3-s12</c> → <c>knuckle-v3</c>.</summary>
    public static string KnuckleAssemblyFromName(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            name, @"stringer-knuckle-v(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? $"knuckle-v{m.Groups[1].Value}" : "knuckle";
    }

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

