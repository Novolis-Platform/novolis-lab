using Novolis.Audio.Core;

namespace XFighter.Game.Audio;

/// <summary>Starfighter-style procedural PCM (no asset files).</summary>
internal static class ProceduralSfx
{
    public static readonly PcmFormat Format = new(22_050, 1, PcmSampleFormat.Int16);

    public static PcmBuffer LaserPew() =>
        Build(0.14f, (t, _, _) =>
        {
            var env = MathF.Exp(-t * 28f);
            var freq = 1800f - t * 9200f;
            var tone = MathF.Sin(t * freq * MathF.Tau) * 0.55f;
            var noise = (Random.Shared.NextSingle() * 2f - 1f) * 0.35f;
            return (tone + noise) * env * 0.7f;
        });

    public static PcmBuffer ExplosionBoom() =>
        Build(0.55f, (t, _, _) =>
        {
            var env = MathF.Exp(-t * 5.5f);
            var noise = Random.Shared.NextSingle() * 2f - 1f;
            var rumble = MathF.Sin(t * 90f * MathF.Tau) * MathF.Exp(-t * 12f);
            return (noise * 0.65f + rumble * 0.45f) * env * 0.85f;
        });

    public static PcmBuffer RadioSquelch() =>
        Build(0.09f, (t, _, _) =>
        {
            var env = t < 0.02f ? t / 0.02f : MathF.Exp(-(t - 0.02f) * 40f);
            var noise = Random.Shared.NextSingle() * 2f - 1f;
            var crackle = MathF.Sin(t * 640f * MathF.Tau) * 0.25f;
            return (noise + crackle) * env * 0.55f;
        });

    public static PcmBuffer ShieldHit() =>
        Build(0.2f, (t, _, _) =>
        {
            var env = MathF.Exp(-t * 14f);
            return MathF.Sin(t * 420f * MathF.Tau) * env * 0.5f;
        });

    public static PcmBuffer EnemyBolt() =>
        Build(0.1f, (t, _, _) =>
        {
            var env = MathF.Exp(-t * 35f);
            var freq = 900f + t * 2400f;
            return MathF.Sin(t * freq * MathF.Tau) * env * 0.45f;
        });

    /// <summary>~1s engine segment; looped by <see cref="XFighterSoundscape"/>.</summary>
    public static PcmBuffer EngineSegment(float throttle, float phaseStart)
    {
        const float duration = 1f;
        var frameCount = (int)(Format.SampleRate * duration);
        var pcm = new byte[frameCount * Format.BytesPerFrame];
        var baseHz = 55f + throttle * 95f;
        var whineHz = 220f + throttle * 480f;
        for (var i = 0; i < frameCount; i++)
        {
            var t = phaseStart + i / (float)Format.SampleRate;
            var rumble = MathF.Sin(t * baseHz * MathF.Tau) * (0.22f + throttle * 0.18f);
            var whine = MathF.Sin(t * whineHz * MathF.Tau) * (0.08f + throttle * 0.14f);
            var flutter = MathF.Sin(t * 17f * MathF.Tau) * 0.04f * throttle;
            WriteSample(pcm, i, rumble + whine + flutter);
        }

        return new PcmBuffer(Format, pcm, frameCount);
    }

    private static PcmBuffer Build(float durationSeconds, Func<float, int, byte[], float> sampleAt)
    {
        var frameCount = (int)(Format.SampleRate * durationSeconds);
        var pcm = new byte[frameCount * Format.BytesPerFrame];
        for (var i = 0; i < frameCount; i++)
        {
            var t = i / (float)Format.SampleRate;
            WriteSample(pcm, i, sampleAt(t, i, pcm));
        }

        return new PcmBuffer(Format, pcm, frameCount);
    }

    private static void WriteSample(byte[] pcm, int index, float normalized)
    {
        var s = (short)Math.Clamp(normalized * short.MaxValue, short.MinValue, short.MaxValue);
        pcm[index * 2] = (byte)(s & 0xFF);
        pcm[index * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }
}
