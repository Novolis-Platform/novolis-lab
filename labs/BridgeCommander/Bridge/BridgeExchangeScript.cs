namespace BridgeCommander.Bridge;

/// <summary>Legacy shorter patrol script (subset of crew).</summary>
public static class BridgeExchangeScript
{
    /// <summary>Original voiced patrol engagement sequence.</summary>
    public static IReadOnlyList<BridgeExchangeBeat> PatrolEngagement { get; } =
    [
        BridgeExchangeBeat.Say(
            BridgeCharacterRegistry.Computer,
            "Bridge to all stations. USS Novolis on patrol, sector seven alpha. All departments report ready.",
            pauseMs: 900),

        BridgeExchangeBeat.Say(
            BridgeCharacterRegistry.ExecutiveOfficer,
            "Captain, long-range sensors show a hostile frigate on bearing two seven zero. Recommend weapons tight and shields up.",
            pauseMs: 700),

        .. BridgeExchangeBeat.OrderWithAck(
            "Helm, come right to heading zero nine zero, steady as she goes.",
            "helm, set heading to 090",
            BridgeCharacterRegistry.Helm,
            "Aye, Captain. Heading zero nine zero."),

        .. BridgeExchangeBeat.OrderWithAck(
            "Tactical, lock the hostile and stand by phasers.",
            "tactical, lock target",
            BridgeCharacterRegistry.Tactical,
            "Target locked, standing by."),

        .. BridgeExchangeBeat.OrderWithAck(
            "Engineering, divert auxiliary power to the forward shield grid.",
            "engineering, divert shields",
            BridgeCharacterRegistry.ChiefEngineer,
            "Auxiliary power to forward shields, aye."),

        .. BridgeExchangeBeat.OrderWithAck(
            "Tactical, fire when ready!",
            "tactical, fire",
            BridgeCharacterRegistry.Tactical,
            "Firing now, sir!"),

        .. BridgeExchangeBeat.OrderWithAck(
            "Helm, all ahead full. Let's close the distance.",
            "helm, all ahead full",
            BridgeCharacterRegistry.Helm,
            "All ahead full, aye."),

        .. BridgeExchangeBeat.OrderWithAck(
            "Comms, open hail on priority channel. Identify us as Federation patrol.",
            "comms, hail",
            BridgeCharacterRegistry.Communications,
            "Hail open on priority channel."),

        .. BridgeExchangeBeat.OrderWithAck(
            "Navigation, plot jump solution to waypoint alpha for after action.",
            "nav, set course waypoint alpha",
            BridgeCharacterRegistry.Navigator,
            "Course laid in to waypoint alpha."),

        .. BridgeExchangeBeat.OrderWithAck(
            "Helm, come about to face the debris field. Prepare for recovery ops.",
            "helm, come about",
            BridgeCharacterRegistry.Helm,
            "Coming about, sir."),

        BridgeExchangeBeat.Say(
            BridgeCharacterRegistry.Computer,
            "Hostile neutralized. Hull integrity ninety two percent. Standing by for further orders.",
            pauseMs: 800),

        BridgeExchangeBeat.Say(
            BridgeCharacterRegistry.Captain,
            "Well done, all stations. Log the engagement and resume patrol. Bridge out.",
            pauseMs: 500),
    ];
}
