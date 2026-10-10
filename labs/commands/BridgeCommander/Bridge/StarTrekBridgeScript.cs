namespace BridgeCommander.Bridge;

/// <summary>Full voiced Star Trek–style bridge shift (multi-character Spectre colors + TTS).</summary>
public static class StarTrekBridgeScript
{
    private static readonly BridgeCharacter C = BridgeCharacterRegistry.Captain;
    private static readonly BridgeCharacter Xo = BridgeCharacterRegistry.ExecutiveOfficer;
    private static readonly BridgeCharacter Helm = BridgeCharacterRegistry.Helm;
    private static readonly BridgeCharacter Tac = BridgeCharacterRegistry.Tactical;
    private static readonly BridgeCharacter Eng = BridgeCharacterRegistry.ChiefEngineer;
    private static readonly BridgeCharacter Sci = BridgeCharacterRegistry.Science;
    private static readonly BridgeCharacter Comms = BridgeCharacterRegistry.Communications;
    private static readonly BridgeCharacter Nav = BridgeCharacterRegistry.Navigator;
    private static readonly BridgeCharacter Cpu = BridgeCharacterRegistry.Computer;

    /// <summary>Red-alert patrol engagement with full crew rotation.</summary>
    public static IReadOnlyList<BridgeExchangeBeat> RedAlertPatrol { get; } = BuildRedAlertPatrol();

    private static List<BridgeExchangeBeat> BuildRedAlertPatrol()
    {
        var beats = new List<BridgeExchangeBeat>();

        void Say(BridgeCharacter who, string line, int pause = 650) =>
            beats.Add(BridgeExchangeBeat.Say(who, line, pause));

        void Order(string captainLine, string transmit, BridgeCharacter station, string ack, int pause = 320)
        {
            beats.AddRange(BridgeExchangeBeat.OrderWithAck(captainLine, transmit, station, ack, pause));
        }

        Say(Cpu,
            "Bridge to all stations. U.S.S. Novolis on patrol, sector seven alpha. All departments report ready.",
            900);

        Say(Cpu, "Incoming transmission. Priority one. Red alert. Red alert.", 700);

        Say(C,
            "Sound general quarters. Battle stations. Mister Saavik, report.",
            800);

        Say(Xo,
            "Captain, long-range sensors detect a Klingon battle cruiser, bearing two seven zero, range four hundred thousand kilometers. She is charging weapons.",
            750);

        Say(Sci,
            "Captain, neutrino emissions confirm a warp signature consistent with D-seven class. Recommend passive scan only until we are ready.",
            650);

        Say(Eng,
            "Engineering reports auxiliary power routed to forward shields. Warp core stable at ninety four percent.",
            600);

        Order(
            "Helm, come right to heading zero nine zero. Warp six, steady as she goes.",
            "helm, set heading to 090",
            Helm,
            "Aye, Captain. Coming to zero nine zero, warp six.");

        Order(
            "Tactical, lock phasers and photon torpedoes. Stand by to fire.",
            "tactical, lock target",
            Tac,
            "Phasers locked. Torpedoes armed. Awaiting your order.");

        Say(Tac,
            "Captain, the cruiser has locked us. Recommend evasive pattern gamma.",
            500);

        Order(
            "Tactical, fire phasers! Full spread!",
            "tactical, fire",
            Tac,
            "Firing phasers now!");

        Order(
            "Helm, execute evasive pattern gamma. Come about, now!",
            "helm, come about",
            Helm,
            "Evasive gamma, coming about!");

        Say(Eng,
            "Captain, minor fluctuations in the port nacelle. Holding for now.",
            450);

        Order(
            "Engineering, divert all auxiliary power to the forward shield grid.",
            "engineering, divert shields",
            Eng,
            "Diverting auxiliary power. Forward shields at maximum.");

        Order(
            "Tactical, torpedoes! Fire!",
            "tactical, fire",
            Tac,
            "Torpedoes away!");

        Say(Cpu,
            "Direct hit on hostile vessel. Target power signatures fading.",
            600);

        Say(Comms,
            "Captain, the Klingon commander is hailing. Demanding our surrender.",
            550);

        Order(
            "Communications, open hail. This is Captain Novolis of the Federation starship Novolis. You are in violation of the Neutral Zone. Withdraw immediately.",
            "comms, hail",
            Comms,
            "Hail open, Captain. Channel secured.");

        Say(Comms,
            "They are breaking off, Captain. Jumping to warp.",
            500);

        Order(
            "Navigation, plot a course to starbase twelve seven for resupply. ETA at maximum warp.",
            "nav, set course waypoint alpha",
            Nav,
            "Course plotted, Captain. ETA six point two hours at warp eight.");

        Order(
            "Helm, reduce speed. Resume patrol heading zero four five.",
            "helm, set heading to 045",
            Helm,
            "Aye, sir. Speed reduced. New heading zero four five.");

        Say(Cpu,
            "Hostile neutralized. Hull integrity ninety two percent. All systems nominal.",
            700);

        Say(C,
            "Well done, all stations. Damage control teams stand down. Resume normal patrol. Captain out.",
            600);

        return beats;
    }
}
