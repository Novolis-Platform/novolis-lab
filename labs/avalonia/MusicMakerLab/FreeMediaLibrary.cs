using System.Net.Http;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Novolis.Audio.Core;
using Novolis.Audio.Edit;
using Novolis.Audio.Midi;

namespace MusicMakerLab;

/// <summary>
/// Downloads + caches free online MIDI/SFX into LocalAppData and imports into Arrangement.
/// </summary>
internal static class FreeMediaLibrary
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "MusicMakerLab",
            "FreeLibrary");

    public static string MidiDirectory => Path.Combine(RootDirectory, "midi");
    public static string AudioDirectory => Path.Combine(RootDirectory, "audio");

    public static string PathFor(FreeMediaEntry entry) =>
        Path.Combine(entry.Kind == FreeMediaKind.Midi ? MidiDirectory : AudioDirectory, entry.LocalFileName);

    /// <summary>Ensures all catalog entries are cached. Returns (ok, failed).</summary>
    public static (int Ok, int Failed) EnsureCached(IEnumerable<FreeMediaEntry>? entries = null, Action<string>? log = null)
    {
        Directory.CreateDirectory(MidiDirectory);
        Directory.CreateDirectory(AudioDirectory);
        var ok = 0;
        var failed = 0;
        foreach (var entry in entries ?? FreeMediaCatalog.All)
        {
            try
            {
                if (EnsureCached(entry) is not null)
                {
                    ok++;
                    log?.Invoke($"Cached {entry.Title}");
                }
                else
                {
                    failed++;
                    log?.Invoke($"Failed {entry.Title}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                log?.Invoke($"Failed {entry.Title}: {ex.Message}");
            }
        }

        return (ok, failed);
    }

    public static string? EnsureCached(FreeMediaEntry entry)
    {
        var path = PathFor(entry);
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 200)
                return path;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".partial";
            using (var response = Http.GetAsync(entry.DownloadUrl).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var stream = response.Content.ReadAsStream();
                using var file = File.Create(tmp);
                stream.CopyTo(file);
            }

            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
            return path;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Free media cache failed ({entry.Id}): {ex.Message}");
            try
            {
                var partial = path + ".partial";
                if (File.Exists(partial))
                    File.Delete(partial);
            }
            catch
            {
                // ignore
            }

            return File.Exists(path) ? path : null;
        }
    }

    /// <summary>Imports cached audio entries into the project library (skips duplicates by name).</summary>
    public static int ImportAudioIntoProject(MusicProject project, int sampleRate = 44_100)
    {
        ArgumentNullException.ThrowIfNull(project);
        var added = 0;
        var existing = new HashSet<string>(project.Assets.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in FreeMediaCatalog.AudioEntries)
        {
            var path = EnsureCached(entry);
            if (path is null)
                continue;
            if (existing.Contains(entry.Title))
                continue;

            var pcm = DecodeMp3(path, sampleRate, TimeSpan.FromSeconds(12));
            if (pcm is null)
                continue;

            AudioEditOps.AddPcm(project, entry.Title, pcm);
            existing.Add(entry.Title);
            added++;
        }

        return added;
    }

    /// <summary>Lists cached MIDI paths that exist on disk.</summary>
    public static IReadOnlyList<(FreeMediaEntry Entry, string Path)> CachedMidiFiles()
    {
        var list = new List<(FreeMediaEntry, string)>();
        foreach (var entry in FreeMediaCatalog.MidiEntries)
        {
            var path = PathFor(entry);
            if (File.Exists(path) && new FileInfo(path).Length > 200)
                list.Add((entry, path));
        }

        return list;
    }

    public static MusicScore? LoadMidiScore(FreeMediaEntry entry)
    {
        var path = EnsureCached(entry);
        if (path is null)
            return null;

        var seq = StandardMidiFile.Read(path);
        var score = new MusicScore(entry.Title, seq.TempoBpm, barCount: 8)
        {
            Composer = entry.ArtistOrSource,
            InstrumentName = "Imported MIDI",
            SnapBeats = 0.25,
        };
        score.ReplaceFromSequence(seq);
        score.Title = entry.Title;
        score.Composer = entry.ArtistOrSource;
        return score;
    }

    /// <summary>
    /// Builds an audio→MIDI sketch from a free Mixkit clip (not for copyrighted commercial tracks).
    /// </summary>
    public static MusicScore? SketchFromFreeAudio(FreeMediaEntry entry, int sampleRate = 44_100)
    {
        if (entry.Kind != FreeMediaKind.Audio)
            return null;
        var path = EnsureCached(entry);
        if (path is null)
            return null;
        var pcm = DecodeMp3(path, sampleRate, TimeSpan.FromSeconds(16));
        return pcm is null ? null : AudioToMidiSketch.FromPcm(pcm, $"{entry.Title} · MIDI sketch");
    }

    static PcmBuffer? DecodeMp3(string path, int sampleRate, TimeSpan maxDuration)
    {
        try
        {
            using var reader = new AudioFileReader(path);
            ISampleProvider samples = reader;
            if (reader.WaveFormat.Channels == 2)
                samples = new StereoToMonoSampleProvider(reader);
            else if (reader.WaveFormat.Channels > 2)
                return null;

            var srcRate = samples.WaveFormat.SampleRate;
            var srcFrames = Math.Max(1, (int)(srcRate * maxDuration.TotalSeconds));
            var srcBuf = new float[srcFrames];
            var got = 0;
            while (got < srcFrames)
            {
                var n = samples.Read(srcBuf, got, srcFrames - got);
                if (n <= 0)
                    break;
                got += n;
            }

            if (got <= 0)
                return null;

            float[] dest;
            int destFrames;
            if (srcRate == sampleRate)
            {
                dest = srcBuf;
                destFrames = got;
            }
            else
            {
                destFrames = Math.Max(1, (int)(got * (sampleRate / (double)srcRate)));
                dest = new float[destFrames];
                for (var i = 0; i < destFrames; i++)
                {
                    var srcIndex = Math.Min(got - 1, (int)(i * (srcRate / (double)sampleRate)));
                    dest[i] = srcBuf[srcIndex];
                }
            }

            var bytes = new byte[destFrames * 2];
            for (var i = 0; i < destFrames; i++)
            {
                var s = (short)(Math.Clamp(dest[i], -1f, 1f) * short.MaxValue);
                bytes[i * 2] = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }

            return new PcmBuffer(new PcmFormat(sampleRate, 1, PcmSampleFormat.Int16), bytes, destFrames);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Decode failed ({path}): {ex.Message}");
            return null;
        }
    }
}
