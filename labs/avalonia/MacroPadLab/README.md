# Macro Pad Lab

Avalonia lab for the full 4×4 Keytron macro pad (`6D7D:DCFC`).

The lab targets only the exact full-size Keytron VID:PID `6D7D:DCFC`; it does
not present a layout for unrelated HID devices. The action view shows the 16
keys and three controls for each knob, with friendly action descriptions and
the underlying four bytes. The Raw KVP tab contains copyable device, HID
descriptor, probe, protocol, and action details. Writes are explicit, staged
changes are read back, and factory-reset and bootloader commands are not
exposed.

The currently attached unit identifies as `6D7D:DCFC` but exposes a 32-byte
report-ID-2 interface, so the lab intentionally stays in read-only probe mode
until that firmware variant is understood. The standard writer requires
64-byte report-ID-free WebHub frames.

Run locally with:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MacroPadLab\MacroPadLab.csproj -p:NovolisUseProjectReferences=true
```
