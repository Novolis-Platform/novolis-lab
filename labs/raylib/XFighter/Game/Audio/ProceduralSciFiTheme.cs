using Novolis.Audio.Core;

namespace XFighter.Game.Audio;

/// <summary>Looping procedural "MIDI-like" space-opera motif (square lead + sine bass + arp).</summary>
internal static class ProceduralSciFiTheme
{
    private const float Bpm = 92f;
    private const float LoopBeats = 16f;

    private static readonly Note[] Score =
    [
        // Bass roots (whole notes)
        new(0f, 4f, 110f, 0.22f, Wave.Sine),
        new(4f, 4f, 87.31f, 0.2f, Wave.Sine),
        new(8f, 4f, 130.81f, 0.21f, Wave.Sine),
        new(12f, 4f, 98f, 0.2f, Wave.Sine),
        // Heroic minor lead (square)
        new(0f, 0.5f, 440f, 0.12f, Wave.Square),
        new(0.5f, 0.5f, 523.25f, 0.11f, Wave.Square),
        new(1f, 0.5f, 659.25f, 0.11f, Wave.Square),
        new(1.5f, 0.5f, 783.99f, 0.1f, Wave.Square),
        new(2f, 1f, 880f, 0.13f, Wave.Square),
        new(3.5f, 0.5f, 783.99f, 0.09f, Wave.Square),
        new(4f, 0.5f, 698.46f, 0.1f, Wave.Square),
        new(4.5f, 0.5f, 659.25f, 0.1f, Wave.Square),
        new(5f, 1f, 587.33f, 0.11f, Wave.Square),
        new(6.5f, 0.5f, 659.25f, 0.09f, Wave.Square),
        new(7f, 0.5f, 783.99f, 0.1f, Wave.Square),
        new(8f, 1.5f, 880f, 0.12f, Wave.Square),
        new(10f, 0.5f, 783.99f, 0.09f, Wave.Square),
        new(10.5f, 0.5f, 698.46f, 0.09f, Wave.Square),
        new(11f, 1f, 659.25f, 0.1f, Wave.Square),
        new(12f, 0.5f, 587.33f, 0.1f, Wave.Square),
        new(12.5f, 0.5f, 523.25f, 0.09f, Wave.Square),
        new(13f, 1f, 440f, 0.11f, Wave.Square),
        new(14.5f, 1f, 392f, 0.1f, Wave.Square),
        // Arpeggio bed
        new(0f, 0.25f, 329.63f, 0.05f, Wave.Triangle),
        new(0.25f, 0.25f, 392f, 0.05f, Wave.Triangle),
        new(0.5f, 0.25f, 440f, 0.05f, Wave.Triangle),
        new(0.75f, 0.25f, 523.25f, 0.05f, Wave.Triangle),
        new(1f, 0.25f, 440f, 0.05f, Wave.Triangle),
        new(1.25f, 0.25f, 392f, 0.05f, Wave.Triangle),
        new(1.5f, 0.25f, 329.63f, 0.05f, Wave.Triangle),
        new(1.75f, 0.25f, 392f, 0.05f, Wave.Triangle),
        new(8f, 0.25f, 261.63f, 0.05f, Wave.Triangle),
        new(8.25f, 0.25f, 329.63f, 0.05f, Wave.Triangle),
        new(8.5f, 0.25f, 392f, 0.05f, Wave.Triangle),
        new(8.75f, 0.25f, 440f, 0.05f, Wave.Triangle),
    ];

    public static PcmBuffer RenderLoop()
    {
        var beat = 60f / Bpm;
        var duration = LoopBeats * beat;
        var format = ProceduralSfx.Format;
        var frameCount = (int)(format.SampleRate * duration);
        var pcm = new byte[frameCount * format.BytesPerFrame];
        var sampleRate = format.SampleRate;

        for (var i = 0; i < frameCount; i++)
        {
            var t = i / (float)sampleRate;
            var beatTime = t / beat;
            var v = 0f;
            foreach (var note in Score)
            {
                if (beatTime < note.StartBeat || beatTime >= note.StartBeat + note.LengthBeats)
                    continue;

                var local = (beatTime - note.StartBeat) / note.LengthBeats;
                var env = MathF.Min(local * 8f, 1f) * MathF.Min((1f - local) * 6f, 1f);
                var phase = t * note.Frequency * MathF.Tau;
                v += Sample(note.Waveform, phase) * note.Amplitude * env;
            }

            WriteSample(pcm, i, v * 0.85f);
        }

        return new PcmBuffer(format, pcm, frameCount);
    }

    private static float Sample(Wave wave, float phase) =>
        wave switch
        {
            Wave.Square => MathF.Sign(MathF.Sin(phase)) * 0.55f,
            Wave.Sine => MathF.Sin(phase),
            Wave.Triangle => 2f / MathF.PI * MathF.Asin(MathF.Sin(phase)),
            _ => 0f,
        };

    private static void WriteSample(byte[] pcm, int index, float normalized)
    {
        var s = (short)Math.Clamp(normalized * short.MaxValue, short.MinValue, short.MaxValue);
        pcm[index * 2] = (byte)(s & 0xFF);
        pcm[index * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }

    private enum Wave
    {
        Square,
        Sine,
        Triangle,
    }

    private readonly record struct Note(
        float StartBeat,
        float LengthBeats,
        float Frequency,
        float Amplitude,
        Wave Waveform);
}
