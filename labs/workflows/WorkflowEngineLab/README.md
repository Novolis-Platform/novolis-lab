# WorkflowEngineLab

Small channel-triggered workflow that passes a value through a typed
transformation and terminal sink:

```text
ChannelWorkflowTrigger → NormalizeStep → PrintSink
```

Run it from the workspace with:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workflows\WorkflowEngineLab\WorkflowEngineLab.csproj -p:NovolisUseProjectReferences=true
```

The sample prints `WORKFLOW ENGINE` and exits after the sink receives the
result.
