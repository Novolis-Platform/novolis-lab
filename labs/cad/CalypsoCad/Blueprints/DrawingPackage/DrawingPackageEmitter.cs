using System.Globalization;
using System.Text;
using System.Text.Json;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>
/// Emits the DNV/ISO-style blueprint package under <c>docs/blueprints/</c> from
/// <c>calypso.cadjson</c> only (one CAD model).
/// </summary>
internal static class DrawingPackageEmitter
{
    private static readonly JsonSerializerOptions JsonWrite = new() { WriteIndented = true };

    public static string Emit(string? outputDirectory = null, string? cadJsonPath = null)
    {
        var outDir = outputDirectory ?? CalypsoProjectPaths.BlueprintsPackageDirectory;

        // Keep legacy detail pack for skills; package sheets are CAD-projected.
        StructureDetailBlueprintEmitter.Emit(cadJsonPath: cadJsonPath);

        if (Directory.Exists(outDir))
        {
            foreach (var stale in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories))
                File.Delete(stale);
            foreach (var dir in Directory.EnumerateDirectories(outDir, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                try { Directory.Delete(dir, recursive: false); } catch { /* non-empty ok */ }
            }
        }

        Directory.CreateDirectory(outDir);

        var cad = CadJsonStructureQuery.Load(cadJsonPath);
        var loa = CadJsonStructureQuery.Loa(cad);
        if (CadJsonStructureQuery.HullLoft(cad).Count == 0)
        {
            throw new InvalidOperationException(
                "calypso.cadjson missing Properties.hullLoft — regenerate CAD before blueprints.");
        }

        var drawings = DrawingPackageCatalog.Build(cad);
        var bom = DrawingPackageBom.Build(cad, drawings);
        var members = DrawingPackageCatalog.StructuralMemberNames(cad);
        var memberChunks = members.Count == 0
            ? new List<string[]> { Array.Empty<string>() }
            : members.Chunk(40).Select(c => c.ToArray()).ToList();

        foreach (var cat in DrawingPackageCategories.All)
            Directory.CreateDirectory(Path.Combine(outDir, cat.FolderName));

        foreach (var cat in DrawingPackageCategories.All)
        {
            var html = DrawingPackageHtml.CategoryIndex(cat, drawings);
            Write(outDir, Path.Combine(cat.FolderName, "index.html"), html);
        }

        var bhSchedule = CalypsoBulkheads.BuildSchedule(loa, CadJsonStructureQuery.StationMap(cad));

        var memberSheetIndex = 0;
        foreach (var entry in drawings)
        {
            if (entry.Category == DrawingCategoryId.Bom)
                continue;
            if (entry.Category == DrawingCategoryId.General && entry.Sequence == 1)
            {
                // Nested cover sheet needs ../ prefixes; package root index does not.
                var nestDepth = entry.RelativePath.Count(c => c is '/' or '\\');
                Write(outDir, entry.RelativePath, DrawingPackageHtml.PackageIndex(drawings, bom, nestDepth));
                Write(outDir, "index.html", DrawingPackageHtml.PackageIndex(drawings, bom, nestDepth: 0));
                continue;
            }

            switch (entry.Category)
            {
                case DrawingCategoryId.Bulkheads when entry.SubjectId is { } bhId:
                {
                    var st = bhSchedule.FirstOrDefault(s =>
                        string.Equals(s.Id, bhId, StringComparison.OrdinalIgnoreCase));
                    var z = string.IsNullOrEmpty(st.Id) ? ParseZFromTags(entry.Tags) : st.ZStem;
                    var note = string.IsNullOrEmpty(st.Id) ? "heavy bulkhead" : st.Note;
                    var cadBh = CadJsonStructureQuery.BulkheadStations(cad, loa)
                        .FirstOrDefault(b => string.Equals(b.Id, bhId, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(cadBh?.Id))
                        z = cadBh!.ZStem;
                    Write(outDir, entry.RelativePath,
                        BulkheadDrawingSheet.Build(entry, cad, bhId, z, note));
                    break;
                }
                case DrawingCategoryId.Frames:
                    Write(outDir, entry.RelativePath,
                        FrameDrawingSheet.Build(entry, cad, ParseZFromTags(entry.Tags)));
                    break;
                case DrawingCategoryId.DeckPlates:
                {
                    ParseDeckSubject(entry.SubjectId, out var deckNum, out var deckLabel);
                    Write(outDir, entry.RelativePath,
                        DeckPlateDrawingSheet.Build(entry, cad, deckNum, deckLabel));
                    break;
                }
                case DrawingCategoryId.StructuralMembers:
                {
                    var chunk = memberSheetIndex < memberChunks.Count
                        ? memberChunks[memberSheetIndex]
                        : Array.Empty<string>();
                    memberSheetIndex++;
                    Write(outDir, entry.RelativePath,
                        StructuralMemberDrawingSheet.Build(entry, cad, chunk));
                    break;
                }
                case DrawingCategoryId.ShirtSleeves:
                case DrawingCategoryId.AirlockAnteroom:
                case DrawingCategoryId.Airlock:
                case DrawingCategoryId.Hybrid:
                {
                    if (entry.SubjectId is not { } space)
                    {
                        Write(outDir, entry.RelativePath,
                            DrawingPackageHtml.DrawingSheet(entry, detailHref: null, memberChunk: null));
                        break;
                    }

                    var kind = entry.VolumeKind != VolumeCirculationKind.None
                        ? entry.VolumeKind
                        : VolumeCirculation.Classify(space);
                    Write(outDir, entry.RelativePath,
                        VolumeDrawingSheet.Build(entry, cad, space, kind));
                    break;
                }
                default:
                    Write(outDir, entry.RelativePath,
                        DrawingPackageHtml.DrawingSheet(entry, detailHref: null, memberChunk: null));
                    break;
            }
        }

        WriteBomArtifacts(outDir, drawings, bom);

        File.WriteAllText(
            Path.Combine(outDir, "drawing-list.json"),
            JsonSerializer.Serialize(
                new
                {
                    package = "CAL-DWG-PACKAGE",
                    rev = "A",
                    generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    standards = new[] { "ISO 5457", "ISO 7200", "ISO 128-15" },
                    model = "calypso.cadjson",
                    note = "Classification-style drawing register from one CAD model — not DNV plan approval.",
                    categories = DrawingPackageCategories.All.Select(c => new
                    {
                        code = c.SystemCode,
                        folder = c.FolderName,
                        title = c.Title,
                        discipline = c.Discipline,
                    }),
                    drawings = drawings.Select(d => new
                    {
                        d.DrawingNumber,
                        d.Label,
                        d.Title,
                        category = d.Category.ToString(),
                        categoryCode = ((int)d.Category).ToString("D3"),
                        d.RelativePath,
                        d.SubjectId,
                        volume = d.VolumeKind.ToString(),
                        d.Tags,
                    }),
                },
                JsonWrite),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(outDir, "drawing-list.csv"),
            BuildCsv(drawings),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(outDir, "bom-master.json"),
            JsonSerializer.Serialize(bom, JsonWrite),
            new UTF8Encoding(false));

        Console.WriteLine(
            $"Wrote drawing package ({drawings.Count} drawings, {bom.Count} BOM lines) → {outDir}");
        return outDir;
    }

    private static void WriteBomArtifacts(
        string outDir,
        IReadOnlyList<DrawingEntry> drawings,
        IReadOnlyList<DrawingPackageBomLine> bom)
    {
        foreach (var entry in drawings.Where(d => d.Category == DrawingCategoryId.Bom))
        {
            var lines = entry.SubjectId switch
            {
                "structure" => DrawingPackageBom.FilterStructure(bom),
                "volumes" => DrawingPackageBom.FilterVolumes(bom),
                _ => bom,
            };
            Write(outDir, entry.RelativePath, DrawingPackageHtml.BomSheet(entry.DrawingNumber, entry.Title, lines));
            var jsonName = Path.ChangeExtension(Path.GetFileName(entry.RelativePath), ".json");
            File.WriteAllText(
                Path.Combine(outDir, DrawingPackageCategories.Get(DrawingCategoryId.Bom).FolderName, jsonName),
                JsonSerializer.Serialize(lines, JsonWrite),
                new UTF8Encoding(false));
        }
    }

    private static string BuildCsv(IReadOnlyList<DrawingEntry> drawings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("drawingNumber,label,title,category,volume,path,subjectId");
        foreach (var d in drawings)
        {
            sb.Append(Csv(d.DrawingNumber)).Append(',')
                .Append(Csv(d.Label)).Append(',')
                .Append(Csv(d.Title)).Append(',')
                .Append(Csv(d.Category.ToString())).Append(',')
                .Append(Csv(d.VolumeKind.ToString())).Append(',')
                .Append(Csv(d.RelativePath)).Append(',')
                .Append(Csv(d.SubjectId ?? "")).AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static float ParseZFromTags(IReadOnlyList<string> tags)
    {
        foreach (var t in tags)
        {
            if (t.StartsWith("z=", StringComparison.OrdinalIgnoreCase)
                && float.TryParse(t[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return z;
        }

        return 0f;
    }

    private static void ParseDeckSubject(string? subjectId, out int deckNum, out string deckLabel)
    {
        if (subjectId is not null
            && subjectId.StartsWith("deck:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = subjectId.Split(':', 3);
            if (parts.Length >= 3
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out deckNum))
            {
                deckLabel = parts[2];
                return;
            }
        }

        deckNum = 0;
        deckLabel = subjectId ?? "DK0";
    }

    private static void Write(string root, string relative, string html)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, html, new UTF8Encoding(false));
    }
}
