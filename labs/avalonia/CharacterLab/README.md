# CharacterLab

CMU BVH **mocap player** dogfood: orbitable 3D wire capsule mannequin, Front/Side sticks,
and rifle hold locks via `HumanoidFullBodyIk` / `BakeLocal`. Character/weapon FBX meshes stay off
until the wire path reads correctly.

## What you see

- Primary: **3D wire mannequin** (`SceneWireGlControl`) — bone capsules + rifle gizmo
- Secondary: Front (XY) / Side (ZY) stick panes
- Clip dropdown + scrub/seek + pause + hold-lock toggle
- Hands locked to primary/secondary holds when hold mode is on

## Motion sources

| Source | Role |
|--------|------|
| [CMU MoCap](http://mocap.cs.cmu.edu/) BVH under `assets/mocap/` | Shipped clips (see `assets/mocap/CREDITS.md`) |
| Synthetic drill | Procedural Order → Present → Salute (still listed) |

**Not vendored:** [EasyMocap](https://github.com/zju3dv/EasyMocap) / [FreeMoCap](https://freemocap.org/) are capture pipelines for a later ingest story (cameras → BVH/FBX → this player). [mannequin.js](https://boytchev.github.io/mannequin.js/) is **GPL-3.0** visual inspiration only — not copied.

## Run

```powershell
dotnet run --project novolis-lab/labs/avalonia/CharacterLab -p:NovolisUseProjectReferences=true
```

```powershell
dotnet run --project novolis-lab/labs/avalonia/CharacterLab -p:NovolisUseProjectReferences=true -- --drill-smoke
dotnet run --project novolis-lab/labs/avalonia/CharacterLab -p:NovolisUseProjectReferences=true -- --agent-explore
```

## Agent

`http://127.0.0.1:18795` — snapshot fields `clip` / `source` (`cmu-bvh` \| `synthetic`); actions `explore`, `sampleholds`, `setphasetime` (`clip?`, `time?`, `phase?`), …

## Next

When mocap + hold locks look right in wire: optional `--with-mesh` character/weapon overlay (holds remain source of truth).
