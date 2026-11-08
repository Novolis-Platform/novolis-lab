using Novolis.Agent.Core;

namespace ArtillerySimulator;

internal sealed class ArtilleryAgentHostState
{
    public const string GunResource = "artillery.gun";

    public long Revision = 1;
    public long Sequence;
    public string Seen = "";
    public AgentReceipt? Receipt;
    public AgentLastAction? Last;
}
