namespace ArtillerySimulator;

internal static class FireControlLayout
{
    public static void Build(
        List<FireControlUiButton> buttons,
        in FireControlHudMetrics m,
        GunModel gun,
        bool followProjectile,
        bool canFire,
        bool chargeMenuOpen)
    {
        buttons.Clear();
        var scale = m.Scale;
        var width = m.Width;
        var height = m.Height;

        var bw = 280f * scale;
        var bh = 42f * scale;
        var bx = 8f;
        var by = height - 180f * scale;
        buttons.Add(new FireControlUiButton("charge-toggle", bx, by, bw, bh, $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}", true));
        if (chargeMenuOpen)
        {
            for (var i = 0; i < gun.Spec.Charges.Count; i++)
            {
                var y = by - (i + 1) * (bh + 4 * scale);
                buttons.Add(new FireControlUiButton($"charge-{i}", bx, y, bw, bh, gun.Spec.Charges[i].Label, gun.ChargeIndex == i));
            }
        }

        var gap = 8f * scale;
        var cbw = 120f * scale;
        var cbh = 36f * scale;
        var cx = width - 8 - 4 * cbw - 3 * gap;
        var cy = height - 120f * scale;
        buttons.Add(new FireControlUiButton("follow", cx, cy, cbw, cbh, followProjectile ? "SHELL" : "GUN", true));
        buttons.Add(new FireControlUiButton("snap-gun", cx + cbw + gap, cy, cbw, cbh, "GUN VIEW", false));
        buttons.Add(new FireControlUiButton("snap-battle", cx + 2 * (cbw + gap), cy, cbw, cbh, "BATTLE", false));
        buttons.Add(new FireControlUiButton("map", cx + 3 * (cbw + gap), cy, cbw, cbh, "MAP", false));

        var fbw = 180f * scale;
        var fbh = 48f * scale;
        buttons.Add(new FireControlUiButton("fire", (width - fbw) * 0.5f, height - 64f * scale, fbw, fbh, "FIRE", canFire));
    }
}
