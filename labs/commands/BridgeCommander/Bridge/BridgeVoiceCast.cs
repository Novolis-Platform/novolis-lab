using Novolis.Audio.Voice;
using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;
using Novolis.Audio.Voice.SherpaOnnx;

namespace BridgeCommander.Bridge;

/// <summary>One TTS voice per bridge character (archetype + optional comms delivery).</summary>
public sealed class BridgeVoiceCast : IAsyncDisposable
{
    private readonly Dictionary<string, IVoiceService> _byCharacterId = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _enabled;

    private BridgeVoiceCast(bool enabled) => _enabled = enabled;

    /// <summary>Builds voices for every member of <see cref="BridgeCharacterRegistry"/>.</summary>
    public static BridgeVoiceCast Create(bool enabled)
    {
        if (!enabled)
            return new BridgeVoiceCast(false);

        BundledVoiceModelExtractor.EnsureAllExtracted(AppContext.BaseDirectory);
        var cast = new BridgeVoiceCast(true);

        foreach (var character in BridgeCharacterRegistry.All)
            cast._byCharacterId[character.Id] = cast.CreateVoice(character);

        return cast;
    }

    /// <summary>Speaks a line in the character's voice.</summary>
    public async Task SpeakAsync(
        BridgeCharacter character,
        string? text,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(text))
            return;

        if (!_byCharacterId.TryGetValue(character.Id, out var voice) || voice is null)
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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        foreach (var voice in _byCharacterId.Values)
        {
            if (voice is IAsyncDisposable asyncDisposable)
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else if (voice is IDisposable disposable)
                disposable.Dispose();
        }

        _byCharacterId.Clear();
        return ValueTask.CompletedTask;
    }

    private IVoiceService CreateVoice(BridgeCharacter character)
    {
        var builder = VoiceArchetypeApplicator.Apply(new VoiceServiceBuilder().UseSherpaOnnx(), character.Archetype);
        // Bridge comms: brisk delivery on top of archetype pacing.
        var speakingRate = character.Archetype.SpeakingRate * 1.12f;
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
        if (character.UseCommsDelivery)
            AtcVoiceProfile.ApplyDelivery(builder, character.Delivery ?? BridgeVoice.UrgentAtcDelivery);
        return builder.BuildService();
    }
}
