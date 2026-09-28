# ScarcityRationingLab

This Core-only lab demonstrates quantity rationing at a fixed posted price:

```text
units sold = min(affordable demand, available supply)
```

Both scenarios post the same `$10` price and give the buyer `$1,000`:

- abundant supply: `100` units available, `100` units sold;
- scarce supply: `3` units available, `3` units sold, `97` unmet demand.

The lab invokes `TransferOwnershipPaymentsStep` directly. It checks that cash
and goods are conserved, inventory never becomes negative, and the Core
invariants remain valid. Its output also shows the economic event itself:
the buyer's spending, the seller's revenue, the quantity transferred, and
the value of demand that could not be fulfilled at the posted price. It does
not model endogenous price discovery or claim that `$10` is an equilibrium
price.

Run it from PowerShell:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\economy\ScarcityRationingLab\ScarcityRationingLab.csproj
```
