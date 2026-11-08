using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlChargeMenuPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        GunModel gun,
        bool chargeMenuOpen)
    {
        var scale = m.Scale;
        var height = m.Height;
        var bw = 280f * scale;
        var bh = 42f * scale;
        var bx = 8f;
        var by = height - 230f * scale;
        var label = $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}";
        chrome.DrawButtonVisual(canvas, bx, by, bw, bh, label, selected: true);

        if (!chargeMenuOpen)
            return;

        for (var i = 0; i < gun.Spec.Charges.Count; i++)
        {
            var y = by - (i + 1) * (bh + 4 * scale);
            var zone = gun.Spec.Charges[i];
            var item = $"{i + 1}  {zone.Label.ToUpperInvariant()}  {zone.MuzzleSpeedMps:F0} M/S";
            chrome.DrawButtonVisual(canvas, bx, y, bw, bh, item, gun.ChargeIndex == i);
        }
    }
}
