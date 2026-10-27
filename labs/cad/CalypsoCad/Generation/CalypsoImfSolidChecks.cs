using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

internal static class CalypsoImfSolidChecks
{
    public static int CountViolations(CadDocument doc, float loa)
    {
        var violations = 0;
        foreach (var e in doc.Entities)
        {
            if (!string.Equals(e.Kind, "box", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsStructuralOrOutfit(e))
                continue;
            if (e.Points is not { Count: >= 2 })
                continue;
            var c = e.Points[0];
            var h = e.Points[1];
            var y = c[0];
            var up = c[1];
            var zStem = loa * 0.5f - c[2];
            var hy = MathF.Abs(h[0]);
            var hu = MathF.Abs(h[1]);
            var hz = MathF.Abs(h[2]);
            foreach (var sampleUp in new[] { up - hu, up, up + hu })
            {
                foreach (var sampleY in new[] { y - hy, y, y + hy })
                {
                    if (!CalypsoLockGenerator.InsideHullPublic(sampleY, sampleUp, zStem, 0.01f))
                        violations++;
                }
            }
        }

        return violations;
    }

    private static bool IsStructuralOrOutfit(CadEntity e)
    {
        if (e.Name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (e.Name?.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (e.Name?.StartsWith("ramp-", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true
            && e.Name.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase))
            return true;
        if (e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (e.Properties is not null
            && e.Properties.TryGetValue("role", out var role)
            && string.Equals(role.GetString(), "structure", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
