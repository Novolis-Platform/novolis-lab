# ChannelLab

Avalonia secure-text dogfood: control window + peer windows over a small ASP.NET **ChannelHost**.
SignalR delivers opaque direct envelopes; endpoints compare device fingerprints, pin peer bundles, and
decrypt text locally. Media remains native Avalonia + `Novolis.Video.Rtc` mesh (no WebView / browser WebRTC).

## Primitives

| Primitive | Role |
|-----------|------|
| Nick | Guest JWT via `POST /api/guest` (`PlayerRef`) for relay access only |
| Channel | `#lobby` only |
| Device | P-256 public bundle registration and an independently compared fingerprint |
| Protected text | Direct AES-GCM envelope relay + ciphertext-only SQLite history |
| Protected group text | Explicitly approved roster + one AES-GCM encrypted copy per recipient + ciphertext-only SQLite history |
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

Smoke enrolls three devices, validates public-bundle retrieval, relays and decrypts Unicode
markers, rejects altered ciphertext, confirms direct and group markers are absent from SQLite,
requires every group device to approve the roster, and covers signaling (`video-join` + fake
`offer`). No camera in CI.

UI:

1. Start ChannelLab — control window ensures host, opens **alice** and **bob** peers.
2. Click **Connect** on each peer.
3. Select the other nick in each peer window. Compare each displayed device fingerprint through an
   independent channel, then click **Trust peer** on both sides.
4. Type in alice; bob should see the decrypted line. Roster lists both nicks.
5. For a group, alice first trusts every invited peer, enters a group name and comma-separated nicks,
   and clicks **Create group**. Every invited peer selects the pending group, independently verifies
   and trusts every listed device, then clicks **Approve group**. Text is enabled after every device approves.
6. Toggle **Video** on both peers (Windows camera permission) — local + remote `VideoSurface` tiles.
7. Reconnect a peer — SQLite scrollback (under `%LocalAppData%\Novolis\ChannelLab\messages.db`) replays recent encrypted envelopes.

## Stack

- Avalonia control + peer windows (Fluent, no Inter)
- ChannelHost: SignalR hub + JWT guest claims (`Novolis.Game.Identity` / `Identity.AspNetCore`), opaque envelope routing, and ciphertext persistence
- Secure text: `Novolis.Security.SecureText` (P-256 ECDSA/ECDH, HKDF-SHA-256, AES-256-GCM) + `Novolis.Messaging.SecureText` (canonical envelope and replay state)
- Mesh: `Novolis.Video.Rtc` (SIPSorcery), `Novolis.Video.Capture.Windows`, `Novolis.Avalonia.Video` — packages live in [novolis-video](https://github.com/Novolis-Platform/novolis-video)
- Scrollback: `Microsoft.Data.Sqlite` at `%LocalAppData%\Novolis\ChannelLab\`  
  (`Novolis.Storage.Sqlite` is currently unusable — `IKeyed` / `IRepository` API drift — so the host uses the same SQLite stack that package wraps.)

## Non-goals

Group roster changes, offline prekeys, multi-device synchronization, ratcheting, forward secrecy,
post-compromise security, Voxa microservices, RavenDB, Duende, Aspire SQL Edge, YARP, LiveKit,
Coturn, browser WebView media, workspaces/admin portal.

## Group security limits

ChannelLab groups use pairwise fan-out: the sender encrypts a separate message for every approved
device. Each client persists its own approval for the immutable roster and rejects a same-id update
that changes that roster. This retains the direct-text security boundary but has linear sender work
and relay storage. Changing membership requires a new group instead of changing an active roster.
The relay can see the roster, delivery timing, and ciphertext sizes, but never group plaintext.
