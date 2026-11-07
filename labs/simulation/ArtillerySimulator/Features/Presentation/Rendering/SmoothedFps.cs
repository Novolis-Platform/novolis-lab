namespace ArtillerySimulator;

internal sealed class SmoothedFps
{
    private float _value = 60f;

    public float Value => _value;

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 1e-6f)
            return;

        var instant = 1f / deltaSeconds;
        _value += (instant - _value) * 0.12f;
    }
}
