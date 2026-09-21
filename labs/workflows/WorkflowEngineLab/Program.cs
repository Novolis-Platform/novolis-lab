using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Novolis.WorkflowEngine;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkflow(workflow =>
{
    workflow
        .StartWith<ReadStep, RawMessage>()
        .Then<NormalizeStep, RawMessage, WorkflowResult>()
        .ThenEndWith<PrintStep, WorkflowResult>();
});

await builder.Build().RunAsync();

public sealed record RawMessage(string Value);

public sealed record WorkflowResult(string Value);

public sealed class ReadStep(ChannelWriter<RawMessage> writer) : IStartStep<RawMessage>
{
    public Task RunAsync(CancellationToken cancellationToken) =>
        writer.WriteAsync(new RawMessage("workflow engine"), cancellationToken).AsTask();
}

public sealed class NormalizeStep : IStep<RawMessage, WorkflowResult>
{
    public Task<WorkflowResult> ExecuteAsync(RawMessage input) =>
        Task.FromResult(new WorkflowResult(input.Value.ToUpperInvariant()));
}

public sealed class PrintStep(IHostApplicationLifetime lifetime) : IEndStep<WorkflowResult>
{
    public Task ExecuteAsync(WorkflowResult result)
    {
        Console.WriteLine(result.Value);
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
