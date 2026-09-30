using Microsoft.Extensions.Hosting;
using Novolis.WorkflowEngine;

public sealed class PrintSink(IHostApplicationLifetime lifetime) : IWorkflowSink<WorkflowResult>
{
    public ValueTask HandleAsync(
        WorkflowResult payload,
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine(payload.Value);
        lifetime.StopApplication();
        return ValueTask.CompletedTask;
    }
}
