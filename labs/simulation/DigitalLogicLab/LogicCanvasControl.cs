using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Simulation.Logic;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace DigitalLogicLab;

internal sealed class LogicCanvasControl : Control
{
    private const double NodeWidth = 116;
    private const double NodeHeight = 66;
    private const double PortRadius = 6;

    private readonly LogicCircuitHistory _history;
    private readonly LogicChipLibrary _library;
    private LogicEvaluationResult? _evaluation;
    private LogicCircuit? _previewCircuit;
    private LogicCircuit? _dragBefore;
    private LogicNodeId? _dragNodeId;
    private LogicPortAddress? _pendingSource;
    private Point _lastPointer;
    private Point? _hoverScreen;
    private double _scale = 72;
    private double _originX;
    private double _originZ;
    private bool _panning;
    private bool _dragging;

    public LogicCanvasControl(
        LogicCircuitHistory history,
        LogicChipLibrary library)
    {
        _history = history;
        _library = library;
        Focusable = true;
        ClipToBounds = true;
    }

    public event Action? CircuitChanged;

    public event Action<string>? StatusChanged;

    public LogicNodeId? SelectedNodeId { get; private set; }

    public LogicCircuit CurrentCircuit => _previewCircuit ?? _history.Current;

    public (double X, double Z) SuggestedPosition()
    {
        var index = CurrentCircuit.Nodes.Count;
        return ((index % 5) * 3.5 - 7, (index / 5) * 3.2 - 3);
    }

    public void SetEvaluation(LogicEvaluationResult? evaluation)
    {
        _evaluation = evaluation;
        InvalidateVisual();
    }

    public void Apply(Func<LogicCircuit, LogicCircuit> edit)
    {
        _history.Execute(edit);
        _previewCircuit = null;
        CircuitChanged?.Invoke();
        InvalidateVisual();
    }

    public bool Undo()
    {
        var changed = _history.Undo();
        if (changed)
        {
            CircuitChanged?.Invoke();
            InvalidateVisual();
        }

        return changed;
    }

    public bool Redo()
    {
        var changed = _history.Redo();
        if (changed)
        {
            CircuitChanged?.Invoke();
            InvalidateVisual();
        }

        return changed;
    }

    public void Reset(LogicCircuit circuit)
    {
        _history.Reset(circuit);
        _previewCircuit = null;
        SelectedNodeId = null;
        _pendingSource = null;
        CircuitChanged?.Invoke();
        InvalidateVisual();
    }

    public void Fit()
    {
        if (CurrentCircuit.Nodes.Count == 0)
        {
            _originX = 0;
            _originZ = 0;
            _scale = 72;
            InvalidateVisual();
            return;
        }

        var minX = CurrentCircuit.Nodes.Min(node => node.X);
        var maxX = CurrentCircuit.Nodes.Max(node => node.X);
        var minZ = CurrentCircuit.Nodes.Min(node => node.Z);
        var maxZ = CurrentCircuit.Nodes.Max(node => node.Z);
        _originX = (minX + maxX) * 0.5;
        _originZ = (minZ + maxZ) * 0.5;
        var span = Math.Max(maxX - minX + 4, maxZ - minZ + 4);
        _scale = Math.Clamp(
            Math.Min(Math.Max(1, Bounds.Width), Math.Max(1, Bounds.Height)) /
            span,
            32,
            160);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed ||
            (properties.IsLeftButtonPressed &&
             e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _panning = true;
            _lastPointer = point;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
            return;

        var port = HitTestPort(point);
        if (port is not null)
        {
            HandlePortClick(port.Value.Address, port.Value.Port, e);
            return;
        }

        var node = HitTestNode(point);
        if (node is null)
        {
            SelectedNodeId = null;
            _pendingSource = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        SelectedNodeId = node.Id;
        if (node.Kind == LogicNodeKind.Input)
        {
            Apply(circuit =>
            {
                var value = node.InputValue.Signal == LogicSignal.High
                    ? LogicValue.Low
                    : LogicValue.High;
                return circuit.SetInputValue(node.Id, value);
            });
            StatusChanged?.Invoke(
                $"{node.Label} toggled to {CurrentCircuit.TryGetNode(node.Id, out var updated) switch
                {
                    true when updated!.InputValue.Signal == LogicSignal.High => "1",
                    _ => "0",
                }}");
        }
        else
        {
            _dragging = true;
            _dragNodeId = node.Id;
            _dragBefore = _history.Current;
            _previewCircuit = _history.Current;
            _lastPointer = point;
            e.Pointer.Capture(this);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        _hoverScreen = point;
        if (_panning)
        {
            var dx = point.X - _lastPointer.X;
            var dz = point.Y - _lastPointer.Y;
            _originX -= dx / _scale;
            _originZ -= dz / _scale;
            _lastPointer = point;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_dragging &&
            _dragNodeId is { } nodeId &&
            _dragBefore is not null)
        {
            var world = Snap(ScreenToWorld(point));
            _previewCircuit = _dragBefore.MoveNode(nodeId, world.X, world.Z);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning)
        {
            _panning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_dragging)
        {
            if (_previewCircuit is not null &&
                _dragBefore is not null &&
                !ReferenceEquals(_previewCircuit, _dragBefore))
            {
                var preview = _previewCircuit;
                _history.Execute(_ => preview);
                CircuitChanged?.Invoke();
            }

            _dragging = false;
            _dragNodeId = null;
            _dragBefore = null;
            _previewCircuit = null;
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var anchor = e.GetPosition(this);
        var before = ScreenToWorld(anchor);
        var factor = e.Delta.Y > 0 ? 1.12 : 0.89;
        _scale = Math.Clamp(_scale * factor, 24, 280);
        var after = ScreenToWorld(anchor);
        _originX += before.X - after.X;
        _originZ += before.Z - after.Z;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && SelectedNodeId is { } nodeId)
        {
            Apply(circuit => circuit.RemoveNode(nodeId));
            SelectedNodeId = null;
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Profile.BackgroundBrush, new Rect(Bounds.Size));
        DrawGrid(context);
        DrawConnections(context);
        foreach (var node in CurrentCircuit.Nodes)
            DrawNode(context, node);

        if (_pendingSource is { } source &&
            _hoverScreen is { } hover &&
            TryGetPortPoint(source, out var sourcePoint))
        {
            context.DrawLine(
                new Pen(Profile.AccentBrush, 2),
                sourcePoint,
                hover);
        }

        var hint = _pendingSource is null
            ? "Click an output port, then an input port. Drag nodes. Alt-drag or middle-drag pans."
            : "Choose an input port to complete the wire.";
        context.DrawText(
            new FormattedText(
                hint,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Profile.MutedBrush),
            new Point(12, Math.Max(8, Bounds.Height - 24)));
    }

    private void HandlePortClick(
        LogicPortAddress address,
        LogicPortDefinition port,
        PointerPressedEventArgs e)
    {
        if (port.Direction == LogicPortDirection.Output)
        {
            _pendingSource = address;
            StatusChanged?.Invoke(
                $"Source selected: {address.NodeId} · {address.PortId}");
        }
        else if (_pendingSource is { } source)
        {
            var result = CurrentCircuit.TryConnect(
                LogicConnectionId.New(),
                source,
                address,
                _library);
            if (result.IsValid)
            {
                Apply(_ => result.Circuit);
                StatusChanged?.Invoke("Wire connected.");
            }
            else
            {
                StatusChanged?.Invoke(
                    result.Diagnostics.FirstOrDefault()?.Message ??
                    "The wire could not be connected.");
            }

            _pendingSource = null;
        }
        else
        {
            StatusChanged?.Invoke("Select an output port first.");
        }

        InvalidateVisual();
        e.Handled = true;
    }

    private void DrawGrid(DrawingContext context)
    {
        var minor = new Pen(BrushWithOpacity(Profile.Border, 0.25), 1);
        var major = new Pen(BrushWithOpacity(Profile.Border, 0.48), 1);
        var step = _scale;
        var startX = Math.Floor(ScreenToWorld(new Point(0, 0)).X);
        var endX = Math.Ceiling(ScreenToWorld(new Point(Bounds.Width, 0)).X);
        var startZ = Math.Floor(ScreenToWorld(new Point(0, 0)).Z);
        var endZ = Math.Ceiling(ScreenToWorld(new Point(0, Bounds.Height)).Z);

        for (var x = startX; x <= endX; x++)
        {
            var pen = ((int)Math.Abs(x) % 5 == 0) ? major : minor;
            var a = WorldToScreen(x, startZ);
            var b = WorldToScreen(x, endZ);
            context.DrawLine(pen, a, b);
        }

        for (var z = startZ; z <= endZ; z++)
        {
            var pen = ((int)Math.Abs(z) % 5 == 0) ? major : minor;
            var a = WorldToScreen(startX, z);
            var b = WorldToScreen(endX, z);
            context.DrawLine(pen, a, b);
        }
    }

    private void DrawConnections(DrawingContext context)
    {
        foreach (var connection in CurrentCircuit.Connections)
        {
            if (!TryGetPortPoint(connection.Source, out var source) ||
                !TryGetPortPoint(connection.Destination, out var destination))
            {
                continue;
            }

            var signal = _evaluation?.GetValue(connection.Source)
                         ?? LogicValue.Disconnected;
            context.DrawLine(
                new Pen(SignalBrush(signal), SelectedNodeId is { } selected &&
                    (selected == connection.Source.NodeId ||
                     selected == connection.Destination.NodeId)
                    ? 3
                    : 2),
                source,
                destination);
        }
    }

    private void DrawNode(DrawingContext context, LogicNode node)
    {
        var rect = NodeRect(node);
        var selected = SelectedNodeId == node.Id;
        var fill = node.Kind == LogicNodeKind.CustomChip
            ? Profile.ActionSoftBrush
            : Profile.SurfaceBrush;
        var pen = new Pen(
            selected ? Profile.AccentBrush : Profile.BorderBrush,
            selected ? 2.5 : 1.2);
        context.DrawRectangle(fill, pen, rect, 8, 8);

        var title = $"{node.Label} · {node.Kind}";
        context.DrawText(
            new FormattedText(
                title,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Profile.TextBrush),
            new Point(rect.X + 8, rect.Y + 8));

        if (node.Kind == LogicNodeKind.Input)
        {
            var value = _evaluation?.GetValue(
                            new LogicPortAddress(node.Id, "out"))
                        ?? node.InputValue;
            context.DrawText(
                new FormattedText(
                    value.ToString(),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono"),
                    18,
                    SignalBrush(value)),
                new Point(rect.X + 8, rect.Y + 32));
        }
        else if (node.Kind == LogicNodeKind.Output &&
                 _evaluation?.OutputValues.TryGetValue(node.Id, out var outputValue) == true)
        {
            context.DrawText(
                new FormattedText(
                    outputValue.ToString(),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono"),
                    18,
                    SignalBrush(outputValue)),
                new Point(rect.X + 8, rect.Y + 32));
        }

        foreach (var port in LogicNodeCatalog.GetPorts(node, _library))
        {
            if (!TryGetPortPoint(node, port, out var point))
                continue;
            var address = new LogicPortAddress(node.Id, port.Id);
            var value = _evaluation?.GetValue(address) ?? LogicValue.Disconnected;
            context.DrawEllipse(
                SignalBrush(value),
                new Pen(Profile.BorderBrush, 1),
                point,
                PortRadius,
                PortRadius);
            var labelX = port.Direction == LogicPortDirection.Input
                ? point.X - 28
                : point.X + 10;
            context.DrawText(
                new FormattedText(
                    port.Id,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    Profile.MutedBrush),
                new Point(labelX, point.Y - 7));
        }
    }

    private LogicNode? HitTestNode(Point point) =>
        CurrentCircuit.Nodes
            .Reverse()
            .FirstOrDefault(node => NodeRect(node).Contains(point));

    private (LogicPortAddress Address, LogicPortDefinition Port)? HitTestPort(
        Point point)
    {
        foreach (var node in CurrentCircuit.Nodes.Reverse())
        {
            foreach (var port in LogicNodeCatalog.GetPorts(node, _library))
            {
                if (!TryGetPortPoint(node, port, out var portPoint) ||
                    Math.Sqrt(
                        Math.Pow(portPoint.X - point.X, 2) +
                        Math.Pow(portPoint.Y - point.Y, 2)) > PortRadius + 7)
                {
                    continue;
                }

                return (
                    new LogicPortAddress(node.Id, port.Id),
                    port);
            }
        }

        return null;
    }

    private bool TryGetPortPoint(
        LogicPortAddress address,
        out Point point)
    {
        if (!CurrentCircuit.TryGetNode(address.NodeId, out var node) ||
            node is null ||
            !LogicNodeCatalog.TryGetPort(node, address.PortId, _library, out var port) ||
            port is null)
        {
            point = default;
            return false;
        }

        return TryGetPortPoint(node, port, out point);
    }

    private bool TryGetPortPoint(
        LogicNode node,
        LogicPortDefinition port,
        out Point point)
    {
        var ports = LogicNodeCatalog.GetPorts(node, _library);
        var directionPorts = ports
            .Where(candidate => candidate.Direction == port.Direction)
            .ToArray();
        var index = Array.IndexOf(directionPorts, port);
        var y = WorldToScreen(node.X, node.Z).Y +
                (index - (directionPorts.Length - 1) * 0.5) * 20;
        var center = WorldToScreen(node.X, node.Z);
        point = new Point(
            center.X + (port.Direction == LogicPortDirection.Input
                ? -NodeWidth * 0.5
                : NodeWidth * 0.5),
            y);
        return true;
    }

    private Rect NodeRect(LogicNode node)
    {
        var center = WorldToScreen(node.X, node.Z);
        return new Rect(
            center.X - NodeWidth * 0.5,
            center.Y - NodeHeight * 0.5,
            NodeWidth,
            NodeHeight);
    }

    private Point WorldToScreen(double x, double z) =>
        new(
            (x - _originX) * _scale + Bounds.Width * 0.5,
            (z - _originZ) * _scale + Bounds.Height * 0.5);

    private (double X, double Z) ScreenToWorld(Point point) =>
        (
            (point.X - Bounds.Width * 0.5) / _scale + _originX,
            (point.Y - Bounds.Height * 0.5) / _scale + _originZ);

    private static (double X, double Z) Snap((double X, double Z) point) =>
        (Math.Round(point.X), Math.Round(point.Z));

    private static IBrush SignalBrush(LogicValue value) =>
        value.Signal switch
        {
            LogicSignal.High => Profile.AccentBrush,
            LogicSignal.Low => Profile.MutedBrush,
            LogicSignal.Unknown => Profile.WarningBrush,
            LogicSignal.Contention => Profile.DangerBrush,
            _ => Profile.BorderBrush,
        };

    private static IBrush BrushWithOpacity(Color color, double opacity) =>
        new SolidColorBrush(color) { Opacity = opacity };
}
