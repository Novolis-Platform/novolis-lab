using Novolis.WorkflowEngine;

public sealed class NormalizeStep : IWorkflowStep<RawMessage, WorkflowResult>
{
    public ValueTask<WorkflowResult> ExecuteAsync(
        RawMessage input,
        WorkflowContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new WorkflowResult(input.Value.ToUpperInvariant()));
}
