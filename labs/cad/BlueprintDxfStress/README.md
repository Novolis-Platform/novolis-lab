# BlueprintDxfStress

Imports a **CC0 residential DXF** ([Innerscene three-bedroom bungalow](https://www.innerscene.com/library/three-bedroom-bungalow-plan-drawing-0a096c88))
via `Novolis.Cad.Dxf`, then exports typed HTML / SVG / PDF sheets through `Novolis.Cad.Blueprint`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\BlueprintDxfStress\BlueprintDxfStress.csproj -p:NovolisUseProjectReferences=true
```

| Output | Path |
| --- | --- |
| CAD SoT | `docs/innerscene-bungalow.cadjson` |
| Blueprint JSON | `docs/innerscene-bungalow.cadblueprint.json` |
| HTML GA | `docs/sheets/NBP-DXF-001.html` |
| SVG | `docs/svg/NBP-DXF-001.svg` |
| PDF | `docs/pdf/NBP-DXF-001.pdf` |

Fixture provenance: `fixtures/SOURCE.md`.
