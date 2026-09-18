using Avalonia;
using Novolis.Audio.Midi;

namespace MusicMakerLab;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Warm TimGM6mb + Kevin Graham preview so Arrangement/Score are ready.
            _ = Task.Run(() =>
            {
                try
                {
                    if (SoundFontEngine.EnsureInstalled(downloadIfMissing: true))
                        Console.WriteLine($"SoundFont ready: {SoundFontEngine.LoadedPath}");
                    else
                        Console.WriteLine($"SoundFont unavailable ({SoundFontEngine.LastError}) — parametric fallback.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"SoundFont warm-up failed: {ex.Message}");
                }

                try
                {
                    var path = ByTheSwordDemoAudio.EnsureCached(downloadIfMissing: true);
                    Console.WriteLine(path is null
                        ? "By The Sword preview not cached."
                        : $"By The Sword cached: {path}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"By The Sword warm-up failed: {ex.Message}");
                }
            });

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
