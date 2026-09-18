using Novolis.Audio.Voice;
using Novolis.Audio.Voice.Design;
using Novolis.Audio.Voice.Platform;
using Novolis.Audio.Voice.Profiles;

namespace Novolis.Lab.Voice;

/// <summary>Kokoro-backed voice archetypes for dogfood apps (use with <see cref="VoiceServiceBuilder.UseKokoro"/>).</summary>
public static class KokoroVoiceArchetypeCatalog
{
    /// <summary>Stressed, professional female; British Isabella at brisk but clear pacing.</summary>
    public static VoiceArchetype ExcitableFemale { get; } = new(
        new VoiceProfile("excitable_female"),
        new VoiceModelProfile("kokoro:bf_isabella"),
        SpeakingRate: 1.21f,
        Description: "Stressed, professional; brisk but clear");

    /// <summary>All Kokoro archetypes shipped in lab.</summary>
    public static IReadOnlyList<VoiceArchetype> All { get; } = [ExcitableFemale];

    /// <summary>Studio-ready draft with Kokoro backend and default effect chain.</summary>
    public static VoicePresetDraft ExcitableFemaleDraft()
    {
        var draft = VoicePresetDraft.FromArchetype(ExcitableFemale);
        draft.Backend = VoiceSynthesizerBackend.KokoroOnnx;
        draft.PropertyName = "ExcitableFemale";
        return draft;
    }
}
