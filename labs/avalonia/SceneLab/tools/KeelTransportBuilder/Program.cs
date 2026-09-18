using KeelTransportBuilder;

var stageDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "keel-stages"));
var finalPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(stageDir, "..", "keel-transport.nov3djson"));

Console.WriteLine($"Stages → {stageDir}");
KeelStages.BuildAll(stageDir, finalPath);
