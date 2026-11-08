using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlLegendPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        bool solutionUnsafe)
    {
        var scale = m.Scale;
        var width = m.Width;
        var pw = 280f * scale;
        var ph = 190f * scale;
        var px = width - pw - 8;
        var py = 280f * scale;
        chrome.PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * scale;
        var ly = py + 28 * scale;
        var ls = 15f * scale;
        var lh = 28f * scale;
        chrome.DrawText(canvas, "ARC LEGEND", lx, ly, 17f * scale, FireControlTheme.Accent);
        ly += lh;
        var aim = solutionUnsafe ? BallisticArcColors.OrderedUnsafe : BallisticArcColors.Ordered;
        var prob = solutionUnsafe ? BallisticArcColors.ProbabilityUnsafe : BallisticArcColors.Probability;
        LegendRow(canvas, chrome, ref ly, lx, ls, lh, aim, solutionUnsafe ? "AIMED (UNSAFE)" : "AIMED TRAJECTORY");
        LegendRow(canvas, chrome, ref ly, lx, ls, lh, prob, solutionUnsafe ? "PROBABILITY (UNSAFE)" : "PROBABILITY CONE");
        LegendRow(canvas, chrome, ref ly, lx, ls, lh, BallisticArcColors.Tube, "TUBE (LAYING)");
        LegendRow(canvas, chrome, ref ly, lx, ls, lh, BallisticArcColors.Flight, "LIVE ROUND");
        LegendRow(canvas, chrome, ref ly, lx, ls, lh, BallisticArcColors.MinRange, "HE MIN RING");
    }

    private static void LegendRow(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        ref float ly,
        float lx,
        float ls,
        float lh,
        System.Drawing.Color color,
        string label)
    {
        var scale = chrome.Scale;
        var swatch = 22f * scale;
        var sk = FireControlChromePrimitives.ToSk(color);
        using (var fill = new SKPaint { Color = sk, Style = SKPaintStyle.Fill, IsAntialias = true })
            canvas.DrawRect(lx, ly - 12 * scale, swatch, 10 * scale, fill);
        chrome.DrawText(canvas, label, lx + swatch + 10 * scale, ly, ls, FireControlTheme.Text);
        ly += lh;
    }
}
