using System.Drawing;
using Novolis.Audio;
using Novolis.Raylib.Game;
using PulseStrip.Core;
using PulseStrip.Game;

var smoke = args.Any(a => a.Equals("--smoke", StringComparison.OrdinalIgnoreCase));
var contentDir = Path.Combine(AppContext.BaseDirectory, "Content");
var repoContent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Content"));
if (Directory.Exists(repoContent))
    contentDir = repoContent;

if (smoke)
{
    Environment.SetEnvironmentVariable("NOVOLIS_RAYLIB_HEADLESS", "1");
    return PulseStripSmoke.Run(contentDir);
}

IAudioEngine audio = CreateAudio();
using var game = new PulseStripGame(contentDir, audio, smoke: false);
return RayGame.Run("PulseStrip — Anti-Grav Circuit", 1600, 900, game.Initialize, game.Update);

static IAudioEngine CreateAudio()
{
    try
    {
        var engine = new Novolis.Audio.Runtime.MiniaudioAudioEngine();
        if (engine.Start())
            return engine;
        engine.Dispose();
    }
    catch
    {
        // Fall through to null engine.
    }

    return new NullAudioEngine();
}
