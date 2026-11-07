using Novolis.Audio.Voice;
using Novolis.Audio.Voice.Profiles;
using Novolis.Audio.Voice.SherpaOnnx;
using Novolis.Lab.Voice;

namespace ArtillerySimulator;

/// <summary>Battery radio TTS via Novolis.Audio.Voice (ATC radio chain).</summary>
internal sealed class ArtilleryVoice : IDisposable
{
    private static readonly AtcVoiceOptions CommsDelivery = new()
    {
        Drive = 2.4f,
        OutputGainDb = 3.5f,
        HissLevel = 0.008f,
    };

    private readonly IVoiceService? _voice;
    private readonly SemaphoreSlim _speakGate = new(1, 1);
    private bool _disposed;

    private ArtilleryVoice(IVoiceService? voice) => _voice = voice;

    public bool IsAvailable => _voice is not null;

    public static ArtilleryVoice TryCreate()
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
                    SpeakingRate = VoiceArchetypeCatalog.SteadyMale.SpeakingRate * 1.05f,
                };
            });
            AtcVoiceProfile.ApplyDelivery(builder, CommsDelivery);
            return new ArtilleryVoice(builder.BuildService());
        }
        catch
        {
            return new ArtilleryVoice(null);
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
                // Models / device unavailable — silent.
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
