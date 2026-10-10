# Cursor Remote

Experimental lab host in `novolis-lab`.

Phone console for the **Cursor** IDE on your unlocked Windows PC — Tailscale LAN
discovery, no port/token pairing. The host captures the Cursor window (or primary
monitor) and injects input; the Flip finds matching `CursorRemote` hosts and
connects when the protocol aligns.

## What it is

| Surface | Role |
| --- | --- |
| Windows host | Listens on Tailscale `:18790`, UDP discovery `:18791`, activity log + tray |
| Android controller | Scan → tap host → pinch/pan screen, tap, type, focus Cursor |

Not RDP. Not a Cursor agent API. Direct console capture/input on an unlocked session.

## Run the Windows host

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\apps\CursorRemote\CursorRemote.Desktop\CursorRemote.Desktop.csproj -p:NovolisUseProjectReferences=true
```

Close/minimize goes to the tray by default. **Export log** writes a `.log` file.

Firewall (Tailscale range):

```powershell
New-NetFirewallRule -DisplayName "Cursor Remote HTTP (Tailscale)" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 18790 -Profile Any -RemoteAddress 100.64.0.0/10
New-NetFirewallRule -DisplayName "Cursor Remote Discovery (Tailscale)" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 18791 -Profile Any -RemoteAddress 100.64.0.0/10
```

## Build and install Android

```powershell
dotnet build d:\novolis\novolis-lab\labs\apps\CursorRemote\CursorRemote.Android\CursorRemote.Android.csproj -c Release -p:NovolisUseProjectReferences=true
adb install -r d:\novolis\novolis-lab\artifacts\bin\CursorRemote.Android\release\com.novolis.cursorremote-Signed.apk
```

This host remains a lab artifact while the remote-control contract is being
explored. Sustained product packaging belongs to a future product decision.
