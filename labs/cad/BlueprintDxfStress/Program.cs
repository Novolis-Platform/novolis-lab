using Novolis.Cad.Blueprint;
using Novolis.Cad.Dxf;

// Stress fixture: real AIA-layer residential DXF from jscad/sample-files (Bishop-Overland).
var fixture = args.Length > 0 && File.Exists(args[0])
    ? Path.GetFullPath(args[0])
    : ResolveFixture();

var outDir = args.Length > 1
    ? Path.GetFullPath(args[1])
    : ResolveDocsDir();

Console.WriteLine($"DXF fixture: {fixture}");
Console.WriteLine($"Output:      {outDir}");

var import = DxfCadImporter.ImportFile(fixture, new DxfImportOptions
{
    DocumentName = "Bishop-Overland floor plan (jscad sample)",
});
foreach (var note in import.Notes)
    Console.WriteLine($"  [import] {note}");

Directory.CreateDirectory(outDir);
var cadJsonPath = Path.Combine(outDir, "bishop-overland.cadjson");
await File.WriteAllTextAsync(
    cadJsonPath,
    System.Text.Json.JsonSerializer.Serialize(import.Document, Novolis.Cad.Primitives.CadJson.Options));
Console.WriteLine($"  wrote {cadJsonPath}");

var (bp, lift) = CadBlueprintProjector.FromCadDocumentWithDiagnostics(
    import.Document, CadBlueprintContexts.House, "./bishop-overland.cadjson");
bp.Name = "Bishop-Overland · ground floor plan";
foreach (var sheet in bp.Sheets)
{
    sheet.Id = "NBP-DXF-001";
    sheet.Title = "Bishop-Overland residential GA (DXF stress)";
    sheet.Rev = "A";
    sheet.ShowUi = false;
    sheet.Orientation = "plan-north-up";
}

foreach (var d in lift)
    Console.WriteLine($"  [lift {d.Severity}] {d.Code}: {d.Message}");

var result = CadBlueprintExporter.Export(bp, new CadBlueprintExportOptions
{
    OutputDirectory = outDir,
    FileStem = "bishop-overland",
    SmartHtmlSubfolder = "sheets",
    SvgSubfolder = "svg",
    PdfSubfolder = "pdf",
    ContextOverride = CadBlueprintContexts.House,
    CadDocumentHref = "./bishop-overland.cadjson",
    FailOnError = true,
    FailOnWarning = false,
});

foreach (var d in result.Diagnostics)
    Console.WriteLine($"  [{d.Severity}] {d.Code}: {d.Message}");

if (!result.Ok)
{
    Console.Error.WriteLine("Export failed.");
    return 1;
}

foreach (var p in result.WrittenPaths)
    Console.WriteLine($"  wrote {p}");
Console.WriteLine(
    $"OK — walls={import.WallCount} spaces={import.SpaceCount} openings={import.OpeningCount}");
return 0;

static string ResolveFixture()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var local = Path.Combine(dir.FullName, "fixtures", "bishop-overland-floorplan.dxf");
        if (File.Exists(local))
            return local;

        var nested = Path.Combine(
            dir.FullName, "labs", "cad", "BlueprintDxfStress", "fixtures", "bishop-overland-floorplan.dxf");
        if (File.Exists(nested))
            return nested;

        dir = dir.Parent;
    }

    throw new FileNotFoundException("Could not locate fixtures/bishop-overland-floorplan.dxf");
}

static string ResolveDocsDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "BlueprintDxfStress.csproj")))
            return Path.Combine(dir.FullName, "docs");

        var nested = Path.Combine(dir.FullName, "labs", "cad", "BlueprintDxfStress", "BlueprintDxfStress.csproj");
        if (File.Exists(nested))
            return Path.Combine(Path.GetDirectoryName(nested)!, "docs");

        dir = dir.Parent;
    }

    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs"));
}
