# NearSolPolity

Near-Sol **tycoon slice** — extractive / manufacturing / retail / **8-tramp fleet** / **households** via `Novolis.Economy.Agents`, habitats (`EconomicRegion`), hub order book, thin finance loans.

```powershell
dotnet run --project novolis-lab/labs/economy/NearSolPolity
dotnet run --project novolis-lab/labs/economy/NearSolPolity -- --headless 100d
dotnet run --project novolis-lab/labs/economy/NearSolPolity -- --headless 1000d
```

| Firm | Library agent |
|------|----------------|
| Mining | `ExtractiveFirmAgent` |
| Industry | `ManufacturingFirmAgent` |
| Station | `RetailFirmAgent` + `TreasuryFirmAgent` |
| Carrier + Tramp2…8 | `CarrierFirmAgent` (homes cycle Sol / mines / plants) |
| Sol export | `SolExportHubAgent` (Raw bid + exogenous export) |
| Ventures | `HouseholdTrampVentureAgent` (comfortable HH → hull loan tramp) |
| Cohorts | `HouseholdFirmAgent` (comfort invest into Mining float) |

**Consumption sink:** households spend only on **Final** (Goods). Station shelves at Capital, Inhabited, and Mining camps. Capital parts stay B2B (plant → mine). Wages refill `BudgetRemaining`; retail destroys Final stock — that loop is the intended equilibrium / growth driver.

**Chaos:**
- **Sol Export Hub** — overflow buy below Industry price; **export dump only above soft store-limit**. Volume ≤ hull load; time-gated.
- **Store limits** — soft/hard at Sol (`InventoryStoreLimits`); hard waits unload (no cargo destroy); soft drives ExportBids.
- **Transport capacity** — hull cargo **36**, corridor max **48**, Capital dwell **3h** / **5** berths (volume + time).
- **HH tramp ventures** — agent ready but **pulse-gated off** (entry still freezes haul; follow-up).

SKU story (app only): Raw / Capital / Final / Energy. Travel: 1.3 d/ly. Agents are heuristic + `DeterministicRandom` — not ML.
