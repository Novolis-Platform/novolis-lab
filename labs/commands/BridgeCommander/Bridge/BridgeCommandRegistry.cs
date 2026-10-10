using Novolis.Commands.Engine;

namespace BridgeCommander.Bridge;

public static class BridgeCommandRegistry
{
    public static ICommandRegistry Create() =>
        new CommandRegistryBuilder()
            .Add("helm.set-heading", "helm",
                ["set heading to", "set heading", "set course", "heading", "course"],
                "heading3d")
            .Add("helm.come-about", "helm", ["come about"])
            .Add("helm.all-ahead-full", "helm", ["all ahead full", "ahead full"])
            .Add("helm.full-stop", "helm", ["full stop", "all stop"])
            .Add("helm.set-speed", "helm", ["set warp", "warp"],
                CommandArgumentDefinition.Integer("warp", required: true))
            .Add("tactical.lock-target", "tactical",
                ["lock target", "target lock", "target the closest enemy", "target closest enemy"])
            .Add("tactical.fire-weapons", "tactical", ["fire weapons", "fire"])
            .Add("engineering.divert-shields", "engineering", ["divert shields", "shields max"])
            .Add("engineering.divert-weapons", "engineering", ["divert weapons"])
            .Add("engineering.repair", "engineering", ["repair", "damage control"])
            .Add("nav.set-course", "nav", ["set course", "course"],
                CommandArgumentDefinition.String("destination", required: true))
            .Add("comms.hail", "comms", ["hail", "open channel"])
            .Add("crew.dismiss-personnel", "admin", ["fire"])
            .Build();
}
