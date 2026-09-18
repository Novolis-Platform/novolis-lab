# Avalonia agent smoke (StudioChromeLab)

1. Build packages + lab + MCP with ProjectRef mode:

```powershell
dotnet build d:\novolis\novolis-avalonia\src\Novolis.Avalonia.Agent\Novolis.Avalonia.Agent.csproj -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-lab\labs\AvaloniaAgentMcp\AvaloniaAgentMcp.csproj -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-lab\labs\avalonia\StudioChromeLab\StudioChromeLab.csproj -p:NovolisUseProjectReferences=true
```

2. Start the lab with the agent host enabled:

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\StudioChromeLab -p:NovolisUseProjectReferences=true
```

3. Ensure Cursor MCP server `avalonia-agent` is enabled (see `.cursor/mcp.json`).

4. From Cursor, call tools in order: `UiHello` → `UiTree` (expect `lab.recovery`) → `UiClick`(`lab.dirty`) → `UiScreenshot`.
