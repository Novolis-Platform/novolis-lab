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
