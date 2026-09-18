# KatoriLab

Avalonia dogfood for **Tenshin Shōden Katori Shintō-ryū**-inspired kenjutsu vocabulary on the CharacterLab wire stack:

- Full `KenTimeline` dojo kata: door rei → walk → opening pose → ken → closing → return
- Wire capsule mannequin + Front/Side sticks
- Bokken hold points via `HumanoidFullBodyIk` (no post-IK weapon snap)

This is a **stylized engineering demo** of classical kenjutsu kamae and cut shapes — not an official transmission of the ryū.

## Run

```powershell
dotnet run --project novolis-lab/labs/avalonia/KatoriLab -p:NovolisUseProjectReferences=true
```

Headless / tests:

```powershell
dotnet run --project novolis-lab/labs/avalonia/KatoriLab -p:NovolisUseProjectReferences=true -- --kata-smoke
dotnet run --project novolis-lab/labs/avalonia/KatoriLab -p:NovolisUseProjectReferences=true -- --agent-explore
dotnet test novolis-lab/labs/avalonia/KatoriLab.Tests -p:NovolisUseProjectReferences=true
```

`KataCorrectness` gates (door→center, walk arm hang, chūdan/jōdan geometry + holds, tip continuity, kesagiri travel, return) are shared by `--kata-smoke` and `KatoriLab.Tests`.

## Agent

`http://127.0.0.1:18797` — requires direct `MessagePack` PackageReference (Annotations load). Actions: `listphases`, `sampleholds`, `diagnose`, `setphasetime` (`phase|rei,chudan,jodan,kesagiri,gedan,recover`), `explore`, …
