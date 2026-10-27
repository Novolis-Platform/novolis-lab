using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation.Fabrication;

internal static class HullFabExporter
{
    public static void Export(
        ManufacturerHullDocument hull,
        string outputRoot,
        List<FabPart> parts,
        List<FabCutProfile> cuts,
        List<FabNestPlacement> nests,
        List<FabWeldSeam> welds,
        List<PlateBomLine> bomLines)
    {
        var cutDir = Path.Combine(outputRoot, "cuts", "hull");
        Directory.CreateDirectory(cutDir);
        var nestItems = new List<NestItem>();
        var edgeFaces = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seamIndex = 0;

        foreach (var face in hull.OmlFaces)
        {
            if (face.VertIds.Length < 4)
                continue;
            if (!TryGetQuad(hull.Points, face.VertIds, out var p0, out var p1, out var p2, out var p3))
                continue;

            var partId = ToPartId(face.Id);
            var profile = HullFaceUnfolder.UnfoldQuad(p0, p1, p2, p3);
            var closure = HullFaceUnfolder.VerifyQuadEdgeClosure(p0, p1, p2, p3, profile);
            var area = HullFaceUnfolder.PolygonAreaM2(profile);
            var bounds = HullFaceUnfolder.Bounds(profile);
            var w = bounds.MaxX - bounds.MinX;
            var h = bounds.MaxY - bounds.MinY;
            var rel = $"cuts/hull/{partId}.dxf";
            var dxfPath = Path.Combine(outputRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            DxfWriter.WritePolyline(dxfPath, profile);
            cuts.Add(new FabCutProfile
            {
                PartId = partId,
                OuterLoop = profile,
                BendAllowed = false,
                AreaM2 = area,
                DevelopabilityPass = closure.Pass,
                EdgeClosureMaxDeltaM = closure.MaxDeltaM,
            });
            parts.Add(new FabPart
            {
                PartId = partId,
                Process = FabPartProcess.Cut,
                MaterialDesignation = hull.MaterialDesignation,
                ThicknessM = hull.ThicknessM,
                SourceDrawing = "CAL-HULL-CAD-001.json",
                LockRef = face.Id,
                CutFileRelative = rel,
                MassKg = area * hull.ThicknessM * hull.DensityKgM3,
            });
            bomLines.Add(new PlateBomLine(partId, face.Group, area, hull.ThicknessM, 1,
                area * hull.ThicknessM * hull.DensityKgM3, hull.MaterialDesignation));
            nestItems.Add(new NestItem(partId, w, h, profile));

            for (var i = 0; i < 4; i++)
            {
                var a = face.VertIds[i];
                var b = face.VertIds[(i + 1) % 4];
                var key = EdgeKey(a, b);
                if (!edgeFaces.TryGetValue(key, out var list))
                {
                    list = [];
                    edgeFaces[key] = list;
                }

                list.Add(face.Id);
            }
        }

        foreach (var kv in edgeFaces)
        {
            if (kv.Value.Count != 2)
                continue;
            welds.Add(new FabWeldSeam
            {
                SeamId = $"SEAM-HULL-{seamIndex:0000}",
                FaceA = kv.Value[0],
                FaceB = kv.Value[1],
                Process = hull.WeldProcess,
                Filler = hull.WeldFiller,
            });
            seamIndex++;
        }

        var packed = SheetNester.Pack(
            nestItems,
            hull.SheetWidthM,
            hull.SheetHeightM,
            margin: 0.05f,
            kerfM: hull.KerfM,
            partGapM: hull.PartGapM,
            grainLocked: hull.GrainLocked);
        var nestDir = Path.Combine(outputRoot, "nests");
        Directory.CreateDirectory(nestDir);
        var sheetIdx = 0;
        foreach (var (sheetId, rows) in packed)
        {
            var sheetPath = Path.Combine(nestDir, $"{sheetId}.dxf");
            var polys = new List<IReadOnlyList<(float X, float Y)>>();
            foreach (var row in rows)
            {
                nests.Add(new FabNestPlacement
                {
                    PartId = row.PartId,
                    SheetId = sheetId,
                    OriginX = row.X,
                    OriginY = row.Y,
                    RotationDeg = row.RotDeg,
                });
                var translated = row.Profile
                    .Select(p => (p.X + row.X, p.Y + row.Y))
                    .ToList();
                polys.Add(translated);
            }

            WriteNestSheet(sheetPath, polys);
            sheetIdx++;
        }

        _ = sheetIdx;

        FabWeldProgramExporter.Write(
            Path.Combine(outputRoot, "welds", "hull-program.json"),
            hull,
            welds);
    }

    private static void WriteNestSheet(string path, List<IReadOnlyList<(float X, float Y)>> polys)
    {
        if (polys.Count == 0)
            return;
        DxfWriter.WritePolylines(path, polys);
    }

    private static bool TryGetQuad(
        Dictionary<string, Vector3> pts,
        string[] ids,
        out Vector3 p0,
        out Vector3 p1,
        out Vector3 p2,
        out Vector3 p3)
    {
        p0 = p1 = p2 = p3 = default;
        if (!pts.TryGetValue(ids[0], out p0) || !pts.TryGetValue(ids[1], out p1)
            || !pts.TryGetValue(ids[2], out p2) || !pts.TryGetValue(ids[3], out p3))
            return false;
        return true;
    }

    private static string ToPartId(string faceId) =>
        "P-HULL-" + faceId.Replace('.', '-').Replace(' ', '-');

    private static string EdgeKey(string a, string b) =>
        string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";

    public static string Sha256File(string path)
    {
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
