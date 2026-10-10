# Capitalist Simulator

Experimental lab host in `novolis-lab`.

Avalonia + headless **Capitalism 2 homage**. You start with a stocked supermarket and a coach that tells you the next move.

This host remains a lab artifact while the product scope and release contract
are being explored.

## First run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\apps\CapitalistSimulator\CapitalistSimulator.csproj -p:NovolisUseProjectReferences=true -- --mode avalonia
```

1. Welcome dialog explains the starter store.
2. Press **Advance month**.
3. Follow the coach card on the left.

Headless check:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\apps\CapitalistSimulator\CapitalistSimulator.csproj -p:NovolisUseProjectReferences=true -- --mode headless --days 36
```

## Packages

| Package | Role |
|---------|------|
| Avalonia / Desktop / Fluent / DataGrid | UI surface |
| Novolis.Avalonia.Studio | Status / flash / busy |
| Novolis.Avalonia.Briefing | News feed |
| Novolis.Avalonia.Controls | Shared controls |
| Novolis.Storage.Json | Save files |
| Spectre.Console | Headless report |

## Docs

- [docs/gameplay.md](docs/gameplay.md) — first session walkthrough
