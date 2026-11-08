using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Novolis.Agent.Core;

namespace ArtillerySimulator;

/// <summary>
/// Agent adapter: HTTP/MCP → <see cref="ChannelWriter{BattleCommand}"/> → frame pump.
/// Reads state only through <see cref="IPlayQuery"/>. No MediatR; no direct BattleWorld mutation.
/// </summary>
internal sealed class ArtilleryLiveHost : IArtillerySurface
{
    private readonly IPlayQuery _play;
    private readonly ChannelWriter<BattleCommand> _commands;
    private readonly ILogger<ArtilleryLiveHost> _log;
    private readonly object _gate = new();
    private readonly ArtilleryAgentHostState _state = new();
    private readonly ArtilleryAgentActionCatalog _catalog;
    private readonly ArtilleryAgentEntityFactory _entities;
    private readonly ArtilleryAgentSnapshotBuilder _snapshots;
    private readonly AgentCommandReceiptStore _receipts;
    private readonly ArtilleryImpactEventPublisher _impacts;

    public ArtilleryLiveHost(
        IPlayQuery play,
        ChannelWriter<BattleCommand> commands,
        ILogger<ArtilleryLiveHost>? log = null)
    {
        _play = play;
        _commands = commands;
        _log = log ?? NullLogger<ArtilleryLiveHost>.Instance;
        _catalog = new ArtilleryAgentActionCatalog(play);
        _entities = new ArtilleryAgentEntityFactory(play);
        _snapshots = new ArtilleryAgentSnapshotBuilder(play, _state, _catalog, _entities);
        _receipts = new AgentCommandReceiptStore(_state, _snapshots);
        _impacts = new ArtilleryImpactEventPublisher(play, _state, _snapshots);
        _receipts.ActionResult += evt => ActionResult?.Invoke(evt);
    }

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
#pragma warning restore CS0067

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

    public void RunFrame(Action step)
    {
        // Do not hold _gate across step(): BattleCommandPump completes agent TCSs during Drain,
        // and Execute waits on those TCSs then needs _gate for Accept — lock-through-step deadlocks.
        step();

        List<AgentActionResultEvent> impactEvents = [];
        AgentChangedEvent? changed;
        lock (_gate)
        {
            _impacts.CollectImpacts(impactEvents);
            changed = _snapshots.NoteChange();
        }

        foreach (var evt in impactEvents)
            ActionResult?.Invoke(evt);

        if (changed is not null)
            Changed?.Invoke(changed);
    }

    public AgentHello Hello()
    {
        lock (_gate)
        {
            return new AgentHello
            {
                Sequence = ++_state.Sequence,
                ProtocolVersion = "1.0",
                AppId = "artillery-simulator",
                AppTitle = ProductIdentity.Title,
                ProcessId = Environment.ProcessId,
                SurfaceId = "artillery",
                Description = $"{ProductIdentity.Title}: {ProductIdentity.Subtitle}. Level: {_play.Level.DisplayName}. Educational sandbox — {ProductLegal.Tagline}",
                HttpPort = 18770,
                TcpPort = 18771,
                Capabilities =
                [
                    "artillery.aim",
                    "artillery.charge",
                    "artillery.fire",
                    "artillery.pace",
                    "artillery.camera",
                    AgentMethodNames.Watch,
                    AgentMethodNames.WatchNext,
                    AgentMethodNames.Unwatch,
                ],
            };
        }
    }

    public AgentSnapshot Snapshot()
    {
        lock (_gate)
            return _snapshots.Capture();
    }

    public AgentActionsResponse Actions()
    {
        lock (_gate)
            return new AgentActionsResponse { Sequence = ++_state.Sequence, Actions = _catalog.Catalog() };
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(command.CorrelationId)
                && _receipts.TryGetPrior(command.CorrelationId, out var prior))
                return prior;
        }

        if (command.ActionId is "artillery.pace")
        {
            lock (_gate)
                return _receipts.Accept(command, "Realtime only — fixed battle clock.");
        }

        if (!AgentToBattleCommandMapper.TryBuildBattleCommand(command, _receipts.Reject, out var battleCmd, out var reject))
            return reject!;

        var battleDone = battleCmd.Completion
            ?? throw new InvalidOperationException("Battle command missing completion source.");

        if (!_commands.TryWrite(battleCmd))
        {
            if (!_commands.WaitToWriteAsync().AsTask().Wait(TimeSpan.FromSeconds(30))
                || !_commands.TryWrite(battleCmd))
            {
                return new AgentCommandResult
                {
                    Ok = false,
                    ActionId = command.ActionId,
                    CorrelationId = command.CorrelationId,
                    ErrorCode = "timeout",
                    Message = "Battle command channel did not accept the write within 30 s.",
                };
            }
        }

        if (!battleDone.Task.Wait(TimeSpan.FromSeconds(30)))
        {
            return new AgentCommandResult
            {
                Ok = false,
                ActionId = command.ActionId,
                CorrelationId = command.CorrelationId,
                ErrorCode = "timeout",
                Message = "The range did not accept the command within 30 s.",
            };
        }

        var battle = battleDone.Task.Result;
        lock (_gate)
        {
            if (!battle.Succeeded)
                return _receipts.Reject(command, battle.ErrorCode ?? "unavailable", battle.Message);

            if (command.ActionId is "artillery.fire" && battle.FiredRound is { } round)
                _impacts.TrackFire(round, command.CorrelationId);

            var result = _receipts.Accept(command, battle.Message);
            _log.LogDebug(
                "Agent {ActionId} ok={Ok} corr={CorrelationId}",
                command.ActionId,
                result.Ok,
                command.CorrelationId);
            return result;
        }
    }

    public AgentCommandResult Continue() =>
        new() { Ok = true, Message = "The range has no decision gate.", StateRevision = _state.Revision };

    public void Subscribe()
    {
    }
}
