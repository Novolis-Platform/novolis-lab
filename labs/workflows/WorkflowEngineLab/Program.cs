using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.WorkflowEngine;
using Novolis.WorkflowEngine.Channels;
using Novolis.WorkflowEngine.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWorkflowChannelTrigger<RawMessage>();
builder.Services.AddWorkflow("normalize", workflow => workflow
    .TriggeredBy<ChannelWorkflowTrigger<RawMessage>, RawMessage>()
    .Then<NormalizeStep, RawMessage, WorkflowResult>()
    .EndWith<PrintSink, WorkflowResult>());
builder.Services.AddWorkflowHosting();

using var host = builder.Build();
await host.StartAsync();
await host.Services
    .GetRequiredService<ChannelWriter<RawMessage>>()
    .WriteAsync(new RawMessage("workflow engine"));
await host.WaitForShutdownAsync();

public sealed record RawMessage(string Value);

public sealed record WorkflowResult(string Value);

public sealed class NormalizeStep : IWorkflowStep<RawMessage, WorkflowResult>
{
    public ValueTask<WorkflowResult> ExecuteAsync(
        RawMessage input,
        WorkflowContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new WorkflowResult(input.Value.ToUpperInvariant()));
}

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
