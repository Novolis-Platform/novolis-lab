using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlRadioPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        string? speaker,
        string? line,
        bool voiceAvailable,
        bool scoreAvailable,
        bool audioHubPresent)
    {
        var scale = m.Scale;
        var width = m.Width;
        var height = m.Height;

        string label;
        SKColor color;
        if (!audioHubPresent)
        {
            label = "AUDIO OFF";
            color = FireControlTheme.Alert;
        }
        else if (!string.IsNullOrWhiteSpace(line))
        {
            label = string.IsNullOrWhiteSpace(speaker) ? line! : $"{speaker}: {line}";
            color = FireControlTheme.Accent;
        }
        else
        {
            var radio = voiceAvailable ? "RADIO READY" : "RADIO OFF";
            var score = scoreAvailable ? "SCORE ON" : "SCORE OFF";
            label = $"{radio}  |  {score}";
            color = voiceAvailable && scoreAvailable ? FireControlTheme.Dim : FireControlTheme.Alert;
        }

        var size = 15f * scale;
        var textWidth = chrome.MeasureText(label, size);
        var x = Math.Max(16f * scale, (width - textWidth) * 0.5f);
        var y = height - 36f * scale;
        chrome.PanelBox(canvas, x - 10f * scale, y - 18f * scale, textWidth + 20f * scale, 28f * scale);
        chrome.DrawText(canvas, label, x, y, size, color);
    }
}
