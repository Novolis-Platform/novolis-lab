# XFighter

3D cockpit space-combat demo lab **Novolis.Raylib**, **Novolis.Audio.*** (Core, Effects, Playback, Voice, Voice.Profiles, Voice.SherpaOnnx), and in-repo **Novolis.Lab.Voice** (ATC-style comms).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/raylib/XFighter
```

## Controls

| Input | Action |
|-------|--------|
| Mouse | Aim |
| W / S | Throttle |
| A / D | Roll |
| Space / LMB | Fire lasers |
| M | Toggle audio |
| R | Reset mission |

Cockpit HUD shows kills, shield, throttle, targeting lock, and wingman comms (TTS when voice models are available).

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
