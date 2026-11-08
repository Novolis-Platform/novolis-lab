namespace ArtillerySimulator;

/// <summary>
/// Accumulates wall time into fixed simulation steps so ballistics and battle systems
/// advance at a stable rate independent of render FPS.
/// </summary>
internal sealed class FixedStepClock
{
    public const float Hz = 120f;
    public const float DtSeconds = 1f / Hz;

    private float _accumulator;

    public float AccumulatorSeconds => _accumulator;

    public void Reset() => _accumulator = 0f;

    /// <summary>
    /// Consumes wall-clock delta into a capped number of fixed steps.
    /// Excess time beyond the cap stays in the accumulator (soft catch-up).
    /// </summary>
    public int Consume(float wallSeconds, int maxSteps = 32)
    {
        if (wallSeconds > 0f)
            _accumulator += wallSeconds;

        var steps = Math.Min(maxSteps, (int)(_accumulator / DtSeconds));
        if (steps > 0)
            _accumulator -= steps * DtSeconds;

        // Avoid unbounded spiral-of-death if a hitch dumps seconds of debt.
        if (_accumulator > DtSeconds * maxSteps)
            _accumulator = DtSeconds * maxSteps;

        return steps;
    }
}
