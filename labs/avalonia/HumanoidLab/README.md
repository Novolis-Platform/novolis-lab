# HumanoidLab

Avalonia dogfood for `Novolis.Simulation.Humanoid` + `Novolis.Simulation.Humanoid.Physics`.

Four panes:

1. **Walk** — procedural clip → FK → capsule mannequin (side view)
2. **Ragdoll** — settled ConstrainedSphereSimulator + **AdaptiveMesh** person hull (`HumanoidAdaptiveBody`)
3. **Bow** — bow-draw clip + `TwoBoneIk` + mannequin + bow overlay
4. **Reach** — `HumanoidFullBodyIk` (hands / feet / head); **drag amber handles** to pin targets; idle sway when free; pose persisted via `BakeLocal`

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\HumanoidLab -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\HumanoidLab -p:NovolisUseProjectReferences=true -- --smoke
```

Use ProjectReference mode until Humanoid packages are on GitHub Packages.

## HTTP control (agent tooling)

Starts on `http://127.0.0.1:18765/` (override with `HUMANIOD_LAB_HTTP_PORT`). Marker: `%TEMP%/novolis-humanoid-lab-http.txt`.

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness |
| GET | `/ragdoll` | Speeds, KE, bone error, sleep, hip |
| POST | `/ragdoll/reset` | Respawn standing |
| POST | `/ragdoll/tip` | Body `{ "impulse": [x,y,z] }` optional |
| POST | `/ragdoll/entropy` | Body `{ "rate": 3.2, "autoTip": true }` |
