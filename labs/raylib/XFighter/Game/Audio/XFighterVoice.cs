using Novolis.Audio.Voice;
using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;
using Novolis.Audio.Voice.SherpaOnnx;

namespace XFighter.Game.Audio;

/// <summary>Wingman TTS via Novolis.Audio.Voice (ATC radio chain).</summary>
internal sealed class XFighterVoice : IDisposable
{
    private static readonly AtcVoiceOptions CommsDelivery = new()
    {
        Drive = 2.6f,
        OutputGainDb = 4f,
        HissLevel = 0.005f,
    };

    private readonly IVoiceService? _voice;
    private readonly SemaphoreSlim _speakGate = new(1, 1);
    private bool _disposed;

    private XFighterVoice(IVoiceService? voice) => _voice = voice;

    public bool IsAvailable => _voice is not null;

    public static XFighterVoice TryCreate()
    {
        try
        {
            BundledVoiceModelExtractor.EnsureAllExtracted(AppContext.BaseDirectory);
            var builder = VoiceArchetypeApplicator.Apply(
                new VoiceServiceBuilder().UseSherpaOnnx(),
                VoiceArchetypeCatalog.SteadyMale);
            builder.Configure(options =>
            {
                var synthesis = options.Synthesis;
                options.Synthesis = new VoiceSynthesisOptions
                {
                    Profile = synthesis.Profile,
                    ModelProfile = synthesis.ModelProfile,
                    ModelDirectory = synthesis.ModelDirectory,
                    SpeakingRate = VoiceArchetypeCatalog.SteadyMale.SpeakingRate * 1.08f,
                };
            });
            AtcVoiceProfile.ApplyDelivery(builder, CommsDelivery);
            return new XFighterVoice(builder.BuildService());
        }
        catch
        {
            return new XFighterVoice(null);
        }
    }

    public void SpeakComms(string text)
    {
        if (_voice is null || string.IsNullOrWhiteSpace(text) || _disposed)
            return;

        _ = Task.Run(async () =>
        {
            await _speakGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _voice.SpeakAsync(text.Trim()).ConfigureAwait(false);
            }
            catch
            {
                // Silent fallback when models or device are unavailable.
            }
            finally
            {
                _speakGate.Release();
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _speakGate.Dispose();
        if (_voice is IDisposable d)
            d.Dispose();
    }
}
