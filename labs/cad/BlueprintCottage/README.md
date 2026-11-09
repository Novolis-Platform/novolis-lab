# BlueprintCottage

Complete house CAD fixture for **professional drafting** tests of `Novolis.Cad.Blueprint`
(no Ship / Calypso types). Sheets are typed through **Novolis.Markup.Html** + **Novolis.Markup.Svg**,
with PDF via **Svg.Skia** / SkiaSharp.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\BlueprintCottage\BlueprintCottage.csproj -p:NovolisUseProjectReferences=true
```

Writes under `docs/`:

| File | Content |
| --- | --- |
| `demo-cottage.cadjson` | Full CAD SoT (walls, spaces, openings, shell) |
| `demo-cottage.cadblueprint.json` | Companion blueprint |
| `sheets/NBP-HOUSE-001.html` | A1 ground-floor GA (Markup.Html + embedded SVG) |
| `svg/NBP-HOUSE-001.svg` | Same sheet as standalone Markup.Svg |
| `pdf/NBP-HOUSE-001.pdf` | Same sheet as A1 PDF |
