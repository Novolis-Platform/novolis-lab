using TroopCorvetteBuilder;

var stageDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "corvette-stages"));
var finalPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(stageDir, "..", "troop-corvette.nov3djson"));

Console.WriteLine($"Stages → {stageDir}");
CorvetteStages.BuildAll(stageDir, finalPath);
