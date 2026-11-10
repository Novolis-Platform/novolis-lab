using Novolis.Cad.Blueprint;
using Novolis.Cad.Dxf;

// Smoke fixture: Innerscene three-bedroom bungalow (CC0) — closed A-WALL poche + A-DOOR/A-GLAZ.
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
    DocumentName = "Innerscene three-bedroom bungalow (CC0)",
});
foreach (var note in import.Notes)
    Console.WriteLine($"  [import] {note}");

Directory.CreateDirectory(outDir);
var cadJsonPath = Path.Combine(outDir, "innerscene-bungalow.cadjson");
await File.WriteAllTextAsync(
    cadJsonPath,
    System.Text.Json.JsonSerializer.Serialize(import.Document, Novolis.Cad.Primitives.CadJson.Options));
Console.WriteLine($"  wrote {cadJsonPath}");

var (bp, lift) = CadBlueprintProjector.FromCadDocumentWithDiagnostics(
    import.Document, CadBlueprintContexts.House, "./innerscene-bungalow.cadjson");
bp.Name = "Innerscene · three-bedroom bungalow";
foreach (var sheet in bp.Sheets)
{
    sheet.Id = "NBP-DXF-001";
    sheet.Title = "Innerscene bungalow GA (DXF smoke)";
    sheet.Rev = "A";
    sheet.ShowUi = false;
    sheet.Orientation = "plan-north-up";
}

foreach (var d in lift)
    Console.WriteLine($"  [lift {d.Severity}] {d.Code}: {d.Message}");

var result = CadBlueprintExporter.Export(bp, new CadBlueprintExportOptions
{
    OutputDirectory = outDir,
    FileStem = "innerscene-bungalow",
    SmartHtmlSubfolder = "sheets",
    SvgSubfolder = "svg",
    PdfSubfolder = "pdf",
    ContextOverride = CadBlueprintContexts.House,
    CadDocumentHref = "./innerscene-bungalow.cadjson",
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
        var local = Path.Combine(dir.FullName, "fixtures", "innerscene-three-bedroom-bungalow.dxf");
        if (File.Exists(local))
            return local;

        var nested = Path.Combine(
            dir.FullName, "labs", "cad", "BlueprintDxfStress", "fixtures", "innerscene-three-bedroom-bungalow.dxf");
        if (File.Exists(nested))
            return nested;

        dir = dir.Parent;
    }

    throw new FileNotFoundException("Could not locate fixtures/innerscene-three-bedroom-bungalow.dxf");
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
