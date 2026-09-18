# StudioChromeLab

Avalonia studio chrome dogfood for **Novolis.Avalonia.Controls** (ChoiceDialog, FilteredPickerDialog, MarkedListBox, JobQueuePanel), **Novolis.Avalonia.Studio** (FocusMode, StatusBrushes), **Novolis.Avalonia.Agent** (+ Protocol agent IDs), and **Novolis.IO.Processes** (live job queue).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/avalonia/StudioChromeLab
```

## UI demo

Three-column layout: chapter nav (`MarkedListBox`), toolbar demos, job queue panel.

| Button / key | Action |
|--------------|--------|
| Fake recovery… | `ChoiceDialog` with restore/compare/discard |
| Fake conflict… | External-change conflict dialog |
| Go to… | `FilteredPickerDialog` chapter picker |
| Toggle focus (F11) | `StudioFocusMode` hides menu/top/status chrome |
| Toggle dirty | Status bar clean/dirty brush |
| Enqueue dotnet --info | Runs via `ProcessJobQueue`; cancel/open output on rows |

Flash and status lines use `StudioChrome.CreateFeedback()`.

## Agent + MCP smoke

Enable the in-process agent host, then drive UI via **AvaloniaAgentMcp**:

```powershell
$env:NOVOLIS_AVALONIA_AGENT = "1"
dotnet run --project labs/avalonia/StudioChromeLab -p:NovolisUseProjectReferences=true
```

Step-by-step: [AGENT-SMOKE.md](./AGENT-SMOKE.md) (`UiHello` → `UiTree` → `UiClick`(`lab.dirty`) → `UiScreenshot`).

No `--smoke` CLI flag on the lab itself.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
