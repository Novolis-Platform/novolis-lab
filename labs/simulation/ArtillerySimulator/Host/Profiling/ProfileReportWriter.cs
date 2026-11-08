using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

internal static class ProfileReportWriter
{
    public static void Write(FrameProfiler profiler, string? jsonPath, ILogger log)
    {
        var text = profiler.ToReport();
        Console.WriteLine();
        Console.WriteLine("=== ArtillerySimulator profile ===");
        Console.WriteLine(text);
        if (string.IsNullOrWhiteSpace(jsonPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
        File.WriteAllText(jsonPath, profiler.ToJson());
        log.LogInformation("Wrote profile JSON to {Path}", jsonPath);
        Console.WriteLine($"Wrote {jsonPath}");
    }
}
