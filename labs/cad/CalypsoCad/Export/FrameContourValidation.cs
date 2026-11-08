using System.Text.Json;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Export;

/// <summary>
/// Quantitative check: ordinary frame annulus outer rings vs authoritative hull section.
/// </summary>
internal static class FrameContourValidation
{
    public static string WriteReport(CadDocument document, float maxDeviationMm = 25f)
    {
        _ = document;
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "frame-contour");
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "frame-contour-validation.json");
        if (FrameContourEmitLog.Snapshot().Count > 0)
            return FrameContourEmitLog.WriteJson(path, maxDeviationMm);

        // Fallback when generate did not populate the emit log (loaded CAD only).
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            frameAnnulusCount = 0,
            failures = 1,
            toleranceMm = maxDeviationMm,
            note = "No emit-time contour log — regenerate CAD with ProjectReference mode.",
            frames = Array.Empty<object>(),
        }, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
