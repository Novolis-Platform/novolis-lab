# MauiAgentMcp

stdio MCP sidecar for Novolis MAUI UI automation. Dogfoods **Novolis.Maui.Agent.Protocol** and **Novolis.Transports.LocalIpc** — the MAUI counterpart of [AvaloniaAgentMcp](../AvaloniaAgentMcp/README.md).

Connects to a running MAUI app with the agent host enabled (e.g. **Novolis PDF Reader** with `AgentHost.Attach`).

## Run

```powershell
dotnet build d:\novolis\novolis-lab\labs\commands\MauiAgentMcp\MauiAgentMcp.csproj -p:NovolisUseProjectReferences=true
dotnet exec d:\novolis\novolis-lab\artifacts\bin\MauiAgentMcp\debug\MauiAgentMcp.dll --mcp
```

Default (no args) also starts MCP stdio mode.

## MCP tools

`UiHosts`, `UiConnect`, `UiReconnect`, `UiHello`, `UiGet`, `UiItems`, `UiPoll`, `UiTree`, `UiScreenshot`, `UiClick`, `UiType`, `UiSelect`, `UiFocus`, `UiScroll`, `UiWait`.

Register in Cursor as `maui-agent` (see repo `.cursor/mcp.json`). CLI fallback:

```powershell
dotnet run --project d:\novolis\novolis-maui\tools\MauiAgentDump\MauiAgentDump.csproj --no-launch-profile -- tree
dotnet run --project d:\novolis\novolis-maui\tools\MauiAgentDump\MauiAgentDump.csproj --no-launch-profile -- screenshot PdfReadingPage
```
