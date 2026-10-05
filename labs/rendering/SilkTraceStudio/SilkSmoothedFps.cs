namespace SilkTraceStudio;

internal sealed class SilkSmoothedFps
{
    private float _value = 60f;

    public float Value => _value;

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 1e-4f)
        {
            return;
        }

        var instant = 1f / deltaSeconds;
        _value += (instant - _value) * 0.12f;
    }
}
