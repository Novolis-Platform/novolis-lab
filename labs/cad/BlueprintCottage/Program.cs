using Novolis.Cad.Blueprint;

// Golden drafting fixture: complete house CadDocument → CadBlueprint sheet export.
var outDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs"));

Console.WriteLine($"Exporting demo cottage blueprints → {outDir}");
var result = CadBlueprintDemoCottage.Export(outDir);
foreach (var d in result.Diagnostics)
    Console.WriteLine($"  [{d.Severity}] {d.Code}: {d.Message}");

if (!result.Ok)
{
    Console.Error.WriteLine("Export failed.");
    return 1;
}

foreach (var p in result.WrittenPaths)
    Console.WriteLine($"  wrote {p}");
Console.WriteLine("OK");
return 0;
