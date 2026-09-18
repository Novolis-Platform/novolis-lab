# Bridge Commander

Sci-fi bridge captain console lab [Novolis.Commands](https://github.com/Novolis-Platform/novolis-commands) and **Novolis.Audio.Voice** (per-character archetypes + ATC comms delivery).

Built with **[Spectre.Console](https://spectreconsole.net)** — each station has its own markup color and TTS voice.

## Run

**Default — Star Trek red-alert exchange** (full crew, colored dialogue, per-character Piper voices):

```bash
dotnet run --project labs/BridgeCommander
```

**Legacy shorter patrol script:**

```bash
dotnet run --project labs/BridgeCommander -- --patrol
```

**Interactive manual orders** (single voice, Spectre panels):

```bash
dotnet run --project labs/BridgeCommander -- --interactive
```

**Silent** (Spectre only, no TTS):

```bash
dotnet run --project labs/BridgeCommander -- --no-voice
```

### Local monorepo (before `Novolis.Audio.Voice.Profiles` is on GPR)

When `novolis-audio` is a sibling folder (`d:\novolis\novolis-audio`), BridgeCommander automatically uses **project references** instead of NuGet for voice packages.

Extract all three Piper models on first run (or build once with models present):

```powershell
# from novolis-audio
pwsh -File scripts/pack-all-voice-model-archives.ps1
dotnet build labs/BridgeCommander -c Release
```

## Crew (Spectre color + voice archetype)

| Station | Color | Archetype |
|---------|-------|-----------|
| Captain | yellow | steady_male |
| Executive Officer | green | calm_female |
| Helm | cyan | procedural_male |
| Tactical | red | steady_male |
| Chief Engineer | orange | procedural_male |
| Science Officer | blue | neutral_female |
| Communications | magenta | excitable_female |
| Navigator | purple | calm_female |
| Computer | grey | neutral_female (dry, no radio) |

## MCP (agent / QA automation)

Stdio MCP server for Cursor and other MCP clients:

```bash
dotnet build -c Release
dotnet exec bin/Release/net10.0/BridgeCommander.dll --mcp
```

**Cursor:** workspace `.cursor/mcp.json` registers `bridge-commander`. MCP/QA modes disable voice automatically.

| Tool | Description |
|------|-------------|
| `GetBridgeSnapshot` | Ship state + command log |
| `TransmitOrder` | Submit an order (waits for queue idle by default) |
| `ResetBridge` | Reset to duty-shift defaults |
| `RunQaScenario` | Run built-in QA script |
| `ListQaScenarios` | List scenario names |

## Test suites

```bash
dotnet run -- --qa-smoke      # 5 end-to-end scenarios
dotnet run -- --mcp-test      # 34 MCP tool assertions
dotnet run -- --transmit "helm heading 270"   # one-off JSON result
pwsh scripts/test-mcp-stdio.ps1               # stdio protocol smoke
```
