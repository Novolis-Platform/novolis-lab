namespace PulseStrip.Audio;

using System.Buffers.Binary;
using System.Text;
using Novolis.Audio;

/// <summary>Writes tiny procedural WAV cues and plays them through <see cref="IAudioEngine"/>.</summary>
internal sealed class PulseStripSfx : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _sfxDir;
    private readonly Dictionary<string, ISoundHandle> _sounds = new(StringComparer.Ordinal);
    private bool _ready;

    public PulseStripSfx(IAudioEngine engine, string contentDir)
    {
        _engine = engine;
        _sfxDir = Path.Combine(contentDir, "sfx");
        Directory.CreateDirectory(_sfxDir);
    }

    public void EnsureGenerated()
    {
        if (_ready)
            return;

        WriteTone("blip.wav", 880, 0.08, 0.35);
        WriteTone("boost.wav", 220, 0.22, 0.4, sweepHz: 480);
        WriteTone("fire.wav", 640, 0.12, 0.45);
        WriteTone("hit.wav", 140, 0.18, 0.5);
        WriteTone("lap.wav", 520, 0.25, 0.4, sweepHz: 780);
        WriteTone("pickup.wav", 990, 0.1, 0.35);

        TryLoad("blip");
        TryLoad("boost");
        TryLoad("fire");
        TryLoad("hit");
        TryLoad("lap");
        TryLoad("pickup");
        _ready = true;
    }

    public void Play(string name)
    {
        if (_sounds.TryGetValue(name, out var handle))
            _ = _engine.Play(handle);
    }

    public void Dispose()
    {
        // Engine owns native handles; nothing else to free.
    }

    private void TryLoad(string name)
    {
        var path = Path.Combine(_sfxDir, name + ".wav");
        if (!File.Exists(path))
            return;
        try
        {
            _sounds[name] = _engine.LoadSound(path);
        }
        catch
        {
            // Null engine or missing native — ignore.
        }
    }

    private void WriteTone(string fileName, double freqHz, double seconds, double amp, double? sweepHz = null)
    {
        var path = Path.Combine(_sfxDir, fileName);
        if (File.Exists(path))
            return;

        const int sampleRate = 22050;
        var count = Math.Max(1, (int)(sampleRate * seconds));
        var pcm = new short[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (double)sampleRate;
            var f = sweepHz is null ? freqHz : freqHz + (sweepHz.Value - freqHz) * (i / (double)count);
            var env = Math.Sin(Math.PI * i / count);
            var sample = Math.Sin(2 * Math.PI * f * t) * amp * env;
            pcm[i] = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
        }

        WriteWav(path, pcm, sampleRate);
    }

    private static void WriteWav(string path, short[] samples, int sampleRate)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        var dataBytes = samples.Length * 2;
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataBytes);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataBytes);
        var bytes = new byte[dataBytes];
        for (var i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        bw.Write(bytes);
    }
}
