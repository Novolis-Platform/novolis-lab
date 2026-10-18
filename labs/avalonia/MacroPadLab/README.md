# Macro Pad Lab

Avalonia lab for the full 4×4 Keytron macro pad (`6D7D:DCFC`).

The lab enumerates every HID interface, highlights the vendor configuration
interface, and offers a read-only probe for report-shape diagnostics. Action,
profile, and backlight writes are enabled only when the interface matches the
documented 64-byte WebHub report format. Factory reset and bootloader commands
are not exposed.

The currently attached unit identifies as `6D7D:DCFC` but exposes a 32-byte
report-ID-2 interface, so the lab intentionally stays in read-only probe mode
until that firmware variant is understood.

Run locally with:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MacroPadLab\MacroPadLab.csproj -p:NovolisUseProjectReferences=true
```
