# CAL-MFG-002 — automated fabrication release (authority)

CAL-MFG-002 is the **fab authority** for Calypso automated plate cutting and orbital/ground fab cells. It supersedes [CAL-MFG-001](CAL-MFG-001.md).

## In scope

- Profile-based **DXF** for transverse frame webs (octagon outer + full-height passage holes)
- Hull OML unfold with **developability** edge-closure QA
- **Kerf / part-gap** aware nesting from `CAL-HULL-CAD-001.json` process fields
- Material-aware **BOM** (316L structure/hull, aluminum outfit install kit)
- Hull **weld seam** traceability plus `welds/hull-program.json` (ordered steps with seam midpoints)
- Hard **acceptance** and governance gates (`verify-calypso-fab-release.cs`, `verify-calypso-visual-gate.cs`)

## Deltas from CAL-MFG-001

| Area | 001 | 002 |
| --- | --- | --- |
| Frame webs | Bar-slice rectangles | `FrameWebPlateBuilder` + `FabCutProfile` holes |
| Fab QA | Count thresholds | Corridor width, IML solids, developability, mass reconcile |
| Package id | CAL-MFG-001 | **CAL-MFG-002** |
| Visual CI | None | PNG hash gate vs `docs/preview/` |

## Out of scope — CAL-MFG-003

STEP AP242, GD&T, fastener torque sheets, and certification claims remain **CAL-MFG-003** (documented only).

## Regenerate

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --fab-release
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-fab-release.cs
```
