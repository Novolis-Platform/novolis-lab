# ManuscriptSmoke

Console smoke test for **Novolis.Markup.Manuscript** and **Novolis.Audio.Voice.Manuscript** from GitHub Packages (`2026.1.*`).

Parses a sample chapter for callout metadata and word count, then builds a TTS **SpeechPlanner** dry-run (no audio synthesis).

## Run

```powershell
dotnet restore
dotnet run --project labs/manuscript/ManuscriptSmoke
```

## What it exercises

| Area | Behavior |
|------|----------|
| Manuscript parsing | Chapter structure, callouts, word count |
| Speech planning | Voice manuscript → planner output (stdout only) |

## Related

| Package / app | Role |
|---------------|------|
| `Novolis.Markup.Manuscript` | Source markup parser |
| `Novolis.Audio.Voice.Manuscript` | TTS speech planner |
| [VoiceSmoke](../audio/VoiceSmoke/) | End-to-end Piper synthesis |
