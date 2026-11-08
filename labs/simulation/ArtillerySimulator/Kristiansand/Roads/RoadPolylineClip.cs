using System.Numerics;

namespace ArtillerySimulator;

internal static class RoadPolylineClip
{
    /// <summary>Longest contiguous on-map run — keeps one coherent road approach.</summary>
    public static Vector2[] LongestInMapRun(Vector2[] points, Func<Vector2, bool> inMap)
    {
        if (points.Length == 0)
            return points;

        var bestStart = -1;
        var bestLen = 0;
        var runStart = -1;
        var runLen = 0;
        for (var i = 0; i < points.Length; i++)
        {
            if (inMap(points[i]))
            {
                if (runStart < 0)
                    runStart = i;
                runLen++;
                if (runLen > bestLen)
                {
                    bestLen = runLen;
                    bestStart = runStart;
                }
            }
            else
            {
                runStart = -1;
                runLen = 0;
            }
        }

        if (bestStart < 0 || bestLen < 2)
            return [];

        var clipped = new Vector2[bestLen];
        Array.Copy(points, bestStart, clipped, 0, bestLen);
        return clipped;
    }
}
