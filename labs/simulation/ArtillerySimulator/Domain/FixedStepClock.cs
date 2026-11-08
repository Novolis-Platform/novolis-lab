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
    /// Default cap is 8 steps (~67 ms) so a slow render frame cannot dump a huge
    /// physics backlog into the same Update — soft catch-up across later frames.
    /// </summary>
    public int Consume(float wallSeconds, int maxSteps = 8)
    {
        if (wallSeconds > 0f)
            _accumulator += Math.Min(wallSeconds, DtSeconds * maxSteps * 2f);

        var steps = Math.Min(maxSteps, (int)(_accumulator / DtSeconds));
        if (steps > 0)
            _accumulator -= steps * DtSeconds;

        // Drop debt beyond two caps so render hitches never create a multi-frame spiral.
        if (_accumulator > DtSeconds * maxSteps * 2f)
            _accumulator = DtSeconds * maxSteps;

        return steps;
    }
}
