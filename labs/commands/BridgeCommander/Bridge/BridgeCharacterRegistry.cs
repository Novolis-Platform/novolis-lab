using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;

namespace BridgeCommander.Bridge;

/// <summary>Star Trek–style bridge crew with per-role Spectre colors and voice mapping.</summary>
public static class BridgeCharacterRegistry
{
    public static BridgeCharacter Captain { get; } = new(
        "captain",
        "Captain",
        "yellow1",
        VoiceArchetypeCatalog.SteadyMale,
        Delivery: new AtcVoiceOptions { Drive = 2.9f, OutputGainDb = 5f });

    public static BridgeCharacter ExecutiveOfficer { get; } = new(
        "xo",
        "Executive Officer",
        "green1",
        VoiceArchetypeCatalog.CalmFemale);

    public static BridgeCharacter Helm { get; } = new(
        "helm",
        "Helm",
        "cyan1",
        VoiceArchetypeCatalog.ProceduralMale,
        Delivery: new AtcVoiceOptions { Drive = 2.6f, OutputGainDb = 4.5f });

    public static BridgeCharacter Tactical { get; } = new(
        "tactical",
        "Tactical",
        "red1",
        VoiceArchetypeCatalog.SteadyMale,
        Delivery: new AtcVoiceOptions { Drive = 3.1f, OutputGainDb = 5.5f });

    public static BridgeCharacter ChiefEngineer { get; } = new(
        "engineering",
        "Chief Engineer",
        "orange1",
        VoiceArchetypeCatalog.ProceduralMale,
        Delivery: new AtcVoiceOptions { Drive = 2.7f, HissLevel = 0.005f });

    public static BridgeCharacter Science { get; } = new(
        "science",
        "Science Officer",
        "deepskyblue1",
        VoiceArchetypeCatalog.NeutralFemale);

    public static BridgeCharacter Communications { get; } = new(
        "comms",
        "Communications",
        "magenta1",
        VoiceArchetypeCatalog.ExcitableFemale);

    public static BridgeCharacter Navigator { get; } = new(
        "nav",
        "Navigator",
        "purple",
        VoiceArchetypeCatalog.CalmFemale);

    public static BridgeCharacter Computer { get; } = new(
        "computer",
        "Computer",
        "grey",
        VoiceArchetypeCatalog.NeutralFemale,
        UseCommsDelivery: false);

    public static BridgeCharacter Ensign { get; } = new(
        "ensign",
        "Ensign",
        "white",
        VoiceArchetypeCatalog.SteadyMale,
        Delivery: new AtcVoiceOptions { Drive = 2.5f, OutputGainDb = 4f });

    public static IReadOnlyList<BridgeCharacter> All { get; } =
    [
        Captain,
        ExecutiveOfficer,
        Helm,
        Tactical,
        ChiefEngineer,
        Science,
        Communications,
        Navigator,
        Computer,
        Ensign,
    ];

    public static BridgeCharacter Resolve(string speaker)
    {
        foreach (var character in All)
        {
            if (string.Equals(character.DisplayName, speaker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(character.Id, speaker, StringComparison.OrdinalIgnoreCase))
                return character;
        }

        return Ensign;
    }
}
