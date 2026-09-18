using Novolis.Audio.Core;
using Novolis.Audio.Playback;

namespace MeshBench.Services;

internal sealed class SavePointSound
{
    private readonly IAudioPlayback _playback;
    private readonly PcmBuffer _chime;

    public SavePointSound()
    {
        _playback = new NaudioPcmPlayback();
        _chime = CreateChime();
    }

    public void Play() => _ = _playback.PlayAsync(_chime);

    private static PcmBuffer CreateChime()
    {
        const int sampleRate = 44100;
        const float durationSeconds = 0.12f;
        var frameCount = (int)(sampleRate * durationSeconds);
        var bytes = new byte[frameCount * 2];
        const float freq = 880f;
        for (var i = 0; i < frameCount; i++)
        {
            var t = i / (float)sampleRate;
            var envelope = MathF.Exp(-t * 18f);
            var sample = MathF.Sin(MathF.Tau * freq * t) * envelope * 0.25f;
            var value = (short)(sample * short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new PcmBuffer(new PcmFormat(sampleRate, Channels: 1, SampleFormat: PcmSampleFormat.Int16), bytes, frameCount);
    }
}
