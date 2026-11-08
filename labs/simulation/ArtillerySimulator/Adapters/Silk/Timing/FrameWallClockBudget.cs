using System.Diagnostics;

namespace ArtillerySimulator;

internal sealed class FrameWallClockBudget
{
    private readonly Stopwatch _wall;
    private long _lastWallTimestamp;

    public FrameWallClockBudget(Stopwatch wall)
    {
        _wall = wall;
        _lastWallTimestamp = wall.ElapsedTicks;
    }

    public void Stamp() => _lastWallTimestamp = _wall.ElapsedTicks;

    public float AdvanceSeconds()
    {
        var now = _wall.ElapsedTicks;
        var wallSeconds = (float)((now - _lastWallTimestamp) / (double)Stopwatch.Frequency);
        _lastWallTimestamp = now;
        if (wallSeconds < 0f)
            wallSeconds = 0f;
        return Math.Min(wallSeconds, FixedStepClock.DtSeconds * 8f);
    }
}
