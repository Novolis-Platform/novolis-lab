using Novolis.Agent.Core;

namespace TopDownDoom.Game.Agent;

internal sealed class TopDownDoomAgentHost(TopDownDoomGame game) : ITopDownDoomAgentSurface
{
    private readonly TopDownDoomGame game = game ?? throw new ArgumentNullException(nameof(game));

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;
#pragma warning restore CS0067

    public AgentHello Hello() =>
        new()
        {
            ProtocolVersion = TopDownDoomAgentContract.Definition.ProtocolVersion,
            AppId = "top-down-doom",
            AppTitle = "Top-Down Doom",
            ProcessId = Environment.ProcessId,
            SurfaceId = TopDownDoomAgentContract.Definition.SurfaceId,
            Description = TopDownDoomAgentContract.Definition.Description,
            HttpPort = TopDownDoomAgentContract.Definition.DefaultHttpPort,
            TcpPort = TopDownDoomAgentContract.Definition.DefaultTcpPort,
            Capabilities = TopDownDoomAgentContract.Definition.Methods.ToArray(),
        };

    public AgentSnapshot Snapshot() => game.CreateAgentSnapshot();

    public AgentActionsResponse Actions() =>
        new()
        {
            Actions =
            [
                new AgentAction
                {
                    Id = TopDownDoomAgentActions.Start,
                    Label = "Start game",
                    Summary = "Start the current game session",
                    Enabled = true,
                },
                new AgentAction
                {
                    Id = TopDownDoomAgentActions.CaptureDiagnostics,
                    Label = "Capture diagnostics",
                    Summary = "Read current runtime diagnostics",
                    Enabled = true,
                },
            ],
        };

    public AgentCommandResult Execute(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actionId = command.ActionId?.Trim() ?? string.Empty;
        if (string.Equals(actionId, TopDownDoomAgentActions.Start, StringComparison.OrdinalIgnoreCase))
        {
            game.RequestStart();
            return new AgentCommandResult
            {
                Ok = true,
                ActionId = TopDownDoomAgentActions.Start,
                Message = "Game start requested.",
                Snapshot = Snapshot(),
            };
        }

        if (string.Equals(actionId, TopDownDoomAgentActions.CaptureDiagnostics, StringComparison.OrdinalIgnoreCase))
        {
            return new AgentCommandResult
            {
                Ok = true,
                ActionId = TopDownDoomAgentActions.CaptureDiagnostics,
                Message = "Captured TopDownDoom runtime diagnostics.",
                Snapshot = Snapshot(),
            };
        }

        return new AgentCommandResult
        {
            Ok = false,
            ActionId = actionId,
            ErrorCode = "unknown_action",
            Message = $"Unknown action '{actionId}'.",
        };
    }

    public AgentCommandResult Continue() =>
        new() { Ok = true, ActionId = AgentMethodNames.Continue, Message = "No decision gate.", Snapshot = Snapshot() };

    public void Subscribe()
    {
    }
}
