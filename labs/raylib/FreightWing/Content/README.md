# FreightWing Content (gitignored pack)

Bake from the local X-Wing Alliance install:

```powershell
$env:XWA_INSTALL_DIR = "D:\Steam\steamlabs\common\Star Wars X-Wing Alliance"
dotnet run --project d:\novolis\novolis-experimental\src\Novolis.Experimental.Xwa.Cli -- all --out d:\novolis\novolis-lab\labs\raylib\FreightWing\Content
```

Produces `freightwing.novpack` + `manifest.json` (opaque ids). The game falls back to primitive craft if the pack is missing.
