# FreightWing

X-Wing Alliance–inspired dual-role campaign: fly the family freighter, transfer to an X-wing, protect the Otana.

## Bake content (local Steam install)

```powershell
$env:XWA_INSTALL_DIR = "D:\Steam\steamlabs\common\Star Wars X-Wing Alliance"
dotnet run --project d:\novolis\novolis-experimental\src\Novolis.Experimental.Xwa.Cli -- all --out d:\novolis\novolis-lab\labs\raylib\FreightWing\Content
```

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\raylib\FreightWing -p:NovolisUseProjectReferences=true
```

Smoke (auto-transfer, exit on debrief):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\raylib\FreightWing -p:NovolisUseProjectReferences=true -- --smoke
```
