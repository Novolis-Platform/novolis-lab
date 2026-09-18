using CorellianFreighterBuilder;

var samples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples"));
var stageDir = Path.Combine(samples, "freighter-stages");
var finalPath = Path.Combine(samples, "corellian-freighter.nov3djson");
var dropDir = Path.Combine(samples, "external", "cgtrader-falcon");
var interiorOnlyPath = Path.Combine(samples, "falcon-interior.nov3djson");

if (args.Length > 0 && args[0] is "--import" or "import" or "--import-interior" or "import-interior")
{
    // Usage:
    //   --import <fbx|obj|…> [out.nov3djson]              exterior only
    //   --import-interior <fbx|obj|…> [out.nov3djson]     exterior + procedural interior
    var withInterior = args[0] is "--import-interior" or "import-interior";
    var meshPath = args.Length > 1
        ? args[1]
        : FindDropMesh(dropDir)
          ?? throw new InvalidOperationException(
              $"No mesh path given and nothing in drop folder:\n  {dropDir}\n" +
              "Download FBX from CGTrader (login required), unzip into that folder, re-run.");

    var outPath = args.Length > 2 ? args[2] : finalPath;
    Console.WriteLine($"Import → {meshPath}");
    Console.WriteLine("Source: CGTrader evercity #4201368 — local dogfood; respect listing RF license.");
    var exterior = ExternalMeshImport.Load(meshPath, targetLengthMeters: 34.37f);

    if (withInterior)
    {
        Console.WriteLine("Building procedural interior (ShipYard shells — not UI edit)…");
        var interior = FalconInterior.Build();
        ExternalMeshImport.WriteExteriorWithInterior(exterior, interior, outPath);
    }
    else
    {
        ExternalMeshImport.WriteScene(exterior, outPath, "YT-1300 (CGTrader evercity import)");
    }

    return;
}

if (args.Length > 0 && args[0] is "--interior" or "interior")
{
    // Usage: --interior [out.nov3djson]
    var outPath = args.Length > 1 ? args[1] : interiorOnlyPath;
    Console.WriteLine("Building procedural Falcon interior only…");
    var interior = FalconInterior.Build();
    ExternalMeshImport.WriteScene(interior, outPath, "YT-1300 Interior (procedural)");
    return;
}

if (args.Length > 0)
    stageDir = args[0];
if (args.Length > 1)
    finalPath = args[1];

Console.WriteLine($"Stages → {stageDir}");
Console.WriteLine("Homage: procedural CEC YT-1300 landmarks (Haynes) — original mesh, not a licensed asset.");
Console.WriteLine("Tip: --import-interior <path-to.fbx> to bake CGTrader exterior + procedural interior.");
FreighterStages.BuildAll(stageDir, finalPath);

static string? FindDropMesh(string dir)
{
    if (!Directory.Exists(dir))
        return null;
    string[] exts = [".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds"];
    foreach (var ext in exts)
    {
        var hit = Directory.EnumerateFiles(dir, "*" + ext, SearchOption.AllDirectories)
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
        if (hit is not null)
            return hit;
    }

    return null;
}
