# Macro Pad Lab

Avalonia lab for the full 4×4 Keytron macro pad (`6D7D:DCFC`).

The lab targets only the exact full-size Keytron VID:PID `6D7D:DCFC`; it does
not present a layout for unrelated HID devices. The action view shows the 16
keys and three controls for each knob, with friendly action descriptions and
the underlying raw bytes. The Raw KVP tab contains copyable device, HID
descriptor, protocol, response, and action details. Writes are explicit,
staged changes are read back, and factory-reset and bootloader commands are
not exposed.

This Keytron's attached interface is factory firmware: usage `0xFF00/0x01`,
report ID `2`, 32-byte reports, and three-byte action records. The lab
recognizes that protocol, reads the complete 25-slot table, and can write and
verify it. The “Stage useful knob map” action corrects the inverted volume
direction and replaces mute presses with play/pause, track, brightness, and
navigation actions. “Write staged changes” is the explicit flash write.

Other firmware variants may expose the newer WebHub protocol (64-byte,
report-ID-free frames); those are handled separately when their report shape
is present. No firmware-update, reset, or generic-HID layout commands are
exposed.

Run locally with:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MacroPadLab\MacroPadLab.csproj -p:NovolisUseProjectReferences=true
```
