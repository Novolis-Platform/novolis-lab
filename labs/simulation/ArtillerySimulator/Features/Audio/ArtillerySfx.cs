using System.Buffers.Binary;
using System.Text;
using Novolis.Audio;

namespace ArtillerySimulator;

/// <summary>Loads OSS impact cues and writes procedural muzzle/breech WAVs for <see cref="IAudioEngine"/>.</summary>
internal sealed class ArtillerySfx : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _sfxDir;
    private readonly Dictionary<string, ISoundHandle> _sounds = new(StringComparer.Ordinal);
    private bool _ready;

    public ArtillerySfx(IAudioEngine engine, string assetsDir)
    {
        _engine = engine;
        _sfxDir = Path.Combine(assetsDir, "sfx");
        Directory.CreateDirectory(_sfxDir);
    }

    public void EnsureReady()
    {
        if (_ready)
            return;

        WriteNoiseBurst("muzzle.wav", seconds: 0.55, amp: 0.72, lowHz: 55, highHz: 420);
        WriteNoiseBurst("breech.wav", seconds: 0.18, amp: 0.4, lowHz: 180, highHz: 900);
        WriteTone("radio-blip.wav", 740, 0.07, 0.28, sweepHz: 420);
        WriteTone("warning.wav", 520, 0.35, 0.32, sweepHz: 280);

        TryLoad("muzzle");
        TryLoad("breech");
        TryLoad("radio-blip");
        TryLoad("warning");
        TryLoad("impact-he");
        TryLoad("impact-air");
        TryLoad("beep");
        _ready = true;
    }

    public void Play(string name)
    {
        if (_sounds.TryGetValue(name, out var handle))
            _ = _engine.Play(handle);
    }

    public void PlayImpact(bool airburst) =>
        Play(airburst ? "impact-air" : "impact-he");

    public void Dispose()
    {
        // Engine owns native handles.
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
            // Missing native decoder / device — ignore.
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

    private void WriteNoiseBurst(string fileName, double seconds, double amp, double lowHz, double highHz)
    {
        var path = Path.Combine(_sfxDir, fileName);
        if (File.Exists(path))
            return;

        const int sampleRate = 22050;
        var count = Math.Max(1, (int)(sampleRate * seconds));
        var pcm = new short[count];
        var rng = new Random(fileName.GetHashCode(StringComparison.Ordinal));
        var phase = 0.0;
        for (var i = 0; i < count; i++)
        {
            var n = i / (double)count;
            var env = Math.Pow(1.0 - n, 1.35) * (0.55 + 0.45 * Math.Sin(Math.PI * n));
            var noise = (rng.NextDouble() * 2.0 - 1.0) * 0.55;
            var f = lowHz + (highHz - lowHz) * (1.0 - n);
            phase += 2 * Math.PI * f / sampleRate;
            var boom = Math.Sin(phase) * 0.85;
            var sample = (noise + boom) * amp * env;
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
