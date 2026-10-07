using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

internal sealed class MainWindow : Window
{
    private readonly StudioDocument _studio = new();
    private readonly TextBox _source;
    private readonly TextBox _command;
    private readonly TextBox _ir;
    private readonly TextBlock _stack;
    private readonly TextBlock _status;
    private readonly Image _preview;
    private readonly ListBox _timeline;
    private readonly Slider _wear;
    private VisualStudioTimeline? _history;
    private string _mode = "material";

    public MainWindow()
    {
        Title = "Asset Studio";
        Width = 1280;
        Height = 800;
        FontFamily = GraphicalProfile.BodyFont;
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        _studio.Load(VisualSamples.NavalSteel);

        _stack = new TextBlock
        {
            Text = _studio.Stack,
            FontFamily = GraphicalProfile.BodyFont,
            TextWrapping = TextWrapping.NoWrap,
        };
        GraphicalProfileBinding.Bind(_stack, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);

        _source = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = GraphicalProfile.MonoFont,
            Text = _studio.Source,
        };
        GraphicalProfileBinding.Bind(_source, TextBox.ForegroundProperty, GraphicalProfile.TextResourceKey);
        _source.LostFocus += (_, _) => ApplySource();

        _command = new TextBox { PlaceholderText = "EdgeWear(0.12)  or  Add(PointLight(Red, 1800))" };
        GraphicalProfileBinding.Bind(_command, TextBox.ForegroundProperty, GraphicalProfile.TextResourceKey);

        _ir = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            FontFamily = GraphicalProfile.MonoFont,
        };
        GraphicalProfileBinding.Bind(_ir, TextBox.ForegroundProperty, GraphicalProfile.MutedResourceKey);

        _preview = new Image { Stretch = Stretch.Uniform, Width = 320, Height = 320 };
        _timeline = new ListBox { MinHeight = 120 };
        _wear = new Slider { Minimum = 0, Maximum = 1, Value = 0.08 };
        _wear.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                _studio.FocusName = "ShipSteel";
                _studio.SetParameter("wear", _wear.Value);
                Refresh();
            }
        };

        _status = new TextBlock { Text = "One AST. Stack, source, and commands all edit it." };
        GraphicalProfileBinding.Bind(_status, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);

        Content = BuildLayout();
        Opened += async (_, _) =>
        {
            _history = await VisualStudioTimeline.CreateAsync();
            await _history.SaveAsync(_studio.Source, "Clean");
            Refresh();
        };
    }

    private Control BuildLayout()
    {
        var title = new TextBlock
        {
            Text = "Asset Studio",
            FontSize = 28,
            FontFamily = GraphicalProfile.BodyFont,
            Margin = new Thickness(16, 16, 16, 4),
        };
        GraphicalProfileBinding.Bind(title, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);
        var eyebrow = new TextBlock
        {
            Text = "A visual asset is a small program.",
            Margin = new Thickness(16, 0, 16, 12),
        };
        GraphicalProfileBinding.Bind(eyebrow, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);

        var run = ActionButton("Run command", () =>
        {
            var result = _studio.ApplyCommand(_command.Text ?? string.Empty);
            _status.Text = result.Message;
            Refresh();
        });
        var naval = ActionButton("NavalSteel", () => LoadSample(VisualSamples.NavalSteel, "material"));
        var bolt = ActionButton("RedBolt", () => LoadSample(VisualSamples.RedBolt, "material"));
        var beam = ActionButton("Flashlight + fog", () =>
        {
            _mode = "beam";
            _studio.Load(VisualSamples.MarineFlashlight + Environment.NewLine + VisualSamples.ShipFog);
            Refresh();
        });
        var ship = ActionButton("ShipSteel params", () =>
        {
            _mode = "material";
            _studio.Load(VisualSamples.ShipSteel);
            _studio.FocusName = "ShipSteel";
            Refresh();
        });
        var save = AsyncButton("Save point", async () =>
        {
            if (_history is null)
                return;
            await _history.SaveAsync(_studio.Source, "Edit");
            await RefreshTimelineAsync();
        });
        var branch = AsyncButton("Branch Worn", async () =>
        {
            if (_history is null)
                return;
            var rows = await _history.RowsAsync();
            var head = rows.FirstOrDefault(r => r.IsHead);
            if (head is not null)
                await _history.BranchAsync("Worn", head.Id);
            await RefreshTimelineAsync();
        });

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(12, 0, 12, 8),
            Children = { naval, bolt, beam, ship, save, branch },
        };

        var commandRow = new DockPanel { Margin = new Thickness(12, 0, 12, 8) };
        DockPanel.SetDock(run, Dock.Right);
        commandRow.Children.Add(run);
        commandRow.Children.Add(_command);

        var left = Chrome("Stack", new ScrollViewer { Content = _stack });
        var center = new DockPanel();
        var previewCard = Chrome("Preview", _preview);
        DockPanel.SetDock(previewCard, Dock.Top);
        center.Children.Add(previewCard);
        center.Children.Add(Chrome("Command", commandRow));
        var right = new Grid { RowDefinitions = new RowDefinitions("*,*") };
        var sourceCard = Chrome("Source", _source);
        var irCard = Chrome("Visual IR / C#", _ir);
        Grid.SetRow(irCard, 1);
        right.Children.Add(sourceCard);
        right.Children.Add(irCard);

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*,360"), Margin = new Thickness(12) };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(center);
        columns.Children.Add(right);

        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,220"), Margin = new Thickness(12, 0, 12, 12) };
        var timeCard = Chrome("Timeline branches", _timeline);
        var wearCard = Chrome("ShipSteel wear", _wear);
        Grid.SetColumn(wearCard, 1);
        bottom.Children.Add(timeCard);
        bottom.Children.Add(wearCard);

        var root = new DockPanel();
        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(eyebrow, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        DockPanel.SetDock(bottom, Dock.Bottom);
        _status.Margin = new Thickness(16, 4);
        root.Children.Add(title);
        root.Children.Add(eyebrow);
        root.Children.Add(toolbar);
        root.Children.Add(_status);
        root.Children.Add(bottom);
        root.Children.Add(columns);
        return root;
    }

    private void LoadSample(string source, string mode)
    {
        _mode = mode;
        _studio.Load(source);
        Refresh();
    }

    private void ApplySource()
    {
        if (_studio.TrySetSource(_source.Text ?? string.Empty, out var error))
        {
            _status.Text = "Source applied.";
            Refresh();
        }
        else
            _status.Text = error;
    }

    private void Refresh()
    {
        _stack.Text = _studio.Stack;
        _source.Text = _studio.Source;
        var ir = _studio.Ir;
        var silk = ir.Definitions.Count > 0 ? SilkVisualLowering.Lower(ir.Definitions[0]) : null;
        _ir.Text = (silk is null ? "" : $"Silk mixers: {silk.Mixers.Count}{Environment.NewLine}")
                   + string.Join(Environment.NewLine, ir.Diagnostics.Select(d => d.Message))
                   + Environment.NewLine + _studio.Csharp;
        _preview.Source = ToBitmap(RenderPreview());
    }

    private Rgba32[] RenderPreview()
    {
        if (_mode == "beam")
        {
            var light = _studio.Document.Definitions.FirstOrDefault(d => d.Kind == VisualDefinitionKind.Light);
            var volume = _studio.Document.Definitions.FirstOrDefault(d => d.Kind == VisualDefinitionKind.Volume);
            if (light is not null && volume is not null)
                return BeamComposition.Render(light, volume, 256, 256);
        }

        var material = _studio.Document.Definitions.FirstOrDefault(d => d.Kind == VisualDefinitionKind.Material)
                       ?? new VisualDefinition { Kind = VisualDefinitionKind.Material, Name = "Preview" };
        return SpherePreview.Render(material, 256, 256);
    }

    private async Task RefreshTimelineAsync()
    {
        if (_history is null)
            return;
        var rows = await _history.RowsAsync();
        _timeline.ItemsSource = rows.Select(r => $"{r.Branch}  {r.Label}{(r.IsHead ? "  (head)" : "")}").ToArray();
    }

    private static Button ActionButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 8, 8),
            MinHeight = 42,
            Padding = new Thickness(16, 8),
        };
        GraphicalProfileBinding.Bind(button, Button.BackgroundProperty, GraphicalProfile.ActionResourceKey);
        GraphicalProfileBinding.Bind(button, Button.ForegroundProperty, GraphicalProfile.OnActionResourceKey);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button AsyncButton(string label, Func<Task> onClick)
    {
        var button = ActionButton(label, () => { });
        button.Click += async (_, _) => await onClick();
        return button;
    }

    private Border Chrome(string title, Control child)
    {
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 18,
            Margin = new Thickness(12, 10, 12, 6),
        };
        GraphicalProfileBinding.Bind(heading, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);
        var body = new DockPanel();
        DockPanel.SetDock(heading, Dock.Top);
        body.Children.Add(heading);
        child.Margin = new Thickness(12, 0, 12, 12);
        body.Children.Add(child);
        var border = new Border
        {
            Child = body,
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        };
        GraphicalProfileBinding.Bind(border, Border.BackgroundProperty, GraphicalProfile.SurfaceResourceKey);
        GraphicalProfileBinding.Bind(border, Border.BorderBrushProperty, GraphicalProfile.BorderResourceKey);
        return border;
    }

    private static WriteableBitmap ToBitmap(Rgba32[] pixels)
    {
        const int size = 256;
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
        using var fb = bitmap.Lock();
        var buffer = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length && i < size * size; i++)
        {
            buffer[i * 4] = pixels[i].R;
            buffer[i * 4 + 1] = pixels[i].G;
            buffer[i * 4 + 2] = pixels[i].B;
            buffer[i * 4 + 3] = 255;
        }

        Marshal.Copy(buffer, 0, fb.Address, buffer.Length);
        return bitmap;
    }
}
