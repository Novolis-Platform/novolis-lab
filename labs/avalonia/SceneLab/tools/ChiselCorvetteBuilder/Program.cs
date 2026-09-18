using ChiselCorvetteBuilder;

var stageDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "chisel-stages"));
var finalPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(stageDir, "..", "chisel-corvette.nov3djson"));

Console.WriteLine($"Stages → {stageDir}");
CorvetteStages.BuildAll(stageDir, finalPath);
