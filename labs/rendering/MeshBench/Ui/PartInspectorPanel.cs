using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MeshBench.Models;

namespace MeshBench.Ui;

internal sealed class PartInspectorPanel : Border
{
    private readonly TextBlock _emptyHint = new()
    {
        Text = "Select a mesh to edit position, size, and color.",
        Opacity = 0.65,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(8),
    };

    private readonly StackPanel _editor = new() { Spacing = 6, Margin = new Thickness(8) };
    private readonly TextBox _name = new();
    private readonly TextBox _cx = new();
    private readonly TextBox _cy = new();
    private readonly TextBox _cz = new();
    private readonly TextBox _sx = new();
    private readonly TextBox _sy = new();
    private readonly TextBox _sz = new();
    private readonly TextBox _radius = new();
    private readonly StackPanel _boxSizePanel = new() { Spacing = 4 };
    private readonly StackPanel _spherePanel = new() { Spacing = 4 };
    private readonly Slider _cr = new() { Minimum = 0, Maximum = 1, Width = 140 };
    private readonly Slider _cg = new() { Minimum = 0, Maximum = 1, Width = 140 };
    private readonly Slider _cb = new() { Minimum = 0, Maximum = 1, Width = 140 };

    private MeshPartRecord? _part;
    private bool _suppress;
    private DispatcherTimer? _debounce;

    public event EventHandler? PartChanged;

    public PartInspectorPanel()
    {
        Child = new DockPanel
        {
            Children =
            {
                _emptyHint,
                _editor,
            },
        };

        BuildEditor();
        ShowEmpty();
    }

    public void Bind(MeshPartRecord? part)
    {
        _part = part;
        if (part is null)
        {
            ShowEmpty();
            return;
        }

        _emptyHint.IsVisible = false;
        _editor.IsVisible = true;
        _suppress = true;
        _name.Text = part.Name;
        _cx.Text = F(part.Center, 0);
        _cy.Text = F(part.Center, 1);
        _cz.Text = F(part.Center, 2);
        _sx.Text = F(part.HalfExtents, 0);
        _sy.Text = F(part.HalfExtents, 1);
        _sz.Text = F(part.HalfExtents, 2);
        _radius.Text = part.Radius.ToString("0.###");
        var isSphere = part.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase);
        _boxSizePanel.IsVisible = !isSphere;
        _spherePanel.IsVisible = isSphere;
        _cr.Value = part.Color.Length > 0 ? part.Color[0] : 0.5;
        _cg.Value = part.Color.Length > 1 ? part.Color[1] : 0.5;
        _cb.Value = part.Color.Length > 2 ? part.Color[2] : 0.5;
        _suppress = false;
    }

    private void BuildEditor()
    {
        _editor.Children.Add(Labeled("Name", _name));
        _editor.Children.Add(LabeledRow("Center X", _cx, "Y", _cy, "Z", _cz));
        _boxSizePanel.Children.Add(LabeledRow("Size X", _sx, "Y", _sy, "Z", _sz));
        _spherePanel.Children.Add(Labeled("Radius", _radius));
        _editor.Children.Add(_boxSizePanel);
        _editor.Children.Add(_spherePanel);
        _editor.Children.Add(Labeled("Color R", _cr));
        _editor.Children.Add(Labeled("Color G", _cg));
        _editor.Children.Add(Labeled("Color B", _cb));

        void Wire(TextBox box) => box.LostFocus += (_, _) => CommitFromFields();
        Wire(_name);
        Wire(_cx);
        Wire(_cy);
        Wire(_cz);
        Wire(_sx);
        Wire(_sy);
        Wire(_sz);
        Wire(_radius);

        _cr.PropertyChanged += OnColorSlider;
        _cg.PropertyChanged += OnColorSlider;
        _cb.PropertyChanged += OnColorSlider;
    }

    private void OnColorSlider(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_suppress || e.Property != Slider.ValueProperty || _part is null)
            return;

        EnsureColor(_part);
        _part.Color[0] = (float)_cr.Value;
        _part.Color[1] = (float)_cg.Value;
        _part.Color[2] = (float)_cb.Value;
        SchedulePartChanged();
    }

    private void SchedulePartChanged()
    {
        _debounce ??= new DispatcherTimer(TimeSpan.FromMilliseconds(90), DispatcherPriority.Background, (_, _) =>
        {
            _debounce?.Stop();
            PartChanged?.Invoke(this, EventArgs.Empty);
        });
        _debounce.Stop();
        _debounce.Start();
    }

    private void CommitFromFields()
    {
        if (_suppress || _part is null)
            return;

        _part.Name = _name.Text?.Trim() is { Length: > 0 } n ? n : _part.Kind;
        SetAxis(_part.Center, _cx, _cy, _cz);
        if (_part.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(_radius.Text, out var r))
                _part.Radius = Math.Clamp(r, 0.05f, 8f);
        }
        else
        {
            SetAxis(_part.HalfExtents, _sx, _sy, _sz);
            for (var i = 0; i < 3; i++)
                _part.HalfExtents[i] = Math.Clamp(_part.HalfExtents[i], 0.05f, 8f);
        }

        SchedulePartChanged();
    }

    private void ShowEmpty()
    {
        _emptyHint.IsVisible = true;
        _editor.IsVisible = false;
    }

    private static Control Labeled(string label, Control input)
    {
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.8 });
        row.Children.Add(input);
        return row;
    }

    private static Control LabeledRow(string l1, Control i1, string l2, Control i2, string l3, Control i3)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 6,
        };
        AddCol(grid, 0, l1, i1);
        AddCol(grid, 1, l2, i2);
        AddCol(grid, 2, l3, i3);
        return grid;
    }

    private static void AddCol(Panel grid, int col, string label, Control input)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.8 });
        stack.Children.Add(input);
        Grid.SetColumn(stack, col);
        grid.Children.Add(stack);
    }

    private static string F(float[] values, int index) =>
        index < values.Length ? values[index].ToString("0.###") : "0";

    private static void SetAxis(float[] target, TextBox x, TextBox y, TextBox z)
    {
        if (target.Length < 3)
            return;
        if (float.TryParse(x.Text, out var vx))
            target[0] = vx;
        if (float.TryParse(y.Text, out var vy))
            target[1] = vy;
        if (float.TryParse(z.Text, out var vz))
            target[2] = vz;
    }

    private static void EnsureColor(MeshPartRecord part)
    {
        if (part.Color.Length >= 3)
            return;
        part.Color = [0.7f, 0.7f, 0.7f];
    }
}
