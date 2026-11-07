using CalypsoCad.Viewport;
using CalypsoCad.Session;
namespace CalypsoCad.Export;

/// <summary>Path helpers for Calypso PNG tours (Silk headless / agent dumps).</summary>
internal static class ViewportPngExporter
{
    public static string ExportsDirectory(string generatedRoot) =>
        Path.Combine(generatedRoot, "exports");

    public static string AllocateTourPath(string generatedRoot, string kind) =>
        Path.Combine(ExportsDirectory(generatedRoot), $"{kind}.png");

    public static string AllocatePath(string generatedRoot, string kind) =>
        Path.Combine(ExportsDirectory(generatedRoot), $"{kind}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");

    public static void PurgeLegacyHeadlessExports(string generatedRoot)
    {
        var dir = ExportsDirectory(generatedRoot);
        if (!Directory.Exists(dir))
            return;
        foreach (var file in Directory.EnumerateFiles(dir, "*-headless-*.png"))
        {
            try { File.Delete(file); }
            catch { /* best-effort cleanup */ }
        }
    }
}
