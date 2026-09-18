namespace BridgeCommander.Bridge;



public static class BridgeHelp

{

    public static IReadOnlyList<string> GetLines(string? topic) =>

        topic switch

        {

            null or "" => GeneralHelp(),

            "helm" or "pilot" or "conn" => HelmHelp(),

            "tactical" or "weaps" or "guns" or "tac" => TacticalHelp(),

            "engineering" or "eng" or "damage" => EngineeringHelp(),

            "nav" => NavHelp(),

            "comms" => CommsHelp(),

            "admin" => AdminHelp(),

            "system" or "builtins" => SystemHelp(),

            _ => [$"Unknown topic '{topic}'. Try: help helm | tactical | engineering | nav | comms | admin | system"]

        };



    private static IReadOnlyList<string> GeneralHelp() =>

    [

        "Bridge Commander — every order needs a station prefix (or alias).",

        "Commas after the prefix are fine: helm, set heading to 270",

        "",

        "Prefixes: helm, tactical, engineering, nav, comms, admin",

        "Aliases:  pilot/conn→helm  weaps/guns/tac→tactical  eng/damage→engineering",

        "",

        "── Helm (pilot) ──",

        ..HelmHelp().Skip(1),

        "",

        "── Tactical (weaps) ──",

        ..TacticalHelp().Skip(1),

        "",

        "── Engineering (eng) ──",

        ..EngineeringHelp().Skip(1),

        "",

        "── Nav / Comms / Admin ──",

        ..NavHelp().Skip(1),

        ..CommsHelp().Skip(1),

        ..AdminHelp().Skip(1),

        "",

        "── System (no prefix) ──",

        ..SystemHelp().Skip(1),

        "",

        "help <station>  —  filter this panel"

    ];



    private static IReadOnlyList<string> HelmHelp() =>

    [

        "Helm (prefix: helm or pilot):",

        "helm heading 270              → course 270°",
        "helm course 122 by 33         → sloppy 3D OK",

        "helm set heading to 122 by 180 → 3D: 122° BY 180°",
        "helm set course 123,5 by 119,4 → comma decimals OK",
        "helm heading 122 mark 6 by 180 → 122.6° BY 180°",

        "helm come about               → reverse heading (+180°)",

        "helm all ahead full           → warp 9",

        "helm warp 7                   → warp factor 7",

        "helm full stop                → warp 0"

    ];



    private static IReadOnlyList<string> TacticalHelp() =>

    [

        "Tactical (prefix: tactical or weaps):",

        "tactical lock target              → lock hostile KR-12",

        "weaps target the closest enemy    → same",

        "tactical fire                     → fire if locked (~2.5s)"

    ];



    private static IReadOnlyList<string> EngineeringHelp() =>

    [

        "Engineering (prefix: engineering or eng):",

        "engineering divert shields   → shields +15%",

        "engineering divert weapons   → shields −10%",

        "engineering repair           → hull +8% (~3s)"

    ];



    private static IReadOnlyList<string> NavHelp() =>

    [

        "Nav (prefix: nav):",

        "nav set course alpha centauri   → course laid in",

        "nav course mars                 → same"

    ];



    private static IReadOnlyList<string> CommsHelp() =>

    [

        "Comms (prefix: comms):",

        "comms hail              → open-channel hail"

    ];



    private static IReadOnlyList<string> AdminHelp() =>

    [

        "Admin (prefix: admin):",

        "admin fire              → personnel transfer logged"

    ];



    private static IReadOnlyList<string> SystemHelp() =>

    [

        "System (no station prefix):",

        "help                    → this reference",

        "help tactical           → one station",

        "belay that              → cancel in-flight command",

        "clear queue             → dismiss queued orders",

        "repeat last             → repeat last command"

    ];

}


