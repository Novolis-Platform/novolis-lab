using Novolis.Cad.Blueprint;

// Golden drafting fixture: complete house CadDocument → CadBlueprint sheet export.
string outDir;
if (args.Length > 0)
{
    outDir = Path.GetFullPath(args[0]);
}
else
{
    outDir = ResolveDocsDir()
             ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs"));
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

static string? ResolveDocsDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var local = Path.Combine(dir.FullName, "BlueprintCottage.csproj");
        if (File.Exists(local))
            return Path.Combine(dir.FullName, "docs");

        // artifacts/bin/... → walk up to novolis-lab, then labs/cad/BlueprintCottage
        var nested = Path.Combine(dir.FullName, "labs", "cad", "BlueprintCottage", "BlueprintCottage.csproj");
        if (File.Exists(nested))
            return Path.Combine(Path.GetDirectoryName(nested)!, "docs");

        dir = dir.Parent;
    }

    return null;
}
