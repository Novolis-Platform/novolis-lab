using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlGunPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        GunModel gun,
        float orderedElevMils,
        float orderedDeflMils,
        string? fireBlockReason,
        bool solutionUnsafe,
        bool launchDetonation,
        float predictedRange,
        float tof)
    {
        var scale = m.Scale;
        var px = 8f;
        var py = 92f * scale;
        var pw = 360f * scale;
        var ph = 230f * scale;
        chrome.PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * scale;
        var ly = py + 28 * scale;
        var ls = 16f * scale;
        var lh = 28f * scale;

        chrome.DrawText(canvas, "FIRE CONTROL", lx, ly, 18f * scale, FireControlTheme.Accent);
        ly += lh;
        chrome.DrawText(canvas, $"QE    {gun.ElevationMils:F0} -> {orderedElevMils:F0} MIL", lx, ly, ls, FireControlTheme.Text);
        ly += lh;
        chrome.DrawText(canvas, $"DEFL  {gun.DeflectionMils:F0} -> {orderedDeflMils:F0} MIL", lx, ly, ls, FireControlTheme.Text);
        ly += lh;
        chrome.DrawText(canvas, $"PRED  {SimulationUnits.FormatRange(predictedRange)}   TOF {tof:F1} S", lx, ly, ls, solutionUnsafe ? FireControlTheme.Alert : FireControlTheme.Text);
        ly += lh;
        if (launchDetonation)
        {
            chrome.DrawText(canvas, "NO CLEARANCE — DETONATION AT PIECE", lx, ly, ls, FireControlTheme.Alert);
            ly += lh;
        }
        else if (solutionUnsafe)
        {
            chrome.DrawText(canvas, $"BELOW HE MIN {gun.Spec.MinEffectiveRangeMeters / 1000f:F1} KM", lx, ly, ls, FireControlTheme.Alert);
            ly += lh;
        }

        chrome.DrawText(
            canvas,
            fireBlockReason is null ? "CREW READY" : FireControlChromePrimitives.Truncate(fireBlockReason.ToUpperInvariant(), 36),
            lx,
            ly,
            ls,
            fireBlockReason is null ? FireControlTheme.Ready : FireControlTheme.Alert);
    }
}
