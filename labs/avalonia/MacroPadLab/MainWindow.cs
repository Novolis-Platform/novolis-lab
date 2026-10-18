using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.IO.ConnectedDevices;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace MacroPadLab;

internal sealed class MainWindow : Window
{
    private readonly ConnectedDeviceCatalog _catalog = new();
    private readonly ListBox _deviceList;
    private readonly TextBlock _statusText;
    private readonly Border _statusBar;
    private readonly UniformGrid _actionGrid;
    private readonly StackPanel _knobPanel;
    private readonly ComboBox _actionType;
    private readonly TextBox _parameter1;
    private readonly TextBox _parameter2;
    private readonly TextBox _parameter3;
    private readonly ComboBox _profileBox;
    private readonly Button _readButton;
    private readonly Button _saveButton;
    private readonly Button _profileButton;
    private readonly TextBox _diagnostics;
    private readonly List<Button> _actionButtons = [];
    private readonly List<ConnectedHidDevice> _devices = [];

    private WebHubMacroPad? _target;
    private WebHubMacroPadSession? _session;
    private WebHubMacroPadAction[] _actions =
        Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
    private int _selectedAction = -1;

    public MainWindow()
    {
        Title = "Novolis Macro Pad Lab";
        Width = 1440;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 700;
        Profile.ApplyWindowChrome(this);

        _statusText = new TextBlock
        {
            Text = "Scanning HID interfaces…",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Profile.OnAccentFillBrush,
        };
        _statusBar = new Border
        {
            Background = Profile.AccentFillBrush,
            Padding = new Thickness(12, 7),
            Child = _statusText,
        };

        var refreshButton = CreateButton("Refresh devices", (_, _) => RefreshDevices());
        _deviceList = new ListBox
        {
            Background = Profile.BackgroundBrush,
            BorderBrush = Profile.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            MinHeight = 160,
        };
        _deviceList.SelectionChanged += (_, _) => SelectDevice();

        var devicePanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Heading("Connected HID interfaces"),
                new TextBlock
                {
                    Text = "The highlighted vendor interface is the full 4×4 Keytron configuration channel.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Profile.MutedBrush,
                },
                _deviceList,
                refreshButton,
            },
        };

        _actionGrid = new UniformGrid
        {
            Rows = 4,
            Columns = 4,
            Margin = new Thickness(0, 6, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        for (var index = 0; index < 16; index++)
        {
            var button = CreateActionButton(index, $"Key {index + 1}");
            _actionButtons.Add(button);
            _actionGrid.Children.Add(button);
        }

        _knobPanel = new StackPanel { Spacing = 6 };
        for (var knob = 0; knob < 3; knob++)
        {
            var row = new UniformGrid { Columns = 3 };
            AddActionButton(row, 18 + knob * 3, $"Knob {knob + 1} ←");
            AddActionButton(row, 16 + knob * 3, $"Knob {knob + 1} press");
            AddActionButton(row, 17 + knob * 3, $"Knob {knob + 1} →");
            _knobPanel.Children.Add(row);
        }

        var readInfoButton = CreateButton(
            "Read device (safe probe)",
            (_, _) => ReadConfiguration());
        _readButton = readInfoButton;

        var padPanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Heading("Key and knob actions"),
                new TextBlock
                {
                    Text = "Select a control to edit its four-byte firmware action record.",
                    Foreground = Profile.MutedBrush,
                },
                _actionGrid,
                new TextBlock
                {
                    Text = "Rotary controls",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 4, 0, 0),
                },
                _knobPanel,
                readInfoButton,
            },
        };

        _actionType = new ComboBox
        {
            ItemsSource = Enum.GetValues<WebHubMacroPadActionType>(),
            SelectedItem = WebHubMacroPadActionType.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _parameter1 = CreateHexBox();
        _parameter2 = CreateHexBox();
        _parameter3 = CreateHexBox();
        _profileBox = new ComboBox
        {
            ItemsSource = Enumerable.Range(0, 8).Select(index => $"Profile {index}").ToArray(),
            SelectedIndex = 0,
        };
        _profileButton = CreateButton("Select profile", SelectProfile);
        _saveButton = CreateButton("Write selected action", SaveSelectedAction);

        _diagnostics = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = Profile.MonoFont,
            MinHeight = 230,
            Background = Profile.BackgroundBrush,
            Foreground = Profile.TextBrush,
        };

        var editorPanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Heading("Action editor"),
                new TextBlock
                {
                    Text = "Type-specific parameters are entered as hexadecimal bytes.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Profile.MutedBrush,
                },
                Field("Action type", _actionType),
                Field("Parameter 1", _parameter1),
                Field("Parameter 2", _parameter2),
                Field("Parameter 3", _parameter3),
                _saveButton,
                new Separator { Margin = new Thickness(0, 8) },
                Heading("Stored profile"),
                _profileBox,
                _profileButton,
                new Separator { Margin = new Thickness(0, 8) },
                Heading("Protocol diagnostics"),
                _diagnostics,
            },
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,*,390"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12),
        };
        AddPanel(body, devicePanel, 0);
        AddPanel(body, padPanel, 1);
        AddPanel(body, editorPanel, 2);

        var note = new TextBlock
        {
            Text = "Factory reset and bootloader commands are intentionally not exposed.",
            Foreground = Profile.MutedBrush,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(note, 1);
        Grid.SetColumnSpan(note, 3);
        body.Children.Add(note);

        var root = new DockPanel();
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        root.Children.Add(_statusBar);
        root.Children.Add(body);
        Content = root;

        Closed += (_, _) =>
        {
            _session?.Dispose();
            _catalog.Dispose();
        };
        Opened += (_, _) => RefreshDevices();
    }

    private void RefreshDevices()
    {
        DisposeSession();
        _devices.Clear();
        try
        {
            _devices.AddRange(_catalog.EnumerateHidDevices());
            _deviceList.ItemsSource = _devices
                .Select(DescribeDevice)
                .ToArray();
            _target = WebHubMacroPad.Find(_catalog);

            var targetIndex = _target is null
                ? -1
                : _devices.FindIndex(device =>
                    string.Equals(
                        device.Descriptor.DevicePath,
                        _target.Device.Descriptor.DevicePath,
                        StringComparison.OrdinalIgnoreCase));
            _deviceList.SelectedIndex = targetIndex >= 0
                ? targetIndex
                : (_devices.Count > 0 ? 0 : -1);

            SetStatus(
                _target is null
                    ? "No 6D7D:DCFC full-size macro pad found."
                    : "Keytron found. Select the vendor interface for protocol details.");
            AppendDiagnostic(
                $"Enumerated {_devices.Count} HID interfaces.{Environment.NewLine}" +
                (_target is null
                    ? "Target: not found."
                    : $"Target: {_target.Device.Descriptor.HardwareId} " +
                      $"{_target.Device.Descriptor.ProductName}{Environment.NewLine}" +
                      _target.ProtocolSupportDescription));
        }
        catch (Exception exception)
        {
            SetStatus($"Device scan failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
        }
    }

    private void SelectDevice()
    {
        if (_deviceList.SelectedIndex is < 0 or >= int.MaxValue
            || _deviceList.SelectedIndex >= _devices.Count)
        {
            return;
        }

        var device = _devices[_deviceList.SelectedIndex];
        var descriptor = device.GetReportDescriptor();
        var details =
            $"{device.Descriptor.HardwareId} {device.Descriptor.ProductName}{Environment.NewLine}" +
            $"Path: {device.Descriptor.DevicePath}{Environment.NewLine}" +
            $"Reports: input {descriptor.MaxInputReportLength} B, " +
            $"output {descriptor.MaxOutputReportLength} B, " +
            $"report IDs {(descriptor.ReportsUseId ? "yes" : "no")}{Environment.NewLine}" +
            $"Collections: {string.Join(", ", descriptor.Collections.SelectMany(item => item.Usages).Select(value => $"0x{value:X8}"))}";
        AppendDiagnostic(details);

        var isTarget = _target is not null
            && string.Equals(
                _target.Device.Descriptor.DevicePath,
                device.Descriptor.DevicePath,
                StringComparison.OrdinalIgnoreCase);
        if (isTarget)
        {
            SetStatus(_target!.ProtocolSupportDescription, _target.SupportsWebHubFrames
                ? Profile.AccentFillBrush
                : Profile.WarningBrush);
            _readButton.IsEnabled = true;
            _saveButton.IsEnabled = false;
            _profileButton.IsEnabled = false;
        }
        else
        {
            SetStatus("Select the 6D7D:DCFC vendor configuration interface.");
            _readButton.IsEnabled = false;
            _saveButton.IsEnabled = false;
            _profileButton.IsEnabled = false;
        }
    }

    private void ReadConfiguration()
    {
        if (_target is null)
        {
            return;
        }

        try
        {
            if (!_target.SupportsWebHubFrames)
            {
                var probe = _target.ProbeReadOnly();
                SetStatus(
                    "Read-only probe completed; configuration writes remain disabled.",
                    Profile.WarningBrush);
                AppendDiagnostic(
                    $"PROBE request: {Convert.ToHexString(probe.Request.ToArray())}{Environment.NewLine}" +
                    $"PROBE response (report {probe.ReportId}): " +
                    $"{Convert.ToHexString(probe.Response.ToArray())}{Environment.NewLine}" +
                    _target.ProtocolSupportDescription);
                return;
            }

            DisposeSession();
            _session = _target.OpenSession();
            var info = _session.ReadDeviceInfo();
            if (info.ActiveProfile < _profileBox.ItemCount)
            {
                _profileBox.SelectedIndex = info.ActiveProfile;
            }

            var actions = _session.ReadActions();
            _actions = actions.ToArray();
            for (var index = 0; index < _actions.Length; index++)
            {
                UpdateActionButton(index);
            }

            _saveButton.IsEnabled = true;
            _profileButton.IsEnabled = true;
            SetStatus(
                $"Read {actions.Count} actions from profile {info.ActiveProfile}.",
                Profile.AccentFillBrush);
            AppendDiagnostic(
                $"INFO response: {Convert.ToHexString(info.RawData.ToArray())}{Environment.NewLine}" +
                $"Action record length: {info.ButtonFunctionLength}{Environment.NewLine}" +
                $"Active profile: {info.ActiveProfile}");
        }
        catch (Exception exception)
        {
            DisposeSession();
            SetStatus($"Configuration read failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
        }
    }

    private async void SaveSelectedAction(object? sender, RoutedEventArgs args)
    {
        if (_session is null || _selectedAction < 0)
        {
            return;
        }

        if (!TryReadEditor(out var action))
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "Write macro-pad action?",
            $"Write action {_selectedAction} ({ActionLabel(_selectedAction)}) to the device?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _session.WriteAction(_selectedAction, action, (byte)_profileBox.SelectedIndex);
            var readBack = _session.ReadActions();
            _actions = readBack.ToArray();
            for (var index = 0; index < _actions.Length; index++)
            {
                UpdateActionButton(index);
            }

            SetStatus(
                _actions[_selectedAction] == action
                    ? "Action written and verified."
                    : "Write completed, but read-back differed.",
                _actions[_selectedAction] == action
                    ? Profile.AccentFillBrush
                    : Profile.WarningBrush);
            AppendDiagnostic(
                $"WRITE action {_selectedAction}: {Convert.ToHexString(action.ToByteArray())}{Environment.NewLine}" +
                $"READ-BACK: {Convert.ToHexString(_actions[_selectedAction].ToByteArray())}");
        }
        catch (Exception exception)
        {
            SetStatus($"Action write failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
        }
    }

    private async void SelectProfile(object? sender, RoutedEventArgs args)
    {
        if (_session is null || _profileBox.SelectedIndex < 0)
        {
            return;
        }

        var profile = _profileBox.SelectedIndex;
        if (!await ConfirmAsync("Select stored profile?", $"Switch the device to profile {profile}?"))
        {
            return;
        }

        try
        {
            _session.SelectProfile((byte)profile);
            SetStatus($"Selected profile {profile}.", Profile.AccentFillBrush);
            AppendDiagnostic($"PROFILE SELECT: {profile:X2}");
        }
        catch (Exception exception)
        {
            SetStatus($"Profile selection failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
        }
    }

    private void SelectAction(int index)
    {
        _selectedAction = index;
        var action = _actions[index];
        _actionType.SelectedItem = action.Type;
        _parameter1.Text = action.Parameter1.ToString("X2", CultureInfo.InvariantCulture);
        _parameter2.Text = action.Parameter2.ToString("X2", CultureInfo.InvariantCulture);
        _parameter3.Text = action.Parameter3.ToString("X2", CultureInfo.InvariantCulture);
        SetStatus($"Editing {ActionLabel(index)}.", Profile.AccentFillBrush);
    }

    private bool TryReadEditor(out WebHubMacroPadAction action)
    {
        action = WebHubMacroPadAction.Disabled;
        if (_actionType.SelectedItem is not WebHubMacroPadActionType type
            || !TryReadByte(_parameter1.Text, out var parameter1)
            || !TryReadByte(_parameter2.Text, out var parameter2)
            || !TryReadByte(_parameter3.Text, out var parameter3))
        {
            SetStatus("Enter four valid action bytes.", Profile.WarningBrush);
            return false;
        }

        action = new WebHubMacroPadAction(type, parameter1, parameter2, parameter3);
        return true;
    }

    private void UpdateActionButton(int index)
    {
        if (index < 0 || index >= _actionButtons.Count)
        {
            return;
        }

        _actionButtons[index].Content =
            $"{ActionLabel(index)}{Environment.NewLine}{_actions[index]}";
    }

    private static bool TryReadByte(string? text, out byte value)
    {
        var normalized = (text ?? string.Empty).Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return byte.TryParse(
            normalized,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 430,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        Profile.ApplyWindowChrome(dialog);

        var yes = new Button { Content = "Write", MinWidth = 90 };
        var no = new Button { Content = "Cancel", MinWidth = 90 };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes },
                },
            },
        };

        return await dialog.ShowDialog<bool?>(this) == true;
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
        _saveButton.IsEnabled = false;
        _profileButton.IsEnabled = false;
    }

    private void AppendDiagnostic(string text)
    {
        _diagnostics.Text = string.IsNullOrWhiteSpace(_diagnostics.Text)
            ? text
            : $"{_diagnostics.Text}{Environment.NewLine}{Environment.NewLine}{text}";
        _diagnostics.CaretIndex = _diagnostics.Text?.Length ?? 0;
    }

    private void SetStatus(string text, IBrush? background = null)
    {
        _statusText.Text = text;
        _statusBar.Background = background ?? Profile.ActionSoftBrush;
    }

    private Button CreateActionButton(int index, string label)
    {
        var button = new Button
        {
            Content = label,
            Tag = index,
            MinHeight = 74,
            Margin = new Thickness(3),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Profile.SurfaceBrush,
            Foreground = Profile.TextBrush,
        };
        button.Click += (_, _) => SelectAction(index);
        return button;
    }

    private void AddActionButton(Panel panel, int index, string label)
    {
        var button = CreateActionButton(index, label);
        _actionButtons.Add(button);
        panel.Children.Add(button);
    }

    private static TextBlock Heading(string text) =>
        new()
        {
            Text = text,
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Profile.TextBrush,
        };

    private static Control Field(string label, Control editor) =>
        new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = label, Foreground = Profile.MutedBrush },
                editor,
            },
        };

    private static TextBox CreateHexBox() =>
        new()
        {
            Text = "00",
            PlaceholderText = "00",
            FontFamily = Profile.MonoFont,
        };

    private Button CreateButton(string text, EventHandler<RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Profile.ActionBrush,
            Foreground = Profile.OnActionBrush,
            Padding = new Thickness(10, 7),
        };
        button.Click += handler;
        return button;
    }

    private static void AddPanel(Grid grid, Control control, int column)
    {
        var panel = new Border
        {
            Background = Profile.SurfaceBrush,
            BorderBrush = Profile.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = control,
            },
        };
        Grid.SetColumn(panel, column);
        grid.Children.Add(panel);
    }

    private static string DescribeDevice(ConnectedHidDevice device) =>
        $"{device.Descriptor.HardwareId}  {device.Descriptor.ProductName ?? device.Descriptor.FriendlyName}" +
        $"  (in {device.Descriptor.MaxInputReportLength}, out {device.Descriptor.MaxOutputReportLength})";

    private static string ActionLabel(int index) =>
        index < 16
            ? $"Key {index + 1}"
            : $"Knob {(index - 16) / 3 + 1} " +
              (((index - 16) % 3) switch
              {
                  0 => "press",
                  1 => "→",
                  _ => "←",
              });
}
