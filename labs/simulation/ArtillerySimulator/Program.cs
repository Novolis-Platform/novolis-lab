using Novolis.Silk;

namespace ArtillerySimulator;

internal static class Program
{
    // Headless profile: --profile [frames=180] [--json path]
    private static void Main(string[] args)
    {
        var profileFrames = ParseProfileFrames(args);
        var jsonPath = ParseJsonPath(args);
        using var game = new ArtilleryApp();
        if (profileFrames is int frames)
        {
            game.EnableProfile(frames, jsonPath);
            SilkGame.Run(
                "ArtillerySimulator profile",
                1280,
                720,
                game.Initialize,
                game.Update,
                renderOverlay: null,
                captureSession: null,
                visible: false);
            return;
        }

        SilkGame.Run("Artillery Simulator — Kristiansand", 1920, 1080, game.Initialize, game.Update);
    }

    private static int? ParseProfileFrames(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var n) && n > 0)
                return n;
            return 180;
        }

        return null;
    }

    private static string? ParseJsonPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--json", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }
}
