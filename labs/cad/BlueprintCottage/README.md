# BlueprintCottage

Complete house CAD fixture for **professional drafting** tests of `Novolis.Cad.Blueprint`
(no Ship / Calypso types).

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\BlueprintCottage\BlueprintCottage.csproj -p:NovolisUseProjectReferences=true
```

Writes under `docs/`:

| File | Content |
| --- | --- |
| `demo-cottage.cadjson` | Full CAD SoT (walls, spaces, openings, shell) |
| `demo-cottage.cadblueprint.json` | Companion blueprint |
| `sheets/NBP-HOUSE-001.html` | A1 ground-floor GA (SVG sheet-first) |
