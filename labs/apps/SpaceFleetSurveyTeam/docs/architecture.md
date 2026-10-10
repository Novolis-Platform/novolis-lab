# Architecture

## Ownership

| Concern | Owner |
|---------|--------|
| Region loop (Survey → Detect → Resolve → Certify), certification state, terrain classification | This app (`SpaceFleetSurveyTeam`) |
| Live instrument widgets / HUD “gizmos” (waveform, luminance strips, compass, route geometry) | Packable `Novolis.Avalonia.*` under `d:\novolis\novolis-avalonia\src\` (extend `Novolis.Avalonia.Gaming` and/or add `Novolis.Avalonia.Instruments`) |
| On-device sensor facades (mic, camera metrics, magnetometer, GPS/motion) | `Novolis.Avalonia.Mobile` + `.Android` / `.Desktop` hosts — **not** `Novolis.IO.Mobile.Android` (host ADB) |
| Mic capture primitives (desktop today) | `Novolis.Audio.*` when reused |

`novolis-lab` remains NuGet-only: the game PackageReferences published
packages; it does not host shared libraries.

## Privacy

Sonic and photonic pipelines analyse live signals only. No retained audio or imagery is part of the design. Spatial and magnetic samples are metrics suitable for survey evidence, not raw media archives.

## Heads

| Project | Role |
|---------|------|
| `SpaceFleetSurveyTeam` | Shared UI + survey loop stubs |
| `SpaceFleetSurveyTeam.Desktop` | Windows UI iteration |
| `SpaceFleetSurveyTeam.Android` | Phone field instrument (`net10.0-android`) |
