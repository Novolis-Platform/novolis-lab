using Novolis.Agent.Core;

namespace ArtillerySimulator;

internal sealed class AgentCommandReceiptStore(
    ArtilleryAgentHostState state,
    ArtilleryAgentSnapshotBuilder snapshots)
{
    private const int MaxCompleted = 256;

    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private readonly Queue<string> _completedOrder = new();

    public event Action<AgentActionResultEvent>? ActionResult;

    public bool TryGetPrior(string correlationId, out AgentCommandResult prior) =>
        _completed.TryGetValue(correlationId, out prior!);

    public AgentCommandResult Accept(AgentCommand command, string message)
    {
        state.Revision++;
        state.Receipt = AgentCommandGate.CreateReceipt(command, AgentLifecycle.Applied, true, message, state.Revision);
        state.Last = new AgentLastAction { ActionId = command.ActionId, Ok = true, Message = message };
        var snapshot = snapshots.Capture();
        var result = new AgentCommandResult
        {
            Sequence = ++state.Sequence,
            Ok = true,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            Message = message,
            StateRevision = state.Revision,
            Receipt = state.Receipt,
            Snapshot = snapshot,
        };
        Remember(command.CorrelationId, result);
        ActionResult?.Invoke(new AgentActionResultEvent
        {
            Sequence = state.Sequence,
            ActionId = command.ActionId,
            Ok = true,
            Message = message,
            CorrelationId = command.CorrelationId,
            ResourceId = ArtilleryAgentHostState.GunResource,
            Severity = AgentSeverity.Info,
            Receipt = state.Receipt,
            Snapshot = snapshot,
        });
        return result;
    }

    public AgentCommandResult Reject(AgentCommand command, string reason, string message)
    {
        var receipt = AgentCommandGate.CreateReceipt(command, AgentLifecycle.Rejected, false, message, state.Revision, reason, message);
        return new AgentCommandResult
        {
            Sequence = ++state.Sequence,
            Ok = false,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            ErrorCode = reason,
            Message = message,
            StateRevision = state.Revision,
            Receipt = receipt,
        };
    }

    private void Remember(string? correlationId, AgentCommandResult result)
    {
        if (string.IsNullOrEmpty(correlationId) || !result.Ok)
            return;

        _completed[correlationId] = result;
        _completedOrder.Enqueue(correlationId);
        while (_completedOrder.Count > MaxCompleted)
        {
            var old = _completedOrder.Dequeue();
            _completed.Remove(old);
        }
    }
}
