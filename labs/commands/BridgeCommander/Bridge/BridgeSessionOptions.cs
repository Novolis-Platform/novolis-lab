using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;

namespace BridgeCommander.Bridge;

/// <summary>Options for <see cref="BridgeSession.Create"/>.</summary>
public sealed class BridgeSessionOptions
{
    /// <summary>When true, executed orders are spoken via Novolis voice (Sherpa TTS).</summary>
    public bool VoiceEnabled { get; init; } = true;

    /// <summary>When true, <see cref="BridgeVoiceAnnouncer"/> awaits each line (exchange / interactive).</summary>
    public bool AwaitVoicePlayback { get; init; }

    /// <summary>When true, each bridge character gets a distinct archetype (Star Trek exchange).</summary>
    public bool UseCharacterVoices { get; init; } = true;

    /// <summary>Base voice archetype when <see cref="UseCharacterVoices"/> is false.</summary>
    public VoiceArchetype? VoiceArchetype { get; init; }

    /// <summary>ATC delivery when <see cref="UseCharacterVoices"/> is false.</summary>
    public AtcVoiceOptions? AtcDelivery { get; init; }

    /// <summary>When false, only the base archetype is used (no phraseology or radio DSP).</summary>
    public bool ApplyAtcDelivery { get; init; } = true;

    /// <summary>Default bridge session (voice on, async playback for MCP compatibility).</summary>
    public static BridgeSessionOptions Default { get; } = new();

    /// <summary>Headless / automation session without TTS playback.</summary>
    public static BridgeSessionOptions WithoutVoice { get; } = new() { VoiceEnabled = false };

    /// <summary>Scripted Spectre exchange with blocking per-character voice.</summary>
    public static BridgeSessionOptions ForExchange { get; } = new()
    {
        VoiceEnabled = true,
        AwaitVoicePlayback = true,
        UseCharacterVoices = true,
    };

    /// <summary>Interactive Spectre console with blocking voice.</summary>
    public static BridgeSessionOptions ForInteractive { get; } = new()
    {
        VoiceEnabled = true,
        AwaitVoicePlayback = true,
        UseCharacterVoices = false,
        VoiceArchetype = VoiceArchetypeCatalog.ExcitableFemale,
    };
}
