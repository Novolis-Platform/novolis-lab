namespace SilkTraceStudio;

/// <summary>
/// Honest present rate: only advances when a new path-trace frame is blitted.
/// Uncapped host-loop FPS with VSync off is not path-trace throughput.
/// </summary>
internal sealed class SilkSmoothedBlitHz
{
    private float _blitHz;
    private float _sppPerSecond;
    private float _secondsSinceBlit;
    private int _lastSampleCount = -1;

    public float BlitHz => _blitHz;

    public float SppPerSecond => _sppPerSecond;

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds > 0f)
        {
            _secondsSinceBlit += deltaSeconds;
        }
    }

    public void NoteBlit(int sampleCount)
    {
        if (_secondsSinceBlit <= 1e-4f)
        {
            _lastSampleCount = sampleCount;
            return;
        }

        var instantHz = 1f / _secondsSinceBlit;
        _blitHz = _blitHz <= 1e-3f ? instantHz : _blitHz + (instantHz - _blitHz) * 0.25f;

        if (_lastSampleCount >= 0 && sampleCount >= _lastSampleCount)
        {
            var instantSpp = (sampleCount - _lastSampleCount) / _secondsSinceBlit;
            _sppPerSecond = _sppPerSecond <= 1e-3f
                ? instantSpp
                : _sppPerSecond + (instantSpp - _sppPerSecond) * 0.25f;
        }

        _lastSampleCount = sampleCount;
        _secondsSinceBlit = 0f;
    }
}
