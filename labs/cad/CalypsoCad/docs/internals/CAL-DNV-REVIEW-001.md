# CAL-DNV-REVIEW-001 — design review package

Classification-style **third-party design review** bundle for the Calypso tramp freighter. This is **not** DNV plan approval, rule calculation, or certification.

## Authority chain

1. `CAL-INT-GA-001.json` — compartments, hatches, frames, outfit schedule  
2. `CAL-INT-DK-001.html` — deck plans / `planRing` footprints  
3. `CAL-HULL-CAD-001.json` — manufacturer OML / IML  
4. `calypso.cadjson` — generated 3D assembly  

## Generate

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --dnv-review --headless
```

Outputs under `%LocalAppData%\Novolis\CalypsoCad\generated\`:

- `CAL-DNV-REVIEW-001.html` — principal particulars, compartment schedule, opening register, layer discipline, verification tables, visual annex  
- `calypso-dnv-review.json` — machine-readable manifest for CI  
- `exports\layer-cutaway-*.png` — structure vs fitout evidence  

Acceptance (`--acceptance`) includes `DNV review: …` gates.
