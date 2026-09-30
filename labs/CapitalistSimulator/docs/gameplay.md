# Capitalist Simulator — first session

Capitalism 2 homage: run a retail store, then expand into factories and markets.

## Run (Avalonia — start here)

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\CapitalistSimulator\CapitalistSimulator.csproj -p:NovolisUseProjectReferences=true -- --mode avalonia
```

You begin with **Corner Market** already built in Metropolis, shelves primed with bread / milk / soda from the seaport.

### What to do in the first five minutes

1. Read the welcome dialog, then press **Advance month** (or the coach button).
2. Watch **Cash** and **Mo P&L** at the top. Right rail shows **P/Q/B** (price / quality / brand) and blue/red supply bars.
3. Follow the **coach card** (left). When it says you're profitable, build a second store or raise prices.
4. **Ops** configures purchasing, ads, recipes. **Reports** lists what sold.

### Map legend

| Color | Meaning |
|-------|---------|
| Dark green | Your firm |
| Dark red | Rival firm |
| Teal strip | Seaport (imports) |
| Brown / purple tiles | Bank / stock exchange |
| Grey band | Road (not buildable) |

### Headless smoke

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\CapitalistSimulator\CapitalistSimulator.csproj -p:NovolisUseProjectReferences=true -- --mode headless --days 36
```

Same starter store; prints a month report + “Next:” coach tip.

## Loop (after the tutorial)

1. Retail from seaport → profit.
2. Second city / second store for share.
3. Factory + recipes for vertical integration (wine scenario: farm grapes → factory wine → your stores).
4. Bank / stock / brand when cash or expansion needs it.

Scenarios via `--scenario`:

| Scenario | Win |
| --- | --- |
| `Sandbox` | No win — open play |
| `RetailProfit` | 2+ retail stores and **$40k trailing-year profit** |
| `WineDominance` | Farm + winery + majority wine market share |

## Saves

`%LocalAppData%\Novolis\CapitalistSimulator\saves\`
