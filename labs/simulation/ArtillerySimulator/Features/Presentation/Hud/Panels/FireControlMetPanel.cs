using System.Globalization;
using System.Numerics;
using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlMetPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        AtmosphereModel atmosphere,
        float sampleAlt,
        float windSpeed,
        float windFrom,
        double tempC,
        double qnh,
        double rh,
        Vector3 predictedImpact,
        TerrainWorld terrain)
    {
        var scale = m.Scale;
        var width = m.Width;
        var pw = 360f * scale;
        var ph = 210f * scale;
        var px = width - pw - 8;
        var py = 92f * scale;
        chrome.PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * scale;
        var ly = py + 28 * scale;
        var ls = 15f * scale;
        var lh = 26f * scale;
        var rho = atmosphere.DensityKgPerM3At(sampleAlt);

        chrome.DrawText(canvas, "MET / MPI", lx, ly, 18f * scale, FireControlTheme.Accent);
        ly += lh;
        chrome.DrawText(canvas, string.Create(CultureInfo.InvariantCulture, $"AIR   {rho:F3} KG/M3  {tempC:F0} C"), lx, ly, ls, FireControlTheme.Text);
        ly += lh;
        chrome.DrawText(canvas, string.Create(CultureInfo.InvariantCulture, $"QNH   {qnh:F0}  RH {rh:F0}%"), lx, ly, ls, FireControlTheme.Text);
        ly += lh;
        chrome.DrawText(canvas, string.Create(CultureInfo.InvariantCulture, $"WIND  {windSpeed:F1} M/S  FROM {windFrom:F0}"), lx, ly, ls, FireControlTheme.Text);
        ly += lh;
        chrome.DrawText(canvas, $"LAT   {KristiansandHeightSampler.MeanLatitudeDegrees:F1} N  KRS COAST", lx, ly, ls, FireControlTheme.Text);
        ly += lh;
        var mpiRange = FireControlHudMath.HorizontalDistance(terrain.GunBaseline, predictedImpact);
        chrome.DrawText(
            canvas,
            $"MPI   {SimulationUnits.FormatRange(mpiRange)}  H {predictedImpact.Y:F0} M",
            lx,
            ly,
            ls,
            FireControlChromePrimitives.ToSk(BallisticArcColors.MpiMark));
    }
}
