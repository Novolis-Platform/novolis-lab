using Novolis.Audio.Edit;

namespace MusicMakerLab;

/// <summary>
/// Seeds Arrangement with Kevin Graham — By The Sword (first ~20s) when available,
/// plus the free online SFX library (Mixkit).
/// </summary>
internal static class FullDemoBuilder
{
    public static MusicProject Build()
    {
        var project = new MusicProject("Music Maker Lab", sampleRate: 44_100);
        var master = AudioEditOps.AddTrack(project, "Master");
        var library = AudioEditOps.AddTrack(project, "Library clips");

        Console.WriteLine("Syncing free online media library (Mutopia MIDI + Mixkit SFX)…");
        var (ok, failed) = FreeMediaLibrary.EnsureCached(log: msg => Console.WriteLine("  " + msg));
        Console.WriteLine($"Free library cache: {ok} ok, {failed} failed → {FreeMediaLibrary.RootDirectory}");

        var imported = FreeMediaLibrary.ImportAudioIntoProject(project);
        Console.WriteLine($"Imported {imported} free SFX into Arrangement library.");

        // Prefer the composer’s public Ablaze preview (first ~20 seconds) on Master.
        var sword = ByTheSwordDemoAudio.LoadPcm(project.Format.SampleRate, TimeSpan.FromSeconds(20));
        if (sword is not null)
        {
            project.Title = "By The Sword — Kevin Graham";
            var asset = AudioEditOps.AddPcm(project, $"{ByTheSwordDemoAudio.Title} ({ByTheSwordDemoAudio.Artist})", sword);
            var clip = AudioEditOps.PlaceClip(project, master, asset, TimeSpan.Zero);
            AudioEditOps.SetClipEnvelope(
                clip,
                gain: 0.95f,
                fadeIn: TimeSpan.FromMilliseconds(40),
                fadeOut: TimeSpan.FromMilliseconds(600));
            Console.WriteLine($"Loaded {ByTheSwordDemoAudio.Title} — {ByTheSwordDemoAudio.Artist} ({sword.Duration:mm\\:ss\\.f})");
            Console.WriteLine($"Source: {ByTheSwordDemoAudio.SourcePage}");
            Console.WriteLine("Note: MIDI transcription of this track is not provided (copyright). Use Orchestral Score demos / free Mutopia MIDI / free-SFX sketches instead.");
        }
        else
        {
            Console.WriteLine("By The Sword unavailable — tone bed on Master.");
            var a = AudioEditOps.AddTone(project, "A3 bed", 220, TimeSpan.FromSeconds(4), amplitude: 0.2);
            var bed = AudioEditOps.PlaceClip(project, master, a, TimeSpan.Zero);
            AudioEditOps.SetClipEnvelope(bed, gain: 0.7f, fadeIn: TimeSpan.FromMilliseconds(200), fadeOut: TimeSpan.FromSeconds(0.8));
        }

        // Park a couple free hits on the Library clips track for Magix-style dragging.
        PlaceFirstLibraryHits(project, library, count: 3);
        return project;
    }

    static void PlaceFirstLibraryHits(MusicProject project, ArrangementTrack track, int count)
    {
        var hits = project.Assets
            .Where(a => a.Name.StartsWith("Mixkit", StringComparison.OrdinalIgnoreCase))
            .Take(count)
            .ToList();
        var t = TimeSpan.Zero;
        foreach (var asset in hits)
        {
            var clip = AudioEditOps.PlaceClip(project, track, asset, t);
            AudioEditOps.SetClipEnvelope(clip, gain: 0.85f, fadeIn: TimeSpan.FromMilliseconds(10), fadeOut: TimeSpan.FromMilliseconds(80));
            t += TimeSpan.FromSeconds(Math.Min(2.5, asset.Pcm.Duration.TotalSeconds + 0.4));
        }
    }
}
