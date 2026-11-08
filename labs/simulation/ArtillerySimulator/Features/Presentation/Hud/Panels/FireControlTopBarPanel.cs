using Novolis.Physics.Ballistics;
using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlTopBarPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        BallisticTrajectoryRunner shot,
        float heroRange,
        GunModel gun,
        string? defenseLine,
        string? fireBlockReason,
        bool launchDetonation,
        bool solutionUnsafe,
        in ScoreSnapshot score)
    {
        var scale = m.Scale;
        var width = m.Width;
        var topH = 72f * scale;
        chrome.PanelBox(canvas, 8, 8, width - 16, topH);

        var phase = launchDetonation ? "NO CLR"
            : solutionUnsafe ? "UNSAFE"
            : fireBlockReason is not null ? "BUSY"
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? "IMPACT"
            : "READY";
        var phaseColor = launchDetonation || solutionUnsafe || fireBlockReason is not null ? FireControlTheme.Alert
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? FireControlTheme.Alert
            : FireControlTheme.Ready;

        chrome.DrawText(canvas, phase, 20 * scale, 28 * scale, 22f * scale, phaseColor);
        chrome.DrawText(
            canvas,
            $"{SimulationUnits.FormatRange(heroRange)}   ZONE {gun.ChargeLabel.ToUpperInvariant()}",
            200 * scale,
            28 * scale,
            20f * scale,
            FireControlTheme.Text);

        var scoreLine = $"SCORE {score.Points}   HI {score.HighScore}   {score.TimeText}";
        chrome.DrawText(
            canvas,
            scoreLine,
            width - 20 * scale - chrome.MeasureText(scoreLine, 18f * scale),
            28 * scale,
            18f * scale,
            score.IsNewHighScore ? FireControlTheme.Accent : FireControlTheme.Text);

        var mission = string.IsNullOrEmpty(defenseLine) ? ProductIdentity.Subtitle.ToUpperInvariant() : defenseLine;
        chrome.DrawText(canvas, FireControlChromePrimitives.Truncate(mission, 72), 20 * scale, 56 * scale, 15f * scale, FireControlTheme.Accent);
    }
}
