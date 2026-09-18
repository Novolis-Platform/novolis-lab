# MusicMakerLab

Magix Music Maker–style **Arrangement** plus an **orchestral multi-part score**.

## Arrangement demo cue

On launch, Arrangement loads the first **~20 seconds** of **By The Sword** by **Kevin Graham**
(public preview from the composer’s [Ablaze](https://www.kevingrahamcomposer.com/ablaze) page),
cached under `%LOCALAPPDATA%\Novolis\MusicMakerLab\`.

For production/commercial use, license the track via Artlist (or your deal with the composer)—the preview is for local dogfood listening only.

## Catalog tab

`MediaCatalogWorkspace` browses free Mutopia / Mixkit collections and an **Inspired · cinematic / space opera** stand-in list.
Paste an Artlist collection URL → **Map inspiration** bookmarks it and jumps to the free stand-in (Artlist files are never scraped/downloaded).

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MusicMakerLab\MusicMakerLab.csproj -p:NovolisUseProjectReferences=true
```
