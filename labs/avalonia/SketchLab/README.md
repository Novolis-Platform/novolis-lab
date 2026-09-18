# SketchLab

Dogfoods **Novolis.Avalonia.Controls** `SketchControl` with a Paint-light toolbar (Font Awesome via **Optris.Icons.Avalonia.FontAwesome**).

**Shipped app:** production host lives in **novolis-apps** as [Sketch Studio](../../../novolis-apps/src/SketchStudio/) — full docs in [docs/sketch-studio/](../../../novolis-apps/docs/sketch-studio/README.md).

## Tools

Pen, Line, Spline, Box, Circle, Eraser, Select

## Options

Snap to grid, **Meetup** (vertex snap), Gridify, stroke color + width

## Clipboard

Copy PNG (ChatGPT-friendly), Copy SVG

## Run

```powershell
dotnet run --project novolis-lab/labs/avalonia/SketchLab -p:NovolisUseProjectReferences=true
```

## Related

| Package / app | Role |
|---------------|------|
| `Novolis.Avalonia.Controls` | `SketchControl` canvas |
| [Sketch Studio](../../../novolis-apps/src/SketchStudio/) | Production sketch studio — [docs/sketch-studio/](../../../novolis-apps/docs/sketch-studio/README.md) |
