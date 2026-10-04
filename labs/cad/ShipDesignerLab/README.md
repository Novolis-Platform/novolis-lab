# Ship Designer Lab

Package integration host for the object-first ship design surface. This lab is
not a release product; the shipped host is Novolis CAD Studio's explicit Ship
mode.

It exercises:

- `.shipjson` authoring with PLAN / MODEL / ANALYZE workspaces;
- ship validation, airtight projection, object grips, and analysis;
- `.cadjson` import/export as a bridge;
- Calypso seed import;
- `.nov3djson` scene evaluation.

Run the UI:

```powershell
dotnet run --project labs/cad/ShipDesignerLab/ShipDesignerLab.csproj -p:NovolisUseProjectReferences=true
```

Run the headless smoke:

```powershell
dotnet run --project labs/cad/ShipDesignerLab/ShipDesignerLab.csproj -p:NovolisUseProjectReferences=true -- --smoke
```

The lab data root is `%LOCALAPPDATA%\Novolis\ShipDesignerLab`.
