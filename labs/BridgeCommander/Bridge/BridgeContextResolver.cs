using Novolis.Commands.Engine;

namespace BridgeCommander.Bridge;

public sealed class BridgeContextResolver : ICommandContextResolver<BridgeState>
{
    private static readonly Dictionary<string, string> ContextAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pilot"] = "helm",
            ["conn"] = "helm",
            ["weaps"] = "tactical",
            ["guns"] = "tactical",
            ["tac"] = "tactical",
            ["eng"] = "engineering",
            ["damage"] = "engineering"
        };

    public IReadOnlyDictionary<string, string> GetContextAliases(BridgeState context) => ContextAliases;

    public IReadOnlyDictionary<string, string> GetVerbAliases(BridgeState context) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
