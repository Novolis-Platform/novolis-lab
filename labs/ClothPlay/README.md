# ClothPlay

Interactive cloth room lab **Novolis.Physics.Cloth** (`ClothSheetPreset`, `ClothSheetSimulator`, `ClothCutOps`; length constraints from **Novolis.Physics.Joints**) plus Raylib / Simulation / Collision.

Default scene is a **flag** (top-row pinned). Drape/cut demos use a **horizontal katana** (edge up/down on a stand — not tip-up).

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\ClothPlay -p:NovolisUseProjectReferences=true
```

Headless smoke:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\ClothPlay -p:NovolisUseProjectReferences=true -- --smoke
```

## Controls

| Input | Action |
|-------|--------|
| LMB | Shove particle |
| R | Flag (pinned, wind) |
| 3 | Drop onto katana — drape only |
| 4 | Drop onto katana — cut on contact ridge |
| 5 | Katana edge **up** |
| 6 | Katana edge **down** |
| B | Blast tear + impulse |
| W | Toggle wind |
| F3 | Diagnostics |

If the HUD warns **GROUND HIT**, the sheet reached the floor — that means the setup failed (cloth should hang as a flag or rest on the katana).

## Stiffness

Cloth uses a high `MaxStrainFraction` on `ClothSheetSimulator` (the ragdoll default 0.35 strain cap is what made fabric go doughy). Flag smoke asserts max structural stretch &lt; 1.2× rest and bottom Y above the ground-fail line.

## ProjectRef

`ClothPlay` PackageReferences `Novolis.Raylib.Game` / `Bindings` / `Runtime` explicitly (ProjectRef is non-transitive).
