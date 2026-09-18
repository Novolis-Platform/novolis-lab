using System.Net.Http;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Novolis.Audio.Core;

namespace MusicMakerLab;

/// <summary>
/// Loads Kevin Graham — “By The Sword” (public preview from the composer’s Ablaze page)
/// for local Music Maker Lab demos. Cache: %LOCALAPPDATA%/Novolis/MusicMakerLab/
/// </summary>
internal static class ByTheSwordDemoAudio
{
    public const string Title = "By The Sword";
    public const string Artist = "Kevin Graham";
    public const string SourcePage = "https://www.kevingrahamcomposer.com/ablaze";

    /// <summary>Composer-hosted SE master preview (Ablaze).</summary>
    public const string PreviewMp3Url =
        "https://static1.squarespace.com/static/5d01a609f96e520001c8c02b/5eebdc989fe4e4788eae6048/5eebdce928e84d74b9f55c18/1592515896317/01_By+The+Sword_SE+Master_v1.mp3";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static string CacheDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "MusicMakerLab");

    public static string CacheMp3Path => Path.Combine(CacheDirectory, "ByTheSword_KevinGraham.mp3");

    /// <summary>Ensures the preview MP3 is cached locally. Returns path or null.</summary>
    public static string? EnsureCached(bool downloadIfMissing = true)
    {
        try
        {
            if (File.Exists(CacheMp3Path) && new FileInfo(CacheMp3Path).Length > 10_000)
                return CacheMp3Path;

            if (!downloadIfMissing)
                return null;

            Directory.CreateDirectory(CacheDirectory);
            var tmp = CacheMp3Path + ".partial";
            using (var response = Http.GetAsync(PreviewMp3Url).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var stream = response.Content.ReadAsStream();
                using var file = File.Create(tmp);
                stream.CopyTo(file);
            }

            if (File.Exists(CacheMp3Path))
                File.Delete(CacheMp3Path);
            File.Move(tmp, CacheMp3Path);
            return CacheMp3Path;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"By The Sword cache failed: {ex.Message}");
            try
            {
                var partial = CacheMp3Path + ".partial";
                if (File.Exists(partial))
                    File.Delete(partial);
            }
            catch
            {
                // ignore
            }

            return File.Exists(CacheMp3Path) ? CacheMp3Path : null;
        }
    }

    /// <summary>
    /// Decodes mono Int16 PCM at <paramref name="sampleRate"/>, optionally truncated to <paramref name="maxDuration"/>.
    /// </summary>
    public static PcmBuffer? LoadPcm(int sampleRate = 44_100, TimeSpan? maxDuration = null)
    {
        var path = EnsureCached();
        if (path is null)
            return null;

        try
        {
            using var reader = new AudioFileReader(path);
            ISampleProvider samples = reader;
            if (reader.WaveFormat.Channels == 2)
                samples = new StereoToMonoSampleProvider(reader);
            else if (reader.WaveFormat.Channels > 2)
                throw new NotSupportedException($"Unsupported channel count: {reader.WaveFormat.Channels}");

            var srcRate = samples.WaveFormat.SampleRate;
            var maxSeconds = maxDuration?.TotalSeconds ?? reader.TotalTime.TotalSeconds;
            var srcFrames = Math.Max(1, (int)(srcRate * maxSeconds));
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
            Console.Error.WriteLine($"By The Sword decode failed: {ex.Message}");
            return null;
        }
    }
}
