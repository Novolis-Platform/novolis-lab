using System.Drawing;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Viewport;

/// <summary>
/// Semantic inspection colors from registry classification — not mesh/box heuristics.
/// </summary>
internal static class StructureInspectionPalette
{
    public static readonly Color AnnularWeb = Color.FromArgb(255, 118, 126, 136);      // neutral steel
    public static readonly Color HeavyBulkhead = Color.FromArgb(255, 196, 108, 58);    // orange
    public static readonly Color KnuckleStringer = Color.FromArgb(255, 56, 196, 220);  // cyan
    public static readonly Color LongitudinalGirder = Color.FromArgb(255, 92, 118, 148); // blue-gray
    public static readonly Color DeckChordBeam = Color.FromArgb(255, 178, 182, 186);   // light gray
    public static readonly Color StructuralBracket = Color.FromArgb(255, 188, 168, 72); // muted yellow
    public static readonly Color ShellPanel = Color.FromArgb(255, 88, 96, 104);
    public static readonly Color DeckPanel = Color.FromArgb(255, 72, 78, 86);
    public static readonly Color ReferenceHull = Color.FromArgb(90, 140, 148, 156);    // transparent gray
    public static readonly Color FallbackSteel = Color.FromArgb(255, 78, 86, 96);

    public static Color Resolve(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (CadDocumentAdapter.TryReadStamped(entity, out var part))
            return ForPart(part.Kind, part.System);

        if (CadDocumentAdapter.Active.TryGetPartByDisplayName(entity.Name ?? "", out var registered))
            return ForPart(registered.Kind, registered.System);

        // Migration only — stamped/registry path is authoritative once present.
        var named = StructuralRoleMap.TryClassifyFromName(entity.Name);
        if (named is { } n)
            return ForPart(n.Kind, n.System);

        return FallbackSteel;
    }

    public static Color ForPart(CadPartKind kind, CadSystem system) =>
        kind switch
        {
            CadPartKind.BulkheadPlate => HeavyBulkhead,
            CadPartKind.AnnularWebPlate or CadPartKind.RingFrame or CadPartKind.WebFrame
                or CadPartKind.RingFlange or CadPartKind.WebStiffener => AnnularWeb,
            CadPartKind.KnuckleStringer or CadPartKind.HullStringer or CadPartKind.HullStiffener =>
                KnuckleStringer,
            CadPartKind.LongitudinalGirder or CadPartKind.ThrustMember or CadPartKind.DeckEdgeGirder =>
                LongitudinalGirder,
            CadPartKind.DeckChord or CadPartKind.DeckBeam => DeckChordBeam,
            CadPartKind.StructuralBracket or CadPartKind.ConnectionBracket or CadPartKind.GirderStiffener =>
                StructuralBracket,
            CadPartKind.ShellPanel or CadPartKind.ShellReinforcement => ShellPanel,
            CadPartKind.DeckPanel or CadPartKind.DeckFaceSheet or CadPartKind.DeckCoreWeb
                or CadPartKind.DeckPerimeterMember => DeckPanel,
            _ when system == CadSystem.Transverse => AnnularWeb,
            _ when system == CadSystem.Hull => KnuckleStringer,
            _ when system == CadSystem.Longitudinal => LongitudinalGirder,
            _ => FallbackSteel,
        };

    public static bool IsHeavyBulkhead(CadEntity entity)
    {
        if (CadDocumentAdapter.TryReadStamped(entity, out var part))
            return part.Kind == CadPartKind.BulkheadPlate;
        return CadDocumentAdapter.Active.TryGetPartByDisplayName(entity.Name ?? "", out var p)
               && p.Kind == CadPartKind.BulkheadPlate;
    }
}
