using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Simulation.Logic;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace DigitalLogicLab;

internal sealed class MainWindow : Window
{
    private readonly LogicChipLibrary _library = LogicChipLibrary.Empty;
    private readonly LogicCircuitHistory _history;
    private readonly LogicCanvasControl _canvas;
    private readonly TextBlock _status;
    private readonly TextBlock _diagnostics;
    private readonly DispatcherTimer _runTimer;
    private readonly string _savePath;
    private ulong _tick;
    private bool _running;

    public MainWindow()
    {
        Title = "Digital Logic Lab";
        Width = 1360;
        Height = 820;
        MinWidth = 900;
        MinHeight = 560;
        GraphicalProfile.ApplyWindowChrome(this);

        _history = new LogicCircuitHistory(CreateStarterCircuit());
        _canvas = new LogicCanvasControl(_history, _library);
        _canvas.CircuitChanged += Refresh;
        _canvas.StatusChanged += SetStatus;

        _status = new TextBlock
        {
            Margin = new Thickness(12, 4),
            FontFamily = Profile.BodyFont,
        };
        GraphicalProfileBinding.Bind(
            _status,
            TextBlock.ForegroundProperty,
            Profile.MutedResourceKey);

        _diagnostics = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = Profile.MonoFont,
            Margin = new Thickness(12, 0, 12, 12),
        };
        GraphicalProfileBinding.Bind(
            _diagnostics,
            TextBlock.ForegroundProperty,
            Profile.TextResourceKey);

        _savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "DigitalLogicLab",
            "circuit.json");
        _runTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220),
        };
        _runTimer.Tick += (_, _) => StepSimulation();

        Content = BuildLayout();
        Opened += (_, _) =>
        {
            _canvas.Fit();
            Refresh();
        };
        KeyDown += OnKeyDown;
    }

    private Control BuildLayout()
    {
        var title = new TextBlock
        {
            Text = "Digital Logic Lab",
            FontSize = 28,
            FontFamily = Profile.BodyFont,
            Margin = new Thickness(16, 14, 16, 2),
        };
        GraphicalProfileBinding.Bind(
            title,
            TextBlock.ForegroundProperty,
            Profile.TextResourceKey);

        var eyebrow = new TextBlock
        {
            Text = "Build a circuit, inspect deterministic signals, and save the graph.",
            FontFamily = Profile.BodyFont,
            Margin = new Thickness(16, 0, 16, 10),
        };
        GraphicalProfileBinding.Bind(
            eyebrow,
            TextBlock.ForegroundProperty,
            Profile.MutedResourceKey);

        var toolbar = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 0, 12, 8),
        };
        toolbar.Children.Add(ActionButton("Input", AddInput));
        toolbar.Children.Add(ActionButton("Output", AddOutput));
        toolbar.Children.Add(ActionButton("AND", () => AddGate(LogicNodeKind.And)));
        toolbar.Children.Add(ActionButton("OR", () => AddGate(LogicNodeKind.Or)));
        toolbar.Children.Add(ActionButton("NOT", () => AddGate(LogicNodeKind.Not)));
        toolbar.Children.Add(ActionButton("XOR", () => AddGate(LogicNodeKind.Xor)));
        toolbar.Children.Add(ActionButton("NAND", () => AddGate(LogicNodeKind.Nand)));
        toolbar.Children.Add(ActionButton("Run / Pause", ToggleRun));
        toolbar.Children.Add(ActionButton("Step", StepSimulation));
        toolbar.Children.Add(ActionButton("Undo", () => _canvas.Undo()));
        toolbar.Children.Add(ActionButton("Redo", () => _canvas.Redo()));
        toolbar.Children.Add(ActionButton("Fit", _canvas.Fit));
        toolbar.Children.Add(ActionButton("Save", Save));
        toolbar.Children.Add(ActionButton("Load", Load));

        var diagnostics = Chrome(
            "Diagnostics",
            new ScrollViewer
            {
                Content = _diagnostics,
                VerticalScrollBarVisibility =
                    Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            });
        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,300"),
            Margin = new Thickness(12, 0, 12, 0),
        };
        Grid.SetColumn(diagnostics, 1);
        columns.Children.Add(_canvas);
        columns.Children.Add(diagnostics);

        var root = new DockPanel();
        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(eyebrow, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(title);
        root.Children.Add(eyebrow);
        root.Children.Add(toolbar);
        root.Children.Add(_status);
        root.Children.Add(columns);
        return root;
    }

    private void AddInput()
    {
        var position = _canvas.SuggestedPosition();
        var label = $"Input {_history.Current.Nodes.Count(node =>
            node.Kind == LogicNodeKind.Input) + 1}";
        _canvas.Apply(circuit => circuit.AddNode(
            LogicNode.CreateInput(label, position.X, position.Z)));
        SetStatus($"{label} added. Click it to toggle.");
    }

    private void AddOutput()
    {
        var position = _canvas.SuggestedPosition();
        var label = $"Output {_history.Current.Nodes.Count(node =>
            node.Kind == LogicNodeKind.Output) + 1}";
        _canvas.Apply(circuit => circuit.AddNode(
            LogicNode.CreateOutput(label, position.X, position.Z)));
        SetStatus($"{label} added.");
    }

    private void AddGate(LogicNodeKind kind)
    {
        var position = _canvas.SuggestedPosition();
        _canvas.Apply(circuit => circuit.AddNode(
            LogicNode.CreateGate(
                kind,
                kind.ToString().ToUpperInvariant(),
                position.X,
                position.Z)));
        SetStatus($"{kind} gate added.");
    }

    private void ToggleRun()
    {
        _running = !_running;
        if (_running)
        {
            _runTimer.Start();
            SetStatus("Simulation running.");
        }
        else
        {
            _runTimer.Stop();
            SetStatus("Simulation paused.");
        }
    }

    private void StepSimulation()
    {
        _tick++;
        Refresh();
        SetStatus($"Simulation tick {_tick}.");
    }

    private void Refresh()
    {
        var evaluation = _canvas.CurrentCircuit.Evaluate(_library);
        _canvas.SetEvaluation(evaluation);
        var diagnostics = evaluation.Diagnostics.Count == 0
            ? "No diagnostics. Click an output port, then an input port, to wire the circuit."
            : string.Join(
                Environment.NewLine,
                evaluation.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Severity}: {diagnostic.Code} · {diagnostic.Message}"));
        _diagnostics.Text = diagnostics;
        _status.Text =
            $"{(_running ? "Running" : "Paused")} · tick {_tick} · " +
            $"{_canvas.CurrentCircuit.Nodes.Count} nodes · " +
            $"{_canvas.CurrentCircuit.Connections.Count} wires · " +
            $"save path: {_savePath}";
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
            File.WriteAllText(
                _savePath,
                LogicCircuitJson.Serialize(_canvas.CurrentCircuit));
            SetStatus($"Saved circuit to {_savePath}");
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_savePath))
            {
                SetStatus($"No saved circuit at {_savePath}");
                return;
            }

            _canvas.Reset(LogicCircuitJson.Deserialize(
                File.ReadAllText(_savePath)));
            _canvas.Fit();
            SetStatus($"Loaded circuit from {_savePath}");
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
        {
            Save();
            e.Handled = true;
        }
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
    }

    private static Button ActionButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 34,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(12, 6),
            FontFamily = Profile.BodyFont,
        };
        GraphicalProfileBinding.Bind(
            button,
            Button.BackgroundProperty,
            Profile.ActionResourceKey);
        GraphicalProfileBinding.Bind(
            button,
            Button.ForegroundProperty,
            Profile.OnActionResourceKey);
        button.Click += (_, _) => action();
        return button;
    }

    private static Border Chrome(string title, Control child)
    {
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontFamily = Profile.BodyFont,
            Margin = new Thickness(12, 10, 12, 6),
        };
        GraphicalProfileBinding.Bind(
            heading,
            TextBlock.ForegroundProperty,
            Profile.TextResourceKey);
        child.Margin = new Thickness(12, 0, 12, 12);
        var body = new DockPanel();
        DockPanel.SetDock(heading, Dock.Top);
        body.Children.Add(heading);
        body.Children.Add(child);
        var border = new Border
        {
            Child = body,
            Margin = new Thickness(4, 0, 0, 0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        };
        GraphicalProfileBinding.Bind(
            border,
            Border.BackgroundProperty,
            Profile.SurfaceResourceKey);
        GraphicalProfileBinding.Bind(
            border,
            Border.BorderBrushProperty,
            Profile.BorderResourceKey);
        return border;
    }

    private static LogicCircuit CreateStarterCircuit()
    {
        var inputA = LogicNode.CreateInput("A", -4, -1, LogicValue.High);
        var inputB = LogicNode.CreateInput("B", -4, 1, LogicValue.High);
        var and = LogicNode.CreateGate(LogicNodeKind.And, "AND", 0, 0);
        var output = LogicNode.CreateOutput("Result", 4, 0);
        return LogicCircuit.Empty
            .AddNode(inputA)
            .AddNode(inputB)
            .AddNode(and)
            .AddNode(output)
            .Connect(
                LogicConnectionId.New(),
                new LogicPortAddress(inputA.Id, "out"),
                new LogicPortAddress(and.Id, "a"))
            .Connect(
                LogicConnectionId.New(),
                new LogicPortAddress(inputB.Id, "out"),
                new LogicPortAddress(and.Id, "b"))
            .Connect(
                LogicConnectionId.New(),
                new LogicPortAddress(and.Id, "out"),
                new LogicPortAddress(output.Id, "in"));
    }
}
