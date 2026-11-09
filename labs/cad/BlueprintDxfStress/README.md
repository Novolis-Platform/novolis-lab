# BlueprintDxfStress

Imports a **real residential DXF** ([jscad/sample-files](https://github.com/jscad/sample-files) Bishop-Overland
floor plan, ~1.1 MB / ~967 entities) via `Novolis.Cad.Dxf`, then exports typed
HTML / SVG / PDF sheets through `Novolis.Cad.Blueprint`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\BlueprintDxfStress\BlueprintDxfStress.csproj -p:NovolisUseProjectReferences=true
```

| Output | Path |
| --- | --- |
| CAD SoT | `docs/bishop-overland.cadjson` |
| Blueprint JSON | `docs/bishop-overland.cadblueprint.json` |
| HTML GA | `docs/sheets/NBP-DXF-001.html` |
| SVG | `docs/svg/NBP-DXF-001.svg` |
| PDF | `docs/pdf/NBP-DXF-001.pdf` |

Fixture provenance: `fixtures/SOURCE.md`.
