namespace ArtillerySimulator;

internal static class FireControlHitTester
{
    public static bool HitAny(IReadOnlyList<FireControlUiButton> buttons, float mx, float my, in FireControlHudMetrics m)
    {
        foreach (var btn in buttons)
        {
            if (btn.Contains(mx, my))
                return true;
        }

        var scale = m.Scale;
        var width = m.Width;
        var height = m.Height;

        if (mx < 380 * scale && my < 320 * scale)
            return true;
        if (mx > width - 380 * scale && my < 460 * scale)
            return true;
        if (my > height - 250 * scale)
            return true;
        return false;
    }
}
