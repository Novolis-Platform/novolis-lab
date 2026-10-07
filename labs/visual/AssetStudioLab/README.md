# Asset Studio

A visual asset is a small program. This lab is its IDE.

One canonical visual AST is edited by the stack view, the source editor, command
mode (`Novolis.Commands.Expressions`), generated C#, and JSON. Compilation lowers
that tree to a typed Visual IR, then to Silk mixers, Rendering fields, baked
maps, and C#. Workspaces timeline supplies branchable experiment history.

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\visual\AssetStudioLab\AssetStudioLab.csproj -p:NovolisUseProjectReferences=true
```

Headless compile / bake / compose (no window):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\visual\AssetStudioLab\AssetStudioLab.csproj -p:NovolisUseProjectReferences=true -- --headless
```

Tests:

```powershell
dotnet test d:\novolis\novolis-lab\labs\visual\AssetStudioLab\tests\AssetStudioLab.Unit\AssetStudioLab.Unit.csproj -p:NovolisUseProjectReferences=true
```

## Layout

Lab-private core under `AssetStudioLab.Core` is the candidate split for a future
`Novolis.Visual` family. It does not reference Silk.NET or Rendering packages.
Handshake payloads (`SilkVisualProgram`, `RenderingVisualProgram`) use Math/BCL
only. Compose Silk or Rendering later in this host or in `novolis-silk` /
`novolis-rendering` adapters.

This host is not a release artifact. Graduate a successful experiment into
`novolis-visual` packages plus an app in `novolis-apps`.
