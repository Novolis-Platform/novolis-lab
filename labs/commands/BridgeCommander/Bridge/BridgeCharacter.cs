using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;

namespace BridgeCommander.Bridge;

/// <summary>Bridge crew member: Spectre color and voice archetype/delivery.</summary>
public sealed record BridgeCharacter(
    string Id,
    string DisplayName,
    string SpectreColor,
    VoiceArchetype Archetype,
    bool UseCommsDelivery = true,
    AtcVoiceOptions? Delivery = null)
{
    /// <summary>Markup style for <see cref="Spectre.Console.AnsiConsole.MarkupLine"/> (e.g. <c>yellow1 bold</c>).</summary>
    public string MarkupStyle => $"{SpectreColor} bold";
}
