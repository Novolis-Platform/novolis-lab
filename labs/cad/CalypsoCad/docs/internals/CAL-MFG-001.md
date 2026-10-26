# CAL-MFG-001 — automated fabrication release

Full-vessel manufacturing bundle for orbital/ground automated plate cutting and install sequencing.

## Generate

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --fab-release
```

Outputs under `%LocalAppData%\Novolis\CalypsoCad\generated\CAL-MFG-001\`:

- `calypso-fab-manifest.json` — parts, cuts, nests, welds, assembly sequence
- `bom.json` — plate BOM lines
- `cuts/hull/*.dxf` — OML facet blanks (from `CAL-HULL-CAD-001.json`)
- `cuts/structure/*.dxf` — frame and girder blanks
- `cuts/partition/*.dxf` — wall panels
- `nests/sheet-*.dxf` — 2×6 m stock nests
- `welds/hull-seams.json` — shared-edge weld registry
- `CAL-MFG-001.html` — audit summary

Verify:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-fab-release.cs
```

Acceptance (`--acceptance`) includes `fab release: …` gates after emitting CAL-MFG-001.

## Authority

1. `CAL-HULL-CAD-001.json` — hull facet geometry and material process
2. `CAL-INT-GA-001.json` — compartments, structure schedule, outfit
3. `calypso.cadjson` — generated assembly instances

STEP AP242 and GD&T remain a later revision.
