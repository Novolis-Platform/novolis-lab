# CalypsoInternalsCad

Dogfood app that turns the Calypso **internals drawing pack** into Novolis CAD companions and Wavefront 3D meshes.

| Input | Role |
| --- | --- |
| `CalypsoCad/docs/internals/CAL-INT-GA-001.json` | Lock / fabrication SoT (decks, compartments, hatches, airlocks) |
| `CalypsoCad/docs/manufacturer/CAL-HULL-CAD-001.json` | Outer hull OML/IML mesh |

| Output (`%LocalAppData%\Novolis\CalypsoInternalsCad\generated\`) | Role |
| --- | --- |
| `calypso.cadjson` (+ layers/shapes) | Novolis CAD (ShipDesigner-import compatible names) |
| `calypso-internals.cadjson` (+ sidecars) | Same document under this app’s stable names |
| `calypso-internals.obj` / `.mtl` | Tessellated 3D (hull + spaces + walls) |
| `manifest.json` | Counts + file list |

Lock → CAD generation is shared source with `CalypsoCad` (`CalypsoLockGenerator`). This app owns the export/view path for the internals pack.

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoInternalsCad\CalypsoInternalsCad.csproj -p:NovolisUseProjectReferences=true
```

Generate + orbit view:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoInternalsCad\CalypsoInternalsCad.csproj -p:NovolisUseProjectReferences=true -- --view
```

Custom output directory:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoInternalsCad\CalypsoInternalsCad.csproj -p:NovolisUseProjectReferences=true -- --out d:\novolis\artifacts\calypso-internals
```

HTML deck sheets stay under CalypsoCad (`--blueprints`). This app is CAD + 3D only.
