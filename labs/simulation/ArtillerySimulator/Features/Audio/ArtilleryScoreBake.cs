using Novolis.Audio;
using Novolis.Audio.Core;
using Novolis.Audio.Midi;

namespace ArtillerySimulator;

/// <summary>Bakes <see cref="MusicScore.CreateFrontlineRemnant"/> to a loopable WAV via MidiSynth.</summary>
internal static class ArtilleryScoreBake
{
    public const string FileName = "odderoya-frontline.wav";

    public static string EnsureBaked(string assetsDir)
    {
        var musicDir = Path.Combine(assetsDir, "music");
        Directory.CreateDirectory(musicDir);
        var path = Path.Combine(musicDir, FileName);
        if (File.Exists(path) && new FileInfo(path).Length > 8_000)
            return path;

        var format = new PcmFormat(22050, 1, PcmSampleFormat.Int16);
        var bank = InstrumentBank.CreateDefault();
        var score = MusicScore.CreateFrontlineRemnant();
        var pcm = MidiSynth.RenderScore(format, bank, score);
        new WavEncoder().EncodeFile(pcm, path);
        return path;
    }

    public static TimeSpan EstimateDuration()
    {
        // 24 bars × 4 beats @ 118 BPM ≈ 48.8s (+ release tail).
        return TimeSpan.FromSeconds(24.0 * 4.0 * 60.0 / 118.0 + 1.2);
    }
}
