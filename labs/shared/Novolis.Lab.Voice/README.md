# Novolis.Lab.Voice

In-repo shared library (not published). DI and presets for ATC-style voice delivery in dogfood demos.

Dogfoods **Novolis.Audio.Voice** (+ Abstractions, Design, Phraseology, Profiles, Platform.Abstractions, SherpaOnnx), **Novolis.Audio.Effects**, and **Novolis.Audio.Filters**.

## Consumers

- **XFighter** (wingman comms)
- **BridgeCommander** (when using shared voice wiring)

## API (summary)

- `AddNovolisAtcVoice` — register phraseology + Piper-backed voice service
- `AtcVoiceProfile`, `AtcRadioEffects`, `KokoroVoiceArchetypeCatalog`
- Code-gen helpers for voice preset export

## ProjectRef note

Same-repo `ProjectReference` only. Novolis packages resolve from GitHub Packages (`2026.1.*`) unless you build via `Novolis.Platform.slnx` or `-p:NovolisUseProjectReferences=true`.
