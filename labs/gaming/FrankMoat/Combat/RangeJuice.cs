namespace FrankMoat.Combat;

internal sealed class RangeJuice
{
    public float Shake { get; private set; }

    public float MuzzleTimer { get; private set; }

    public void AddShake(float amount) => Shake = MathF.Min(1.4f, Shake + amount);

    public void FlashMuzzle(float seconds = 0.08f) => MuzzleTimer = MathF.Max(MuzzleTimer, seconds);

    public void Tick(float dt)
    {
        Shake = MathF.Max(0f, Shake - dt * 4.2f);
        MuzzleTimer = MathF.Max(0f, MuzzleTimer - dt);
    }

    public void Clear()
    {
        Shake = 0f;
        MuzzleTimer = 0f;
    }
}
