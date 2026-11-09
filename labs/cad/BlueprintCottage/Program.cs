using Novolis.Cad.Blueprint;

// Golden drafting fixture: complete house CadDocument → CadBlueprint sheet export.
string outDir;
if (args.Length > 0)
{
    outDir = Path.GetFullPath(args[0]);
}
else
{
    outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs"));
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "BlueprintCottage.csproj")))
        {
            outDir = Path.Combine(dir.FullName, "docs");
            break;
        }

        dir = dir.Parent;
    }
}

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
