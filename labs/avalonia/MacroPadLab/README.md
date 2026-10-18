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
verify it. The “Stage safe testing map” action assigns F13–F24 to the first 12
keys and consumer/media controls to the remaining keys and all knobs. It also
corrects the inverted volume direction. “Write staged changes” is the explicit
flash write.

The RGB lighting section reads the factory lighting response and lets you write
the mode, brightness, speed, custom-color flag, and an `#RRGGBB` color. The
color is converted to the firmware's 0–255 HSV representation. The raw
lighting response and accepted write echo are included in the KVP and wire log.

Other firmware variants may expose the newer WebHub protocol (64-byte,
report-ID-free frames); those are handled separately when their report shape
is present. No firmware-update, reset, or generic-HID layout commands are
exposed.

Run locally with:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MacroPadLab\MacroPadLab.csproj -p:NovolisUseProjectReferences=true
```
