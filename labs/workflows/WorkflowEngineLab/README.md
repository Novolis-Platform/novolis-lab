# WorkflowEngineLab

Small host-started workflow that passes a value through a start step, a
transform step, and an end step:

```text
ReadStep → NormalizeStep → PrintStep
```

Run it from the workspace with:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workflows\WorkflowEngineLab\WorkflowEngineLab.csproj -p:NovolisUseProjectReferences=true
```

The sample prints `WORKFLOW ENGINE` and exits after the end step receives the
result.
