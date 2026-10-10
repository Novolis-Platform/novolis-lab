using Novolis.Audio.Voice;
using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;
using Novolis.Audio.Voice.SherpaOnnx;

namespace BridgeCommander.Bridge;

/// <summary>ATC TTS for bridge lines (GPR <c>Novolis.Audio.Voice*</c> packages).</summary>
public static class BridgeVoice
{
    /// <summary>Default ATC delivery (radio DSP); base voice comes from <see cref="VoiceArchetypeCatalog.ExcitableFemale"/>.</summary>
    public static AtcVoiceOptions UrgentAtcDelivery { get; } = new()
    {
        Drive = 3f,
        OutputGainDb = 5.5f,
        HissLevel = 0.0045f,
    };

    /// <summary>Creates a voiced service (archetype + optional ATC delivery), or null when disabled.</summary>
    public static IVoiceService? CreateService(
        bool enabled,
        VoiceArchetype? archetype = null,
        AtcVoiceOptions? delivery = null,
        bool applyAtcDelivery = true)
    {
        if (!enabled)
            return null;

        BundledVoiceModelExtractor.EnsureAllExtracted(AppContext.BaseDirectory);

        var selected = archetype ?? VoiceArchetypeCatalog.ExcitableFemale;
        var builder = VoiceArchetypeApplicator.Apply(new VoiceServiceBuilder().UseSherpaOnnx(), selected);
        var speakingRate = selected.SpeakingRate * 1.12f;
        builder.Configure(options =>
        {
            var synthesis = options.Synthesis;
            options.Synthesis = new VoiceSynthesisOptions
            {
                Profile = synthesis.Profile,
                ModelProfile = synthesis.ModelProfile,
                ModelDirectory = synthesis.ModelDirectory,
                SpeakingRate = speakingRate,
            };
        });

        if (applyAtcDelivery)
            AtcVoiceProfile.ApplyDelivery(builder, delivery ?? UrgentAtcDelivery);

        return builder.BuildService();
    }

    /// <summary>Speaks narrative or station text (not status-prefixed).</summary>
    public static async Task SpeakLineAsync(
        IVoiceService? voice,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (voice is null || string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            await voice.SpeakAsync(text.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine(
                $"[red][[bridge-voice]][/] {Spectre.Console.Markup.Escape(ex.Message)}");
        }
    }
}
