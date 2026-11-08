namespace ArtillerySimulator;

/// <summary>
/// Samples lay / MV residuals from a piece <see cref="BallisticErrorBudget"/> and builds envelope offsets.
/// Live met stays on <see cref="AtmosphereModel"/> — this only models piece/ammo PE and residual-met uncertainty for the cone.
/// </summary>
internal sealed class DeliverySampler
{
    private readonly BallisticErrorBudget _budget;
    private readonly Random _rng;

    public DeliverySampler(BallisticErrorBudget budget, int? seed = null)
    {
        _budget = budget;
        _rng = seed is { } s ? new Random(s) : new Random(unchecked(Environment.TickCount ^ 0x155A17));
    }

    public BallisticErrorBudget Budget => _budget;

    public FiredDelivery? LastFire { get; private set; }

    /// <summary>Sample a fired delivery around the ordered / snapped tube and nominal MV.</summary>
    public FiredDelivery SampleFire(float elevationDegrees, float azimuthDegrees, float nominalMuzzleSpeedMps)
    {
        var dElevMils = NextGaussian() * NatoMils.SigmaFromPe(_budget.ElevationPeMils);
        var dDeflMils = NextGaussian() * NatoMils.SigmaFromPe(_budget.DeflectionPeMils);
        var dMvFrac = NextGaussian() * NatoMils.SigmaFromPe(_budget.MuzzleVelocityPeFraction);

        var elev = elevationDegrees + NatoMils.MilsToDegrees(dElevMils);
        var az = azimuthDegrees + NatoMils.MilsToDegrees(dDeflMils);
        var mv = MathF.Max(50f, nominalMuzzleSpeedMps * (1f + dMvFrac));

        var delivery = new FiredDelivery(elev, az, mv, dElevMils, dDeflMils, dMvFrac);
        LastFire = delivery;
        return delivery;
    }

    /// <summary>Four envelope corners at ±EnvelopePeScale × PE — cheap visual proxy, not a Monte-Carlo cloud.</summary>
    public EnvelopeOffset[] EnvelopeCorners()
    {
        var e = _budget.EnvelopeElevationDegrees;
        var a = _budget.EnvelopeDeflectionDegrees;
        var m = _budget.EnvelopeMuzzleFraction;
        var w = _budget.EnvelopeWindFraction;
        return
        [
            new(e, a * 0.35f, m, w),
            new(-e, -a * 0.35f, -m, -w),
            new(e * 0.4f, a, m * 0.5f, -w),
            new(-e * 0.4f, -a, -m * 0.5f, w),
        ];
    }

    private float NextGaussian()
    {
        // Box–Muller
        double u1;
        do
        {
            u1 = 1.0 - _rng.NextDouble();
        } while (u1 <= double.Epsilon);

        var u2 = 1.0 - _rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
