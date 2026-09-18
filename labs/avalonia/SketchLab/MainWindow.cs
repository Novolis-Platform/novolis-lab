using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Novolis.Avalonia.Controls.Sketch;
using Optris.Icons.Avalonia;

namespace SketchLab;

internal sealed class MainWindow : Window
{
    static readonly string[] Palette =
    [
        "#1e1e1e", "#e63946", "#f4a261", "#2a9d8f", "#457b9d",
        "#6d597a", "#ffffff", "#adb5bd"
    ];

    readonly SketchControl _sketch;
    readonly TextBlock _status;
    readonly Slider _gridSlider;
    readonly Slider _widthSlider;
    readonly List<ToggleButton> _toolButtons = [];
    readonly List<ToggleButton> _styleButtons = [];
    readonly CheckBox _snapBox;
    readonly CheckBox _meetupBox;
    readonly CheckBox _gridBox;
    readonly CheckBox _fillBox;
    readonly Border _colorPreview;

    public MainWindow()
    {
        Title = "Novolis Sketch Lab";
        Width = 1180;
        Height = 760;
        MinWidth = 720;
        MinHeight = 480;

        _sketch = new SketchControl
        {
            Tool = SketchTool.Pen,
            GridSize = 20,
            GridVisible = true,
            SnapEnabled = true,
            MeetupEnabled = true,
            StrokeColor = "#1e1e1e",
            StrokeWidth = 2
        };
        _sketch.DocumentChanged += RefreshStatus;
        _sketch.SelectionChanged += RefreshStatus;

        _status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.8,
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 12
        };

        _colorPreview = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(4),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = BrushFromHex(_sketch.StrokeColor),
            VerticalAlignment = VerticalAlignment.Center
        };

        _snapBox = Toggle("Snap to grid", true, v => _sketch.SnapEnabled = v);
        _meetupBox = Toggle("Meetup", true, v => _sketch.MeetupEnabled = v);
        _gridBox = Toggle("Grid", true, v => _sketch.GridVisible = v);
        _fillBox = Toggle("Fill", false, v =>
        {
            _sketch.FillEnabled = v;
            RefreshStatus();
        });

        _gridSlider = new Slider
        {
            Minimum = 5,
            Maximum = 80,
            Value = 20,
            Width = 100,
            VerticalAlignment = VerticalAlignment.Center
        };
        _gridSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                _sketch.GridSize = _gridSlider.Value;
        };

        _widthSlider = new Slider
        {
            Minimum = 0.25,
            Maximum = 16,
            Value = 2,
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center
        };
        _widthSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                _sketch.StrokeWidth = _widthSlider.Value;
                RefreshStatus();
            }
        };

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                ToolBtn("fa-solid fa-pen", "Pen", SketchTool.Pen, selected: true),
                ToolBtn("fa-solid fa-slash", "Line", SketchTool.Line),
                ToolBtn("fa-solid fa-bezier-curve", "Spline", SketchTool.Spline),
                ToolBtn("fa-regular fa-square", "Box", SketchTool.Rect),
                ToolBtn("fa-regular fa-circle", "Circle", SketchTool.Ellipse),
                ToolBtn("fa-solid fa-eraser", "Eraser", SketchTool.Eraser),
                ToolBtn("fa-solid fa-mouse-pointer", "Select", SketchTool.Select),
                Sep(),
                IconButton("fa-solid fa-check", "Complete (Enter)", () => _sketch.CompleteDrawing()),
                IconButton("fa-solid fa-draw-polygon", "Close shape (Ctrl+Enter)", () => _sketch.CompleteDrawing(closeShape: true)),
            }
        };

        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        colors.Children.Add(_colorPreview);
        foreach (var hex in Palette)
        {
            var swatch = hex;
            var btn = new Button
            {
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = BrushFromHex(swatch),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };
            ToolTip.SetTip(btn, swatch);
            btn.Click += (_, _) => SetStrokeColor(swatch);
            colors.Children.Add(btn);
        }

        var styles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        styles.Children.Add(StyleBtn("────", "Solid", SketchStrokeStyle.Solid, selected: true));
        styles.Children.Add(StyleBtn("- - -", "Dashed", SketchStrokeStyle.Dashed));
        styles.Children.Add(StyleBtn("····", "Dotted", SketchStrokeStyle.Dotted));
        styles.Children.Add(StyleBtn("-·-", "Dash-dot", SketchStrokeStyle.DashDot));
        styles.Children.Add(StyleBtn("·····", "Stipple", SketchStrokeStyle.Stipple));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                IconButton("fa-solid fa-table-cells", "Gridify", () =>
                {
                    _sketch.GridifySelection();
                    SetStatus("Gridified.");
                }),
                IconButton("fa-solid fa-rotate-left", "Undo", () => _sketch.Undo()),
                IconButton("fa-solid fa-rotate-right", "Redo", () => _sketch.Redo()),
                IconButton("fa-solid fa-trash", "Clear", () =>
                {
                    _sketch.Clear();
                    SetStatus("Cleared.");
                }),
                IconButton("fa-solid fa-image", "Copy PNG", () => _ = CopyPngAsync()),
                IconButton("fa-solid fa-code", "Copy SVG", () => _ = CopySvgAsync()),
            }
        };

        var row1 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 8, 10, 4),
            Children =
            {
                tools,
                Sep(),
                _snapBox,
                _meetupBox,
                _gridBox,
                _fillBox,
                Label("Grid"),
                _gridSlider,
                Label("Width"),
                _widthSlider,
            }
        };

        var row2 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 0, 10, 4),
            Children =
            {
                Label("Color"),
                colors,
                Sep(),
                Label("Stroke"),
                styles,
                Sep(),
                actions,
                _status
            }
        };

        var hint = new TextBlock
        {
            Text = "Line/Spline: Done (Enter) or Close (Ctrl+Enter / click start) · Fill on closed shapes · Stroke swatches for dash/dot/stipple · Width down to 0.25",
            Opacity = 0.6,
            FontSize = 11,
            Margin = new Thickness(12, 0, 12, 6),
            TextWrapping = TextWrapping.Wrap
        };

        var top = new StackPanel { Children = { row1, row2, hint } };
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(_sketch);
        Content = root;

        KeyDown += OnKeyDown;
        RefreshStatus();
    }

    void SetStrokeColor(string hex)
    {
        _sketch.StrokeColor = hex;
        if (_sketch.FillEnabled)
            _sketch.FillColor = hex;
        _colorPreview.Background = BrushFromHex(hex);
        RefreshStatus();
    }

    ToggleButton ToolBtn(string icon, string tip, SketchTool tool, bool selected = false)
    {
        var btn = new ToggleButton
        {
            Width = 36,
            Height = 32,
            IsChecked = selected,
            Content = new Icon { Value = icon, FontSize = 14 }
        };
        ToolTip.SetTip(btn, tip);
        btn.IsCheckedChanged += (_, _) =>
        {
            if (btn.IsChecked != true)
                return;
            foreach (var other in _toolButtons)
            {
                if (!ReferenceEquals(other, btn))
                    other.IsChecked = false;
            }

            _sketch.Tool = tool;
            RefreshStatus();
        };
        _toolButtons.Add(btn);
        return btn;
    }

    ToggleButton StyleBtn(string glyph, string tip, SketchStrokeStyle style, bool selected = false)
    {
        var btn = new ToggleButton
        {
            MinWidth = 36,
            Height = 28,
            Padding = new Thickness(6, 0),
            IsChecked = selected,
            Content = new TextBlock
            {
                Text = glyph,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        ToolTip.SetTip(btn, tip);
        btn.IsCheckedChanged += (_, _) =>
        {
            if (btn.IsChecked != true)
                return;
            foreach (var other in _styleButtons)
            {
                if (!ReferenceEquals(other, btn))
                    other.IsChecked = false;
            }

            _sketch.StrokeStyle = style;
            RefreshStatus();
        };
        _styleButtons.Add(btn);
        return btn;
    }

    static Button IconButton(string icon, string tip, Action action)
    {
        var b = new Button
        {
            Width = 36,
            Height = 32,
            Content = new Icon { Value = icon, FontSize = 14 },
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    static CheckBox Toggle(string label, bool on, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = on,
            VerticalAlignment = VerticalAlignment.Center
        };
        box.IsCheckedChanged += (_, _) => set(box.IsChecked == true);
        return box;
    }

    static TextBlock Label(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.75,
        FontSize = 12
    };

    static Control Sep() => new Border
    {
        Width = 1,
        Background = Brushes.Gray,
        Opacity = 0.35,
        Margin = new Thickness(4, 2)
    };

    static IBrush BrushFromHex(string hex)
    {
        try { return new SolidColorBrush(Color.Parse(hex)); }
        catch { return Brushes.Black; }
    }

    async Task CopyPngAsync()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            SetStatus("No clipboard.");
            return;
        }

        try
        {
            var png = SketchExport.ToPng(doc, opaqueBackground: true);
            await using var stream = new MemoryStream(png);
            var bitmap = new Bitmap(stream);
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            item.Set(DataFormat.CreateBytesPlatformFormat("PNG"), png);
            item.Set(DataFormat.CreateBytesPlatformFormat("image/png"), png);
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data);
            SetStatus($"Copied PNG ({png.Length:N0} bytes).");
        }
        catch (Exception ex)
        {
            SetStatus($"PNG copy failed: {ex.Message}");
        }
    }

    async Task CopySvgAsync()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            SetStatus("No clipboard.");
            return;
        }

        try
        {
            var svg = SketchExport.ToSvg(doc);
            await clipboard.SetTextAsync(svg);
            SetStatus($"Copied SVG ({svg.Length:N0} chars).");
        }
        catch (Exception ex)
        {
            SetStatus($"SVG copy failed: {ex.Message}");
        }
    }

    IClipboard? GetClipboard() => TopLevel.GetTopLevel(this)?.Clipboard;

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
        {
            _sketch.Undo();
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
        {
            _sketch.Redo();
            e.Handled = true;
        }
    }

    void RefreshStatus()
    {
        var doc = _sketch.Document;
        if (doc is null)
        {
            _status.Text = "";
            return;
        }

        _status.Text =
            $"{_sketch.Tool} · {_sketch.StrokeStyle} · {doc.Elements.Count} · sel {doc.Selection.Count} · {_sketch.StrokeColor} · w{_sketch.StrokeWidth:0.##}"
            + (_sketch.FillEnabled ? " · fill" : "");
    }

    void SetStatus(string text) => _status.Text = text;
}
