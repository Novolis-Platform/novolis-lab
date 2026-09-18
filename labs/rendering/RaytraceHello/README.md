# RaytraceHello

ILGPU path-traced showcase presented through **Raylib** lab **Novolis.Raylib.Game**, **Novolis.Rendering.Backends.Igpu**, **Novolis.Rendering.Compile**, **Novolis.Rendering.DependencyInjection**, **Novolis.Rendering.Materials**, **Novolis.Rendering.Presentation.Raylib**, and **Novolis.Rendering.Scene**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/rendering/RaytraceHello
```

Optional env (no CLI args):

```powershell
$env:NOVOLIS_RAY_BACKEND = "cpu"   # default is ILGPU
$env:NOVOLIS_ILGPU_DEVICE = "(auto)" # ILGPU device index/name
dotnet run --project labs/rendering/RaytraceHello
```

## Controls

| Input | Action |
|-------|--------|
| Space | Toggle auto-orbit (async samples per frame) |
| R | Reset accumulation |

HUD shows backend label, sample count, and active env vars. Window resizes re-upload the scene.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
