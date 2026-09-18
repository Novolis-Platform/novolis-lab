# ChannelLab

Avalonia IRC dogfood: control window + peer windows over a tiny ASP.NET **ChannelHost** (SignalR + guest nicks). Media plane is native Avalonia + `Novolis.Video.Rtc` mesh (no WebView / browser WebRTC).

## Primitives

| Primitive | Role |
|-----------|------|
| Nick | Guest JWT via `POST /api/guest` (`PlayerRef`) |
| Channel | `#lobby` only |
| Message | `Say` fan-out + SQLite scrollback |
| Presence | `Roster` on join/part |
| MediaSession | Avalonia `VideoSurface` tiles + `Novolis.Video.Rtc` mesh (max **4** peers). SignalR relays `video-join` / `video-part` / `offer` / `answer` / `ice` only. No SFU, LiveKit, Coturn, or WebView. |

## Run

Happy path (app starts ChannelHost on `http://127.0.0.1:5177` if needed):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\ChannelLab\ChannelLab.csproj -p:NovolisUseProjectReferences=true
```

Host alone:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\ChannelLab\ChannelHost\ChannelHost.csproj -p:NovolisUseProjectReferences=true
```

Media packages (`Novolis.Avalonia.Video`, `Novolis.Video.Rtc*`, `Novolis.Video.Capture.Windows`) are on GitHub Packages. For local sibling iteration, pass `-p:NovolisUseProjectReferences=true` (or build via `d:\novolis\Novolis.Platform.slnx`).

## Dogfood proof

Headless (host must be running):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\ChannelLab\ChannelHost\ChannelHost.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\ChannelLab\ChannelSmoke\ChannelSmoke.csproj
```

Smoke covers text fan-out plus signaling (`video-join` + fake `offer`). No camera in CI.

UI:

1. Start ChannelLab — control window ensures host, opens **alice** and **bob** peers.
2. Click **Connect** on each peer.
3. Type in alice; bob should see the line. Roster lists both nicks.
4. Toggle **Video** on both peers (Windows camera permission) — local + remote `VideoSurface` tiles.
5. Reconnect a peer — SQLite scrollback (under `%LocalAppData%\Novolis\ChannelLab\messages.db`) replays recent lines.

## Stack

- Avalonia control + peer windows (Fluent, no Inter)
- ChannelHost: SignalR hub + JWT guest claims (`Novolis.Game.Identity` / `Identity.AspNetCore`)
- Mesh: `Novolis.Video.Rtc` (SIPSorcery), `Novolis.Video.Capture.Windows`, `Novolis.Avalonia.Video` — packages live in [novolis-video](https://github.com/Novolis-Platform/novolis-video)
- Scrollback: `Microsoft.Data.Sqlite` at `%LocalAppData%\Novolis\ChannelLab\`  
  (`Novolis.Storage.Sqlite` is currently unusable — `IKeyed` / `IRepository` API drift — so the host uses the same SQLite stack that package wraps.)

## Non-goals

Voxa microservices, RavenDB, Duende, Aspire SQL Edge, YARP, LiveKit, Coturn, browser WebView media, workspaces/admin portal.
