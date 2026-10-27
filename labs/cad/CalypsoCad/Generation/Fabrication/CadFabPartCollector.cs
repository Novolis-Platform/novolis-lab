using System.Numerics;
using System.Text.Json;
using CalypsoCad.Generation;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation.Fabrication;

internal static class CadFabPartCollector
{
    private const float WallThickness = 0.08f;

    public static void CollectFromDocument(
        CadDocument doc,
        float loa,
        string outputRoot,
        List<FabPart> parts,
        List<FabCutProfile> cuts,
        List<FabAssemblyInstance> sequence,
        List<PlateBomLine> bomLines,
        ref int sequenceIndex)
    {
        var cutStructDir = Path.Combine(outputRoot, "cuts", "structure");
        var cutWallDir = Path.Combine(outputRoot, "cuts", "partition");
        Directory.CreateDirectory(cutStructDir);
        Directory.CreateDirectory(cutWallDir);

        var seenParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fabId, entry) in FrameWebPlateRegistry.All)
        {
            if (!seenParts.Add(fabId))
                continue;
            ExportFrameWebProfile(entry, fabId, loa, cutStructDir, parts, cuts, bomLines, sequence, ref sequenceIndex);
        }

        foreach (var e in doc.Entities)
        {
            if (e.Name is null)
                continue;

            if (e.Name.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase)
                || e.Name.EndsWith("-web-profile", StringComparison.OrdinalIgnoreCase))
                continue;

            if (e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                && e.Name.Contains("-webplate-", StringComparison.OrdinalIgnoreCase))
                continue;

            if (e.Name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
                || e.Name.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase)
                || e.Name.StartsWith("ramp-", StringComparison.OrdinalIgnoreCase))
            {
                var fabId = GetFabPartId(e) ?? $"P-STRUCT-{Sanitize(e.Name)}";
                if (!seenParts.Add(fabId))
                    continue;
                ExportStructureBar(doc, e, loa, fabId, cutStructDir, parts, cuts, bomLines, sequence, ref sequenceIndex,
                    0.15f, "CAL-INT-GA-001.json");
            }
        }

        foreach (var e in doc.Entities.Where(e => string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)))
        {
            var fabId = GetFabPartId(e) ?? $"P-WALL-{Sanitize(e.Name ?? "wall")}";
            if (!seenParts.Add(fabId))
                continue;
            ExportWallPanel(e, loa, fabId, cutWallDir, parts, cuts, bomLines, sequence, ref sequenceIndex);
        }

        foreach (var e in doc.Entities.Where(e =>
                     e.Name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) == true))
        {
            var fabId = GetFabPartId(e) ?? $"P-OUTFIT-{Sanitize(e.Name!)}";
            if (!seenParts.Add(fabId))
                continue;
            ExportInstallPart(e, loa, fabId, parts, sequence, ref sequenceIndex);
        }
    }

    private static void ExportStructureBar(
        CadDocument doc,
        CadEntity e,
        float loa,
        string fabId,
        string cutDir,
        List<FabPart> parts,
        List<FabCutProfile> cuts,
        List<PlateBomLine> bomLines,
        List<FabAssemblyInstance> sequence,
        ref int seq,
        float thickness,
        string drawing)
    {
        if (!TryBoxLockFrame(e, loa, out var y, out var up, out var zStem, out var hy, out var hu, out var hz))
            return;
        var profile = new List<(float X, float Y)>
        {
            (-hy, -hu),
            (hy, -hu),
            (hy, hu),
            (-hy, hu),
        };
        var area = 4f * hy * hu;
        var rel = $"cuts/structure/{fabId}.dxf";
        DxfWriter.WritePolyline(Path.Combine(cutDir, $"{fabId}.dxf"), profile);
        cuts.Add(new FabCutProfile { PartId = fabId, OuterLoop = profile, AreaM2 = area, BendAllowed = false });
        var density = CalypsoMaterialMap.DensityKgM3("structure");
        var mass = area * thickness * density;
        parts.Add(new FabPart
        {
            PartId = fabId,
            Process = FabPartProcess.Cut,
            ThicknessM = thickness,
            MaterialDesignation = CalypsoMaterialMap.Designation("structure"),
            SourceDrawing = drawing,
            CutFileRelative = rel,
            MassKg = mass,
            CadEntityName = e.Name,
        });
        bomLines.Add(new PlateBomLine(
            fabId, "structure", area, thickness, 1, mass, CalypsoMaterialMap.Designation("structure")));
        sequence.Add(MakeInstance(fabId, seq++, y, up, zStem, loa));
    }

    private static void ExportWallPanel(
        CadEntity e,
        float loa,
        string fabId,
        string cutDir,
        List<FabPart> parts,
        List<FabCutProfile> cuts,
        List<PlateBomLine> bomLines,
        List<FabAssemblyInstance> sequence,
        ref int seq)
    {
        if (!TryWallLockFrame(e, loa, out var y, out var up, out var zStem, out var hy, out var hu)
            && !TryBoxLockFrame(e, loa, out y, out up, out zStem, out hy, out hu, out _))
            return;
        var profile = new List<(float X, float Y)> { (-hy, -hu), (hy, -hu), (hy, hu), (-hy, hu) };
        var area = 4f * hy * hu;
        var rel = $"cuts/partition/{fabId}.dxf";
        DxfWriter.WritePolyline(Path.Combine(cutDir, $"{fabId}.dxf"), profile);
        cuts.Add(new FabCutProfile { PartId = fabId, OuterLoop = profile, AreaM2 = area, BendAllowed = false });
        var density = CalypsoMaterialMap.DensityKgM3("partition");
        var mass = area * WallThickness * density;
        parts.Add(new FabPart
        {
            PartId = fabId,
            Process = FabPartProcess.Cut,
            ThicknessM = WallThickness,
            SourceDrawing = "CAL-INT-GA-001.json",
            LockRef = e.Name,
            MaterialDesignation = CalypsoMaterialMap.Designation("partition"),
            CutFileRelative = rel,
            MassKg = mass,
            CadEntityName = e.Name,
        });
        bomLines.Add(new PlateBomLine(
            fabId, "partition", area, WallThickness, 1, mass, CalypsoMaterialMap.Designation("partition")));
        sequence.Add(MakeInstance(fabId, seq++, y, up, zStem, loa));
    }

    private static void ExportInstallPart(
        CadEntity e,
        float loa,
        string fabId,
        List<FabPart> parts,
        List<FabAssemblyInstance> sequence,
        ref int seq)
    {
        if (!TryBoxLockFrame(e, loa, out var y, out var up, out var zStem, out var hy, out var hu, out var hz))
            return;
        var vol = 8f * hy * hu * hz;
        var density = CalypsoMaterialMap.DensityKgM3("outfit");
        var mass = vol * density;
        parts.Add(new FabPart
        {
            PartId = fabId,
            Process = FabPartProcess.Install,
            ThicknessM = 0f,
            MaterialDesignation = CalypsoMaterialMap.Designation("outfit"),
            SourceDrawing = "CAL-INT-GA-001.json",
            LockRef = e.Name,
            MassKg = mass,
            CadEntityName = e.Name,
        });
        sequence.Add(MakeInstance(fabId, seq++, y, up, zStem, loa));
    }

    private static void ExportFrameWebProfile(
        FrameWebPlateRegistry.Entry entry,
        string fabId,
        float loa,
        string cutDir,
        List<FabPart> parts,
        List<FabCutProfile> cuts,
        List<PlateBomLine> bomLines,
        List<FabAssemblyInstance> sequence,
        ref int seq)
    {
        var profile = entry.Profile;
        var holes = profile.Holes.Select(h => (IReadOnlyList<(float X, float Y)>)h).ToList();
        var rel = $"cuts/structure/{fabId}.dxf";
        DxfWriter.WritePolyline(Path.Combine(cutDir, $"{fabId}.dxf"), profile.OuterLoop, holes);
        cuts.Add(profile);
        var density = CalypsoMaterialMap.DensityKgM3("structure");
        var mass = profile.AreaM2 * entry.ThicknessM * density;
        parts.Add(new FabPart
        {
            PartId = fabId,
            Process = FabPartProcess.Cut,
            ThicknessM = entry.ThicknessM,
            MaterialDesignation = CalypsoMaterialMap.Designation("structure"),
            SourceDrawing = entry.DrawingRef,
            CutFileRelative = rel,
            MassKg = mass,
            CadEntityName = fabId,
        });
        bomLines.Add(new PlateBomLine(
            fabId, "structure", profile.AreaM2, entry.ThicknessM, 1, mass, CalypsoMaterialMap.Designation("structure")));
        sequence.Add(MakeInstance(fabId, seq++, 0f, 0f, entry.ZStem, loa));
    }

    private static FabAssemblyInstance MakeInstance(string partId, int seq, float y, float up, float zStem, float loa) =>
        new()
        {
            PartId = partId,
            SequenceIndex = seq,
            PosX = 0.5f + (loa - zStem),
            PosY = y,
            PosZ = up,
        };

    private static bool TryWallLockFrame(
        CadEntity e,
        float loa,
        out float y,
        out float up,
        out float zStem,
        out float hy,
        out float hu)
    {
        y = up = zStem = hy = hu = 0f;
        if (e.A is not { Length: >= 3 } || e.B is not { Length: >= 3 })
            return false;
        var ax = e.A[0];
        var az = e.A[2];
        var bx = e.B[0];
        var bz = e.B[2];
        var cx = (ax + bx) * 0.5f;
        var cz = (az + bz) * 0.5f;
        var len = MathF.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        if (len < 0.05f || e.Height < 0.05f)
            return false;
        zStem = loa * 0.5f - cz;
        y = cx;
        up = e.A[1] + e.Height * 0.5f;
        hy = len * 0.5f;
        hu = e.Height * 0.5f;
        return true;
    }

    private static bool TryBoxLockFrame(
        CadEntity e,
        float loa,
        out float y,
        out float up,
        out float zStem,
        out float hy,
        out float hu,
        out float hz)
    {
        y = up = zStem = hy = hu = hz = 0f;
        if (e.Points is not { Count: >= 2 })
            return false;
        var cx = e.Points[0][0];
        var cy = e.Points[0].Length > 1 ? e.Points[0][1] : 0f;
        var cz = e.Points[0].Length > 2 ? e.Points[0][2] : 0f;
        hy = e.Points[1][0];
        hu = e.Points[1].Length > 1 ? e.Points[1][1] : 0f;
        hz = e.Points[1].Length > 2 ? e.Points[1][2] : 0f;
        zStem = loa * 0.5f - cz;
        y = cx;
        up = cy;
        return hy > 0 && hu > 0;
    }

    private static string? GetFabPartId(CadEntity e)
    {
        if (e.Properties is null)
            return null;
        if (e.Properties.TryGetValue(ShipPropertyKeys.FabPartId, out var el))
            return el.GetString();
        return null;
    }

    private static string Sanitize(string s) =>
        s.Replace('/', '-').Replace(' ', '-').Replace('.', '-');
}
