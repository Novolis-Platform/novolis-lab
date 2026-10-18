# Digital Logic Lab

Unity-free digital circuit editor and deterministic simulator built from the
reusable `Novolis.Simulation.Logic` package.

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\DigitalLogicLab\DigitalLogicLab.csproj -p:NovolisUseProjectReferences=true
```

The starter circuit is a two-input AND gate. Click an input to toggle it.
Click an output port and then an input port to connect a wire. Drag nodes,
pan with Alt-drag or the middle button, zoom with the wheel, and use the
toolbar to add gates, step simulation, undo/redo, or save/load.

Circuits are saved to the per-user application-data path shown in the status
line. The saved document contains structural data and configured input values,
not transient evaluation state.

## Attribution

The interaction concept and educational direction are inspired by
[Sebastian Lague's Digital Logic Sim](https://github.com/SebLague/Digital-Logic-Sim).
That upstream repository is MIT licensed; see its
[license](https://raw.githubusercontent.com/SebLague/Digital-Logic-Sim/main/LICENSE).
This lab uses original Novolis C# and UI code and does not carry Unity source,
art, audio, or other upstream assets.
