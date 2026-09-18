# MovieMakerLab

Full Movie Maker–style dogfood for `Novolis.Video.Edit` + `Novolis.Avalonia.Video`.

## Features in the UI

- **Media library** — thumbnails for images / color cards / audio; preview pane; double-click or **Add to storyboard** / **Add to audio track**
- **Transition inspector** — select a storyboard clip, choose Fade/Wipe + duration, **Apply** (amber wedge on the strip)
- **Export movie…** — writes playable `movie.avi` (+ `audio.wav` + `movie.json`)

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MovieMakerLab\MovieMakerLab.csproj -p:NovolisUseProjectReferences=true
```

## Suggested walkthrough

1. Play the starter storyboard (Sky → Hills with fade).
2. In the library, select **Dusk still** → **Add to storyboard**.
3. Click the Hills clip on the storyboard → set Wipe 0.7s → **Apply transition**.
4. Select **Bed tone A3** → **Add to audio track**.
5. **Export movie…** and open `movie.avi`.

Demo assets: `%LocalAppData%\Novolis\MovieMakerLab\demo-assets\`.
