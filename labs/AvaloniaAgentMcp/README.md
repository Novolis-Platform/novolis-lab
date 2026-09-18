# AvaloniaAgentMcp

stdio MCP sidecar for Novolis Avalonia UI automation. Dogfoods **Novolis.Avalonia.Agent.Protocol**, **Novolis.Transports.LocalIpc**, **Novolis.Agent.Core**, and **Novolis.Agent.Surface**.

Connects to a running Avalonia app with the agent host enabled (e.g. **StudioChromeLab** with `NOVOLIS_AVALONIA_AGENT=1`).

## Run

```powershell
cd novolis-lab
dotnet build labs/AvaloniaAgentMcp -c Release
dotnet exec labs/AvaloniaAgentMcp/bin/Release/net10.0/AvaloniaAgentMcp.dll --mcp
```

Default (no args) also starts MCP stdio mode. Any other args print usage and exit `1`.

## MCP tools (summary)

- **Session / UI:** `UiHosts`, `UiConnect`, `UiReconnect`, `UiHello`, `UiTree`, `UiClick`, `UiType`, `UiWait`, `UiScreenshot`, …
- **Scene (3D apps):** scene snapshot, pick, transform helpers on `SceneAgentMcpTools`

Register in Cursor as `avalonia-agent` (see repo `.cursor/mcp.json`). Full agent smoke flow: `labs/avalonia/StudioChromeLab/AGENT-SMOKE.md`.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
