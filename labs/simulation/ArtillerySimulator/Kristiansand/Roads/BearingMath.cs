using System.Numerics;

namespace ArtillerySimulator;

internal static class BearingMath
{
    public static float AngularDistance(float a, float b)
    {
        var d = MathF.Abs(a - b) % MathF.Tau;
        return d > MathF.PI ? MathF.Tau - d : d;
    }

    public static float DistancePointToRay(Vector2 point, Vector2 origin, Vector2 dirUnit)
    {
        var rel = point - origin;
        var along = Vector2.Dot(rel, dirUnit);
        if (along < 0f)
            return rel.Length();
        var closest = origin + dirUnit * along;
        return Vector2.Distance(point, closest);
    }
}
