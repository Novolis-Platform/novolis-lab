using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
    private readonly TextBlock _actionSummary;
    private readonly ComboBox _actionType;
    private readonly ComboBox _legacyActionType;
    private readonly TextBlock _parameter1Label;
    private readonly TextBlock _parameter2Label;
    private readonly TextBlock _parameter3Label;
    private readonly ComboBox _modifierChoice;
    private readonly ComboBox _keyboardUsageChoice;
    private readonly ComboBox _consumerUsageChoice;
    private readonly Control _modifierChoiceField;
    private readonly Control _keyboardUsageChoiceField;
    private readonly Control _consumerUsageChoiceField;
    private readonly IReadOnlyList<ActionTypeChoice<WebHubMacroPadActionType>> _actionTypeChoices;
    private readonly IReadOnlyList<ActionTypeChoice<KeytronLegacyActionType>> _legacyActionTypeChoices;
    private readonly TextBox _parameter1;
    private readonly TextBox _parameter2;
    private readonly TextBox _parameter3;
    private readonly ComboBox _profileBox;
    private readonly Button _readButton;
    private readonly Button _saveButton;
    private readonly Button _writeAllButton;
    private readonly Button _stageSafeButton;
    private readonly Button _profileButton;
    private readonly ComboBox _lightingMode;
    private readonly Slider _lightingBrightness;
    private readonly Slider _lightingSpeed;
    private readonly ColorPicker _lightingColor;
    private readonly TextBlock _lightingBrightnessValue;
    private readonly TextBlock _lightingSpeedValue;
    private readonly CheckBox _lightingCustom;
    private readonly TextBlock _lightingSummary;
    private readonly Button _readLightingButton;
    private readonly Button _writeLightingButton;
    private readonly TextBox _diagnostics;
    private readonly TextBox _rawKvp;
    private readonly TextBox _activityLog;
    private readonly Button _copyRawButton;
    private readonly Button _copyActivityButton;
    private readonly Button _clearActivityButton;
    private readonly List<Button> _actionButtons = [];
    private readonly List<ConnectedHidDevice> _devices = [];
    private readonly List<ConnectedHidInputReader> _inputReaders = [];
    private readonly List<Task> _inputMonitorTasks = [];

    private KeytronRawInputMonitor? _rawInputMonitor;
    private WebHubMacroPad? _target;
    private WebHubMacroPadSession? _session;
    private KeytronLegacySession? _legacySession;
    private WebHubMacroPadAction[] _actions =
        Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
    private WebHubMacroPadAction[] _loadedActions =
        Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
    private WebHubMacroPadDeviceInfo? _deviceInfo;
    private KeytronLegacyDeviceInfo? _legacyInfo;
    private WebHubMacroPadProbeResult? _probe;
    private ConnectedHidReportDescriptor? _targetReportDescriptor;
    private byte[]? _rawReportDescriptor;
    private KeytronLegacyAction[] _legacyActions = [];
    private KeytronLegacyAction[] _loadedLegacyActions = [];
    private KeytronLegacyLightingResponse? _legacyLighting;
    private KeytronLegacyLightingSettings _lightingSettings =
        KeytronLegacyLightingSettings.Default;
    private CancellationTokenSource? _inputMonitorCancellation;
    private bool _updatingInputChoices;
    private bool _closing;
    private bool _legacyMode;
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

        var refreshButton = CreateButton("Refresh Keytron", (_, _) => RefreshDevices());
        _deviceList = new ListBox
        {
            Background = Profile.BackgroundBrush,
            BorderBrush = Profile.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            MinHeight = 100,
        };
        _deviceList.SelectionChanged += (_, _) => SelectDevice();

        var devicePanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Heading("Keytron target"),
                new TextBlock
                {
                    Text = "Only the exact full 4×4 Keytron target (VID:PID 6D7D:DCFC) is considered. " +
                        "Other HID devices are intentionally ignored.",
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
            AddActionButton(row, 18 + knob * 3, $"Knob {knob + 1}\nCCW");
            AddActionButton(row, 16 + knob * 3, $"Knob {knob + 1} press");
            AddActionButton(row, 17 + knob * 3, $"Knob {knob + 1}\nCW");
            _knobPanel.Children.Add(row);
        }

        _actionSummary = new TextBlock
        {
            Text = "Select a key or knob control to inspect its action.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Profile.ActionBrush,
            FontWeight = FontWeight.Bold,
        };

        var readInfoButton = CreateButton(
            "Read Keytron",
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
                    Text = "This view is the Keytron's 25 action slots: 16 keys plus three controls " +
                        "per knob. Direction is shown as clockwise (CW) and counter-clockwise (CCW).",
                    TextWrapping = TextWrapping.Wrap,
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

        _actionTypeChoices = Enum.GetValues<WebHubMacroPadActionType>()
            .Select(type => new ActionTypeChoice<WebHubMacroPadActionType>(
                type,
                WebHubMacroPadActionDescriptions.DescribeType(type)))
            .ToArray();
        _legacyActionTypeChoices = Enum.GetValues<KeytronLegacyActionType>()
            .Select(type => new ActionTypeChoice<KeytronLegacyActionType>(
                type,
                KeytronLegacyActionDescriptions.DescribeType(type)))
            .ToArray();
        _actionType = new ComboBox
        {
            ItemsSource = _actionTypeChoices,
            SelectedItem = _actionTypeChoices.First(
                choice => choice.Value == WebHubMacroPadActionType.Disabled),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _legacyActionType = new ComboBox
        {
            ItemsSource = _legacyActionTypeChoices,
            SelectedItem = _legacyActionTypeChoices.First(
                choice => choice.Value == KeytronLegacyActionType.Disabled),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
        };
        _actionType.SelectionChanged += (_, _) => UpdateParameterLabels();
        _legacyActionType.SelectionChanged += (_, _) => UpdateParameterLabels();
        _parameter1 = CreateHexBox();
        _parameter2 = CreateHexBox();
        _parameter3 = CreateHexBox();
        _parameter1Label = CreateParameterLabel();
        _parameter2Label = CreateParameterLabel();
        _parameter3Label = CreateParameterLabel();
        _modifierChoice = new ComboBox
        {
            ItemsSource = CreateModifierChoices(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _keyboardUsageChoice = new ComboBox
        {
            ItemsSource = CreateKeyboardUsageChoices(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _consumerUsageChoice = new ComboBox
        {
            ItemsSource = CreateConsumerUsageChoices(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _modifierChoiceField = Field("Modifier preset", _modifierChoice);
        _keyboardUsageChoiceField = Field("Named keyboard key", _keyboardUsageChoice);
        _consumerUsageChoiceField = Field("Named media/control action", _consumerUsageChoice);
        _modifierChoice.SelectionChanged += (_, _) => ApplyNamedInputChoices();
        _keyboardUsageChoice.SelectionChanged += (_, _) => ApplyNamedInputChoices();
        _consumerUsageChoice.SelectionChanged += (_, _) => ApplyNamedInputChoices();
        UpdateParameterLabels();
        _profileBox = new ComboBox
        {
            ItemsSource = Enumerable.Range(0, 8).Select(index => $"Profile {index}").ToArray(),
            SelectedIndex = 0,
        };
        _lightingMode = new ComboBox
        {
            ItemsSource = CreateLightingModeChoices(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _lightingBrightness = CreateLightingSlider();
        _lightingSpeed = CreateLightingSlider();
        _lightingColor = new ColorPicker
        {
            Color = Colors.Red,
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            IsColorPaletteVisible = true,
            IsColorSpectrumVisible = true,
            IsColorComponentsVisible = true,
            IsComponentSliderVisible = true,
            IsComponentTextInputVisible = false,
            IsHexInputVisible = false,
            IsColorModelVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _lightingBrightnessValue = CreateLightingValueLabel();
        _lightingSpeedValue = CreateLightingValueLabel();
        _lightingCustom = new CheckBox
        {
            Content = "Use the selected color",
            IsChecked = true,
            Foreground = Profile.TextBrush,
        };
        _lightingSummary = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Profile.ActionBrush,
        };
        _lightingMode.SelectionChanged += (_, _) => UpdateLightingEditorSummary();
        _lightingBrightness.ValueChanged += (_, _) => UpdateLightingEditorSummary();
        _lightingSpeed.ValueChanged += (_, _) => UpdateLightingEditorSummary();
        _lightingColor.ColorChanged += (_, _) => UpdateLightingEditorSummary();
        _readLightingButton = CreateButton("Read lighting from Keytron", ReadLighting);
        _writeLightingButton = CreateButton("Apply lighting to Keytron", WriteLighting);
        ApplyLightingSettingsToEditor();
        devicePanel.Children.Add(new Separator { Margin = new Thickness(0, 8) });
        devicePanel.Children.Add(Heading("RGB lighting"));
        devicePanel.Children.Add(
            new TextBlock
            {
                Text =
                    "Device-wide lighting belongs to the connected Keytron, not to an " +
                    "individual key. Effect selects the pattern, brightness is the master " +
                    "LED level, speed affects animated effects, and custom color selects " +
                    "the selected color. The firmware details stay in Raw KVP.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Profile.MutedBrush,
            });
        devicePanel.Children.Add(_lightingSummary);
        devicePanel.Children.Add(Field("Lighting effect", _lightingMode));
        devicePanel.Children.Add(
            Field(
                "Brightness",
                LightingSliderField(_lightingBrightness, _lightingBrightnessValue)));
        devicePanel.Children.Add(
            Field(
                "Animation speed",
                LightingSliderField(_lightingSpeed, _lightingSpeedValue)));
        devicePanel.Children.Add(Field("Color", _lightingColor));
        devicePanel.Children.Add(_lightingCustom);
        var lightingButtons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            Children =
            {
                _readLightingButton,
                _writeLightingButton,
            },
        };
        Grid.SetColumn(_writeLightingButton, 1);
        devicePanel.Children.Add(lightingButtons);
        _profileButton = CreateButton("Select profile", SelectProfile);
        _saveButton = CreateButton("Write selected action", SaveSelectedAction);
        _stageSafeButton = CreateButton("Stage safe testing map", StageSafeTestingMap);
        _writeAllButton = CreateButton("Write staged changes", WriteStagedActions);

        _diagnostics = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = Profile.MonoFont,
            MinHeight = 450,
            Background = Profile.BackgroundBrush,
            Foreground = Profile.TextBrush,
        };

        _rawKvp = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = Profile.MonoFont,
            MinHeight = 450,
            Background = Profile.BackgroundBrush,
            Foreground = Profile.TextBrush,
        };
        _copyRawButton = CreateButton("Copy raw KVP", CopyRawKvp);
        _activityLog = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = Profile.MonoFont,
            MinHeight = 120,
            Background = Profile.BackgroundBrush,
            Foreground = Profile.TextBrush,
        };
        _copyActivityButton = CreateButton("Copy raw log", CopyActivityLog);
        _copyActivityButton.HorizontalAlignment = HorizontalAlignment.Left;
        _clearActivityButton = CreateButton("Clear raw log", ClearActivityLog);
        _clearActivityButton.HorizontalAlignment = HorizontalAlignment.Left;

        var actionEditorPanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Heading("Action editor"),
                new TextBlock
                {
                    Text = "The friendly action view is backed by the device's raw action bytes. " +
                        "The current factory firmware stores three bytes per slot; newer WebHub " +
                        "firmware stores four. Parameter labels follow the selected action type, " +
                        "while raw bytes remain visible. Writes are explicit and read back for verification.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Profile.MutedBrush,
                },
                _actionSummary,
                Field("Action type", _actionType),
                Field("Factory action type", _legacyActionType),
                Field(_parameter1Label, _parameter1),
                Field(_parameter2Label, _parameter2),
                Field(_parameter3Label, _parameter3),
                _modifierChoiceField,
                _keyboardUsageChoiceField,
                _consumerUsageChoiceField,
                _saveButton,
                _stageSafeButton,
                _writeAllButton,
                new Separator { Margin = new Thickness(0, 8) },
                Heading("Stored profile"),
                _profileBox,
                _profileButton,
            },
        };

        var rawPanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Copyable key/value evidence for this exact Keytron interface. " +
                        "It includes HID shape, raw descriptor, protocol responses, and every action record.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Profile.MutedBrush,
                },
                _copyRawButton,
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _rawKvp,
                },
            },
        };

        var wirePanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Protocol activity and failures. No other HID traffic is shown here.",
                    Foreground = Profile.MutedBrush,
                },
                _diagnostics,
            },
        };

        var detailTabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Action editor", Content = actionEditorPanel },
                new TabItem { Header = "Raw KVP", Content = rawPanel },
                new TabItem { Header = "Wire log", Content = wirePanel },
            },
        };

        var activityToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Heading("Raw activity log"),
                _copyActivityButton,
                _clearActivityButton,
            },
        };
        var activityPanel = new Border
        {
            Background = Profile.SurfaceBrush,
            BorderBrush = Profile.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    activityToolbar,
                    _activityLog,
                },
            },
        };
        DockPanel.SetDock(activityToolbar, Dock.Top);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,*,390"),
            RowDefinitions = new RowDefinitions("*,180,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12),
        };
        AddPanel(body, devicePanel, 0);
        AddPanel(body, padPanel, 1);
        AddPanel(body, detailTabs, 2);
        Grid.SetRow(activityPanel, 1);
        Grid.SetColumnSpan(activityPanel, 3);
        body.Children.Add(activityPanel);

        var note = new TextBlock
        {
            Text = "Factory reset and bootloader commands are intentionally not exposed.",
            Foreground = Profile.MutedBrush,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(note, 2);
        Grid.SetColumnSpan(note, 3);
        body.Children.Add(note);

        var root = new DockPanel();
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        root.Children.Add(_statusBar);
        root.Children.Add(body);
        Content = root;

        Closed += (_, _) =>
        {
            _closing = true;
            StopRawInputMonitoring();
            StopInputMonitoring();
            _session?.Dispose();
            _legacySession?.Dispose();
            _catalog.Dispose();
        };
        Opened += (_, _) => RefreshDevices();
    }

    private void RefreshDevices()
    {
        StopRawInputMonitoring();
        StopInputMonitoring();
        DisposeSession();
        _devices.Clear();
        _target = null;
        _deviceInfo = null;
        _legacyInfo = null;
        _probe = null;
        _targetReportDescriptor = null;
        _rawReportDescriptor = null;
        _legacyMode = false;
        _legacyActions = [];
        _loadedLegacyActions = [];
        _legacyLighting = null;
        _lightingSettings = KeytronLegacyLightingSettings.Default;
        ApplyLightingSettingsToEditor();
        _actions = Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
        _loadedActions = Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
        _actionType.IsVisible = true;
        _legacyActionType.IsVisible = false;
        _profileBox.IsVisible = true;
        _profileButton.IsVisible = true;
        SetActionControlsEnabled(false);
        SetLightingControlsEnabled(false);
        try
        {
            // This lab is deliberately not a generic HID browser. It only
            // considers the known full-size Keytron target.
            _target = WebHubMacroPad.Find(_catalog);
            if (_target is not null)
            {
                _devices.Add(_target.Device);
            }

            _deviceList.ItemsSource = _devices
                .Select(DescribeDevice)
                .ToArray();
            _deviceList.SelectedIndex = _target is null ? -1 : 0;

            SetStatus(
                _target is null
                    ? "The exact 6D7D:DCFC Keytron is not connected."
                    : "The exact 6D7D:DCFC Keytron is connected.");
            AppendDiagnostic(
                $"Target scan: exact VID:PID 6D7D:DCFC only.{Environment.NewLine}" +
                (_target is null
                    ? "Target: not found."
                    : $"Target: {_target.Device.Descriptor.HardwareId} " +
                      $"{_target.Device.Descriptor.ProductName}{Environment.NewLine}" +
                      _target.ProtocolSupportDescription));
            StartInputMonitoring();
            StartRawInputMonitoring();
        }
        catch (Exception exception)
        {
            SetStatus($"Device scan failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
        }

        UpdateRawKvp();
    }

    private void StartRawInputMonitoring()
    {
        if (_target is null || !OperatingSystem.IsWindows())
        {
            return;
        }

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is null)
        {
            AppendActivity("RAWINPUT monitor unavailable: the lab window has no native handle.");
            return;
        }

        try
        {
            _rawInputMonitor = KeytronRawInputMonitor.Start(
                platformHandle.Handle,
                WebHubMacroPad.VendorId,
                WebHubMacroPad.ProductId,
                AppendActivity);
            AppendActivity(
                "RAWINPUT monitor registered for this exact Keytron " +
                "(keyboard, mouse, and consumer-control usages).");
        }
        catch (Exception exception)
        {
            AppendActivity($"RAWINPUT monitor unavailable: {exception.Message}");
        }
    }

    private void StopRawInputMonitoring()
    {
        _rawInputMonitor?.Dispose();
        _rawInputMonitor = null;
    }

    private void StartInputMonitoring()
    {
        if (_target is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _inputMonitorCancellation = cancellation;
        var configurationPath = _target.Device.Descriptor.DevicePath;
        IReadOnlyList<ConnectedHidDevice> candidates;
        try
        {
            candidates = _catalog.EnumerateHidDevices(
                new ConnectedDeviceFilter
                {
                    VendorId = WebHubMacroPad.VendorId,
                    ProductId = WebHubMacroPad.ProductId,
                });
        }
        catch (Exception exception)
        {
            AppendActivity($"INPUT monitor enumeration failed: {exception.Message}");
            return;
        }

        var monitored = 0;
        foreach (var candidate in candidates)
        {
            if (string.Equals(
                    candidate.Descriptor.DevicePath,
                    configurationPath,
                    StringComparison.OrdinalIgnoreCase)
                || !IsUserInputInterface(candidate))
            {
                continue;
            }

            try
            {
                var reader = candidate.OpenInputReader();
                _inputReaders.Add(reader);
                monitored++;
                AppendActivity(
                    $"INPUT monitor opened: {DescribeInputInterfaceSafe(candidate)}");
                _inputMonitorTasks.Add(
                    Task.Run(
                        () => MonitorInputAsync(candidate, reader, cancellation.Token),
                        cancellation.Token));
            }
            catch (Exception exception)
            {
                AppendActivity(
                    $"INPUT monitor unavailable: {DescribeInputInterfaceSafe(candidate)}; " +
                    $"{exception.Message}");
            }
        }

        if (monitored == 0)
        {
            AppendActivity(
                "INPUT monitor found no readable keyboard, consumer-control, or mouse " +
                "interface for this exact Keytron.");
        }
    }

    private void StopInputMonitoring()
    {
        _inputMonitorCancellation?.Cancel();
        _inputMonitorCancellation?.Dispose();
        _inputMonitorCancellation = null;
        foreach (var reader in _inputReaders)
        {
            reader.Dispose();
        }

        _inputReaders.Clear();
        _inputMonitorTasks.Clear();
    }

    private async Task MonitorInputAsync(
        ConnectedHidDevice device,
        ConnectedHidInputReader reader,
        CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested && reader.IsRunning)
            {
                if (!reader.WaitForInput(TimeSpan.FromMilliseconds(250)))
                {
                    continue;
                }

                while (!cancellation.IsCancellationRequested
                    && reader.TryRead(out var report)
                    && report is not null)
                {
                    AppendActivity(
                        $"INPUT {DescribeInputInterface(device)} " +
                        $"reportId={report.ReportId} " +
                        $"raw={Convert.ToHexString(report.ToArray())} " +
                        $"values={DescribeInputValues(report)}");
                }
            }
        }
        catch (Exception exception) when (
            cancellation.IsCancellationRequested
                || exception is ObjectDisposedException
                || exception is IOException)
        {
        }
        catch (Exception exception)
        {
            AppendActivity(
                $"INPUT monitor stopped for {DescribeInputInterfaceSafe(device)}: " +
                exception.Message);
        }

        await Task.CompletedTask;
    }

    private static bool IsUserInputInterface(ConnectedHidDevice device)
    {
        try
        {
            var descriptor = device.GetReportDescriptor();
            return descriptor.MaxInputReportLength > 0
                && descriptor.Collections.Any(
                    collection => collection.Usages.Any(IsUserInputUsage));
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or NotSupportedException
                or ObjectDisposedException)
        {
            return false;
        }
    }

    private static bool IsUserInputUsage(uint usage) =>
        usage is 0x00010001
            or 0x00010002
            or 0x00010006
            or 0x000C0001;

    private static string DescribeInputInterface(ConnectedHidDevice device)
    {
        var descriptor = device.GetReportDescriptor();
        var usages = descriptor.Collections
            .SelectMany(collection => collection.Usages)
            .Where(IsUserInputUsage)
            .Distinct()
            .Select(DescribeUsage)
            .ToArray();
        return $"{device.Descriptor.HardwareId} " +
            $"{device.Descriptor.ProductName ?? device.Descriptor.FriendlyName} " +
            $"[{string.Join(", ", usages)}]";
    }

    private static string DescribeInputInterfaceSafe(ConnectedHidDevice device)
    {
        try
        {
            return DescribeInputInterface(device);
        }
        catch (Exception)
        {
            return DescribeDevice(device);
        }
    }

    private static string DescribeInputValues(ConnectedHidInputReport report)
    {
        if (report.Values.Count == 0)
        {
            return "none";
        }

        return string.Join(
            "; ",
            report.Values.Select(
                value =>
                {
                    var usages = value.Usages.Count == 0
                        ? "no-usage"
                        : string.Join(",", value.Usages.Select(DescribeUsage));
                    var logical = value.LogicalValue?.ToString(CultureInfo.InvariantCulture)
                        ?? (value.IsNull ? "null" : "invalid");
                    return $"{usages}=logical:{logical}";
                }));
    }

    private static string DescribeUsage(uint usage)
    {
        var page = (ushort)(usage >> 16);
        var id = (byte)usage;
        return page switch
        {
            0x0007 => DescribeKeyboardUsage(id),
            0x0009 => $"Button {id}",
            0x000C => DescribeConsumerUsage((ushort)usage),
            0x0001 => id switch
            {
                0x30 => "X axis",
                0x31 => "Y axis",
                0x32 => "Z axis",
                0x37 => "Dial",
                0x38 => "Wheel",
                _ => $"Generic desktop 0x{id:X2}",
            },
            _ => $"HID usage 0x{usage:X8}",
        };
    }

    private static string DescribeKeyboardUsage(byte usage)
        => usage switch
        {
            >= 0x04 and <= 0x1D => ((char)('A' + usage - 0x04)).ToString(),
            >= 0x1E and <= 0x27 => usage == 0x27
                ? "0"
                : ((char)('1' + usage - 0x1E)).ToString(),
            0x28 => "Enter",
            0x29 => "Escape",
            0x2A => "Backspace",
            0x2B => "Tab",
            0x2C => "Space",
            0x2D => "Minus",
            0x2E => "Equals",
            0x2F => "Left bracket",
            0x30 => "Right bracket",
            0x31 => "Backslash",
            0x32 => "Non-US hash / tilde",
            0x33 => "Semicolon",
            0x34 => "Apostrophe",
            0x35 => "Grave accent",
            0x36 => "Comma",
            0x37 => "Period",
            0x38 => "Slash",
            0x39 => "Caps Lock",
            >= 0x3A and <= 0x45 => $"F{usage - 0x39}",
            0x46 => "Print Screen",
            0x47 => "Scroll Lock",
            0x48 => "Pause",
            0x49 => "Insert",
            0x4A => "Home",
            0x4B => "Page Up",
            0x4C => "Delete",
            0x4D => "End",
            0x4E => "Page Down",
            0x4F => "Right arrow",
            0x50 => "Left arrow",
            0x51 => "Down arrow",
            0x52 => "Up arrow",
            0x53 => "Num Lock",
            0x54 => "Keypad divide",
            0x55 => "Keypad multiply",
            0x56 => "Keypad subtract",
            0x57 => "Keypad add",
            0x58 => "Keypad Enter",
            >= 0x59 and <= 0x62 => $"Keypad {(usage - 0x59 + 1) % 10}",
            0x63 => "Keypad decimal",
            0x64 => "Non-US backslash",
            0x65 => "Application / context menu",
            0x66 => "Power",
            0x67 => "Keypad equals",
            >= 0x68 and <= 0x73 => $"F{usage - 0x5B}",
            0x74 => "Execute",
            0x75 => "Help",
            0x76 => "Menu",
            0x77 => "Select",
            0x78 => "Stop",
            0x79 => "Again",
            0x7A => "Undo",
            0x7B => "Cut",
            0x7C => "Copy",
            0x7D => "Paste",
            0x7E => "Find",
            0x7F => "Mute",
            0x80 => "Volume up",
            0x81 => "Volume down",
            0x82 => "Locking Caps Lock",
            0x83 => "Locking Num Lock",
            0x84 => "Locking Scroll Lock",
            0x85 => "Keypad comma",
            0x86 => "Keypad equals (alternate)",
            >= 0x87 and <= 0x8F => $"International {usage - 0x86}",
            >= 0x90 and <= 0x98 => $"Language {usage - 0x8F}",
            0x99 => "Alt erase",
            0x9A => "SysReq / attention",
            0x9B => "Cancel",
            0x9C => "Clear",
            0x9D => "Prior",
            0x9E => "Return",
            0x9F => "Separator",
            0xA0 => "Out",
            0xA1 => "Oper",
            0xA2 => "Clear / again",
            0xA3 => "CrSel / props",
            0xA4 => "ExSel",
            0xB0 => "Keypad 00",
            0xB1 => "Keypad 000",
            0xB2 => "Thousands separator",
            0xB3 => "Decimal separator",
            0xB4 => "Currency unit",
            0xB5 => "Currency sub-unit",
            0xB6 => "Keypad left parenthesis",
            0xB7 => "Keypad right parenthesis",
            0xB8 => "Keypad left brace",
            0xB9 => "Keypad right brace",
            >= 0xBA and <= 0xC1 => $"Keypad function {usage - 0xB9}",
            0xC2 => "Keypad XOR",
            0xC3 => "Keypad caret",
            0xC4 => "Keypad percent",
            0xC5 => "Keypad less-than",
            0xC6 => "Keypad greater-than",
            0xC7 => "Keypad ampersand",
            0xC8 => "Keypad double ampersand",
            0xC9 => "Keypad double pipe",
            0xCA => "Keypad colon",
            0xCB => "Keypad hash",
            0xCC => "Keypad space",
            0xCD => "Keypad at",
            0xCE => "Keypad exclamation",
            0xCF => "Keypad memory store",
            0xD0 => "Keypad memory recall",
            0xD1 => "Keypad memory clear",
            0xD2 => "Keypad memory add",
            0xD3 => "Keypad memory subtract",
            0xD4 => "Keypad memory multiply",
            0xD5 => "Keypad memory divide",
            0xD6 => "Keypad plus/minus",
            0xD7 => "Keypad clear",
            0xD8 => "Keypad clear entry",
            0xD9 => "Keypad binary",
            0xDA => "Keypad octal",
            0xDB => "Keypad decimal",
            0xDC => "Keypad hexadecimal",
            0xE0 => "Left Ctrl",
            0xE1 => "Left Shift",
            0xE2 => "Left Alt",
            0xE3 => "Left GUI / Windows",
            0xE4 => "Right Ctrl",
            0xE5 => "Right Shift",
            0xE6 => "Right Alt",
            0xE7 => "Right GUI / Windows",
            _ => $"Reserved / vendor keyboard usage 0x{usage:X2}",
        };

    private static string DescribeConsumerUsage(ushort usage)
        => usage switch
        {
            0x00B0 => "Play",
            0x00B1 => "Pause",
            0x00B2 => "Record",
            0x00B3 => "Fast forward",
            0x00B4 => "Rewind",
            0x00B5 => "Next track",
            0x00B6 => "Previous track",
            0x00B7 => "Stop",
            0x00B8 => "Eject",
            0x00B9 => "Random play",
            0x00BA => "Repeat",
            0x00CD => "Play / pause",
            0x00E2 => "Mute",
            0x00E9 => "Volume up",
            0x00EA => "Volume down",
            0x006F => "Brightness up",
            0x0070 => "Brightness down",
            0x00EF => "Bass boost",
            0x00F0 => "Loudness",
            0x00F1 => "Volume",
            0x0183 => "AL Consumer Control",
            0x018A => "AL Email reader",
            0x018B => "AL Calculator",
            0x0192 => "AL Audio player",
            0x0194 => "AL Local browser",
            0x0196 => "AL Internet browser",
            0x0199 => "AC Search",
            0x019A => "AC Home",
            0x019B => "AC Back",
            0x019C => "AC Forward",
            0x019D => "AC Stop",
            0x019E => "AC Refresh",
            0x019F => "AC Bookmarks",
            _ => $"Reserved / vendor consumer usage 0x{usage:X4}",
        };

    private void SelectDevice()
    {
        if (_deviceList.SelectedIndex is < 0 or >= int.MaxValue
            || _deviceList.SelectedIndex >= _devices.Count)
        {
            return;
        }

        var device = _devices[_deviceList.SelectedIndex];
        var descriptor = device.GetReportDescriptor();
        _targetReportDescriptor = descriptor;
        var details =
            $"{device.Descriptor.HardwareId} {device.Descriptor.ProductName}{Environment.NewLine}" +
            $"Path: {device.Descriptor.DevicePath}{Environment.NewLine}" +
            $"Reports: input {descriptor.MaxInputReportLength} B, " +
            $"output {descriptor.MaxOutputReportLength} B, " +
            $"feature {descriptor.MaxFeatureReportLength} B, " +
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
            _stageSafeButton.IsEnabled = false;
            _writeAllButton.IsEnabled = false;
            _profileButton.IsEnabled = false;
            SetLightingControlsEnabled(true, false);
        }
        else
        {
            SetStatus("Select the 6D7D:DCFC vendor configuration interface.");
            _readButton.IsEnabled = false;
            _saveButton.IsEnabled = false;
            _stageSafeButton.IsEnabled = false;
            _writeAllButton.IsEnabled = false;
            _profileButton.IsEnabled = false;
            SetLightingControlsEnabled(false);
        }

        UpdateRawKvp();
        if (isTarget)
        {
            // Reading is safe and makes the lab immediately useful when the
            // exact device is already connected; writes still require explicit
            // user actions and confirmation.
            ReadConfiguration();
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
            if (_target.SupportsLegacyFrames)
            {
                ReadLegacyConfiguration();
                return;
            }

            if (!_target.SupportsWebHubFrames)
            {
                var probe = _target.ProbeReadOnly();
                _probe = probe;
                _deviceInfo = null;
                SetActionControlsEnabled(false);
                SetStatus(
                    "Read-only probe completed; configuration writes remain disabled.",
                    Profile.WarningBrush);
                AppendDiagnostic(
                    $"PROBE request: {Convert.ToHexString(probe.Request.ToArray())}{Environment.NewLine}" +
                    $"PROBE response (report {probe.ReportId}): " +
                    $"{Convert.ToHexString(probe.Response.ToArray())}{Environment.NewLine}" +
                    _target.ProtocolSupportDescription);
                UpdateRawKvp();
                return;
            }

            DisposeSession();
            _session = _target.OpenSession();
            var info = _session.ReadDeviceInfo();
            _deviceInfo = info;
            _probe = null;
            if (info.ActiveProfile < _profileBox.ItemCount)
            {
                _profileBox.SelectedIndex = info.ActiveProfile;
            }

            var actions = _session.ReadActions();
            _actions = actions.ToArray();
            _loadedActions = actions.ToArray();
            for (var index = 0; index < _actions.Length; index++)
            {
                UpdateActionButton(index);
            }

            SetActionControlsEnabled(true);
            _profileButton.IsEnabled = true;
            SetStatus(
                $"Read {actions.Count} actions from profile {info.ActiveProfile}.",
                Profile.AccentFillBrush);
            AppendDiagnostic(
                $"INFO response: {Convert.ToHexString(info.RawData.ToArray())}{Environment.NewLine}" +
                $"Action record length: {info.ButtonFunctionLength}{Environment.NewLine}" +
                $"Active profile: {info.ActiveProfile}");
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            DisposeSession();
            SetActionControlsEnabled(false);
            SetStatus($"Configuration read failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private void ReadLegacyConfiguration()
    {
        DisposeSession();
        _legacySession = _target!.OpenLegacySession();
        _legacyInfo = _legacySession.ReadDeviceInfo();
        _deviceInfo = null;
        _probe = null;
        _legacyMode = true;
        _actionType.IsVisible = false;
        _legacyActionType.IsVisible = true;
        _profileBox.IsVisible = false;
        _profileButton.IsVisible = false;

        var actions = _legacySession.ReadActions();
        _legacyActions = actions.ToArray();
        _loadedLegacyActions = actions.ToArray();
        for (var index = 0; index < _legacyActions.Length; index++)
        {
            UpdateActionButton(index);
        }

        try
        {
            _legacyLighting = _legacySession.ReadLighting();
            if (_legacyLighting.TryGetSettings(out var lightingSettings))
            {
                _lightingSettings = lightingSettings;
                ApplyLightingSettingsToEditor();
            }
            AppendDiagnostic($"FACTORY LIGHTING READ: {_legacyLighting.RawHex}");
        }
        catch (Exception exception)
        {
            AppendDiagnostic($"FACTORY LIGHTING READ FAILED: {exception.Message}");
        }

        SetActionControlsEnabled(true);
        SetLightingControlsEnabled(true, true);
        SetStatus(
            "Factory action table loaded. Writes are flash-backed and read back for verification.",
            Profile.WarningBrush);
        AppendDiagnostic(
            $"FACTORY INFO response: {_legacyInfo.RawHex}{Environment.NewLine}" +
            $"Factory action record length: 3 bytes{Environment.NewLine}" +
            $"Factory action count: {_legacyActions.Length}");
        UpdateRawKvp();
    }

    private void ReadLighting(object? sender, RoutedEventArgs args)
    {
        if (_legacySession is null)
        {
            SetStatus(
                "Read the connected Keytron configuration before reading lighting.",
                Profile.WarningBrush);
            AppendActivity("LIGHTING read ignored: no factory configuration session.");
            return;
        }

        try
        {
            _legacyLighting = _legacySession.ReadLighting();
            if (_legacyLighting.TryGetSettings(out var settings))
            {
                _lightingSettings = settings;
                ApplyLightingSettingsToEditor();
            }

            SetStatus("Read factory RGB lighting response.", Profile.AccentFillBrush);
            AppendDiagnostic($"FACTORY LIGHTING READ: {_legacyLighting.RawHex}");
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"RGB lighting read failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private async void WriteLighting(object? sender, RoutedEventArgs args)
    {
        if (_legacySession is null)
        {
            SetStatus(
                "Read the connected Keytron configuration before applying lighting.",
                Profile.WarningBrush);
            AppendActivity("LIGHTING write ignored: no factory configuration session.");
            return;
        }

        if (!TryReadLightingEditor(out var settings))
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "Write RGB lighting?",
            $"Apply {DescribeLightingSettings(settings)} to the connected Keytron?");
        if (!confirmed)
        {
            AppendActivity("LIGHTING write cancelled by user.");
            return;
        }

        try
        {
            SetStatus("Applying lighting to the Keytron…", Profile.WarningBrush);
            AppendActivity($"LIGHTING write requested: {DescribeLightingSettings(settings)}");
            var requested = settings;
            _legacyLighting = _legacySession.WriteLighting(settings);
            var hasEcho = _legacyLighting.TryGetSettings(out var accepted);
            if (hasEcho)
            {
                settings = accepted;
            }

            _lightingSettings = settings;
            ApplyLightingSettingsToEditor();
            var verification = hasEcho
                ? accepted == requested
                    ? "verified: device echoed the requested settings"
                    : $"warning: device echoed {DescribeLightingSettings(accepted)}"
                : "response received; firmware did not echo the full settings record";
            SetStatus(
                $"Lighting command completed ({verification}).",
                verification.StartsWith("verified", StringComparison.Ordinal)
                    ? Profile.AccentFillBrush
                    : Profile.WarningBrush);
            AppendDiagnostic(
                $"FACTORY LIGHTING WRITE: {_legacyLighting.RawHex}{Environment.NewLine}" +
                $"LIGHTING RESULT: {verification}");
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"Lighting write failed: {exception.Message}", Profile.DangerBrush);
            AppendActivity($"LIGHTING write failed: {exception}");
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private bool TryReadLightingEditor(out KeytronLegacyLightingSettings settings)
    {
        settings = _lightingSettings;
        if (_lightingMode.SelectedItem is not LightingModeChoice effect)
        {
            SetStatus("Choose a lighting effect first.", Profile.WarningBrush);
            return false;
        }

        var color = _lightingColor.Color;
        settings = KeytronLegacyLightingSettings.FromRgb(
            color.R,
            color.G,
            color.B,
            effect.Value,
            ToLightingByte(_lightingBrightness.Value),
            ToLightingByte(_lightingSpeed.Value)) with
        {
            Custom = _lightingCustom.IsChecked == true,
        };
        return true;
    }

    private void ApplyLightingSettingsToEditor()
    {
        _lightingMode.SelectedItem = _lightingMode.Items
            .OfType<LightingModeChoice>()
            .FirstOrDefault(choice => choice.Value == _lightingSettings.EffectMode);
        _lightingBrightness.Value = _lightingSettings.MasterBrightness;
        _lightingSpeed.Value = _lightingSettings.AnimationSpeed;
        _lightingCustom.IsChecked = _lightingSettings.UseCustomColor;
        var (red, green, blue) = _lightingSettings.ToRgb();
        _lightingColor.Color = Color.FromRgb(red, green, blue);
        UpdateLightingEditorSummary();
    }

    private async void SaveSelectedAction(object? sender, RoutedEventArgs args)
    {
        if (_legacyMode)
        {
            await SaveSelectedLegacyAction();
            return;
        }

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
            _loadedActions = readBack.ToArray();
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
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"Action write failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private async Task SaveSelectedLegacyAction()
    {
        if (_legacySession is null || _selectedAction < 0)
        {
            return;
        }

        if (!TryReadLegacyEditor(out var action))
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "Write factory-firmware action?",
            $"Write action {_selectedAction} ({ActionLabel(_selectedAction)}) to flash?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _legacySession.WriteAction(_selectedAction, action);
            var readBack = _legacySession.ReadActions().ToArray();
            _legacyActions = readBack;
            _loadedLegacyActions = readBack.ToArray();
            for (var index = 0; index < _legacyActions.Length; index++)
            {
                UpdateActionButton(index);
            }

            var verified = _legacyActions[_selectedAction] == action;
            SetStatus(
                verified
                    ? "Factory action written and verified."
                    : "Factory write completed, but read-back differed.",
                verified ? Profile.AccentFillBrush : Profile.WarningBrush);
            AppendDiagnostic(
                $"FACTORY WRITE action {_selectedAction}: " +
                $"{Convert.ToHexString(action.ToByteArray())}{Environment.NewLine}" +
                $"READ-BACK: " +
                $"{Convert.ToHexString(_legacyActions[_selectedAction].ToByteArray())}");
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"Factory action write failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
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
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"Profile selection failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private void StageSafeTestingMap(object? sender, RoutedEventArgs args)
    {
        if (_legacyMode)
        {
            StageSafeLegacyMap();
            return;
        }

        if (_session is null || _loadedActions.Length != 25)
        {
            SetStatus("Read the Keytron action table before staging a knob map.", Profile.WarningBrush);
            return;
        }

        var knobActions = WebHubMacroPadPresets.CreateUsefulKnobActions();
        for (var knob = 0; knob < 3; knob++)
        {
            var source = knob * 3;
            var target = 16 + knob * 3;
            _actions[target] = knobActions[source];
            _actions[target + 1] = knobActions[source + 1];
            _actions[target + 2] = knobActions[source + 2];
        }

        for (var index = 16; index < _actions.Length; index++)
        {
            UpdateActionButton(index);
        }

        _writeAllButton.IsEnabled = HasPendingChanges();
        SetStatus(
            "Staged safe knob actions: volume is reversed to match the physical dial.",
            Profile.WarningBrush);
        AppendDiagnostic(
            "STAGED safe knob actions (not written): " +
            string.Join(
                " | ",
                knobActions.Select(
                    (action, index) =>
                        $"{ActionLabel(16 + index)}={action.Description} [{Convert.ToHexString(action.ToByteArray())}]")));
        UpdateRawKvp();
    }

    private void StageSafeLegacyMap()
    {
        if (_legacySession is null || _loadedLegacyActions.Length != 25)
        {
            SetStatus(
                "Read the factory Keytron action table before staging a knob map.",
                Profile.WarningBrush);
            return;
        }

        var preset = KeytronLegacyPresets.CreateUsefulMap();
        for (var index = 0; index < _legacyActions.Length; index++)
        {
            _legacyActions[index] = preset[index];
            UpdateActionButton(index);
        }

        _writeAllButton.IsEnabled = HasPendingChanges();
        SetStatus(
            "Staged safe map: keys 1–12 are F13–F24; keys 13–16 and all knobs " +
            "use consumer/media controls only.",
            Profile.WarningBrush);
        AppendDiagnostic(
            "STAGED factory safe testing map (not written): " +
            string.Join(
                " | ",
                preset.Select(
                    (action, index) =>
                        $"{ActionLabel(index)}={action.Description} " +
                        $"[{Convert.ToHexString(action.ToByteArray())}]")));
        UpdateRawKvp();
    }

    private async void WriteStagedActions(object? sender, RoutedEventArgs args)
    {
        if (_legacyMode)
        {
            await WriteStagedLegacyActions();
            return;
        }

        if (_session is null)
        {
            return;
        }

        var pending = Enumerable.Range(0, _actions.Length)
            .Where(index => _actions[index] != _loadedActions[index])
            .ToArray();
        if (pending.Length == 0)
        {
            SetStatus("There are no staged action changes.", Profile.MutedBrush);
            return;
        }

        var confirmed = await ConfirmAsync(
            "Write staged changes?",
            $"Write {pending.Length} action(s) to Keytron profile {_profileBox.SelectedIndex}?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            foreach (var index in pending)
            {
                _session.WriteAction(
                    index,
                    _actions[index],
                    (byte)_profileBox.SelectedIndex);
            }

            var readBack = _session.ReadActions().ToArray();
            var verified = pending.All(index => readBack[index] == _actions[index]);
            _actions = readBack;
            _loadedActions = readBack.ToArray();
            for (var index = 0; index < _actions.Length; index++)
            {
                UpdateActionButton(index);
            }

            SetStatus(
                verified
                    ? $"Wrote and read back {pending.Length} staged action(s)."
                    : "Write completed, but read-back differed.",
                verified ? Profile.AccentFillBrush : Profile.WarningBrush);
            AppendDiagnostic(
                $"WRITE staged actions: {string.Join(", ", pending)}{Environment.NewLine}" +
                $"READ-BACK action count: {_actions.Length}");
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"Staged write failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private async Task WriteStagedLegacyActions()
    {
        if (_legacySession is null)
        {
            return;
        }

        var pending = Enumerable.Range(0, _legacyActions.Length)
            .Where(index => _legacyActions[index] != _loadedLegacyActions[index])
            .ToArray();
        if (pending.Length == 0)
        {
            SetStatus("There are no staged factory action changes.", Profile.MutedBrush);
            return;
        }

        var confirmed = await ConfirmAsync(
            "Write factory-firmware changes?",
            $"Write {pending.Length} staged action(s) to Keytron flash?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _legacySession.WriteActions(_legacyActions);
            var readBack = _legacySession.ReadActions().ToArray();
            var verified = pending.All(index => readBack[index] == _legacyActions[index]);
            _legacyActions = readBack;
            _loadedLegacyActions = readBack.ToArray();
            for (var index = 0; index < _legacyActions.Length; index++)
            {
                UpdateActionButton(index);
            }

            SetStatus(
                verified
                    ? $"Wrote and read back {pending.Length} factory action(s)."
                    : "Factory write completed, but read-back differed.",
                verified ? Profile.AccentFillBrush : Profile.WarningBrush);
            AppendDiagnostic(
                $"FACTORY WRITE staged actions: {string.Join(", ", pending)}{Environment.NewLine}" +
                $"READ-BACK action count: {_legacyActions.Length}");
            UpdateRawKvp();
        }
        catch (Exception exception)
        {
            SetStatus($"Factory staged write failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
            UpdateRawKvp();
        }
    }

    private void SelectAction(int index)
    {
        _selectedAction = index;
        if (_legacyMode)
        {
            SelectLegacyAction(index);
            return;
        }

        var action = _actions[index];
        _actionType.SelectedItem = _actionTypeChoices.FirstOrDefault(
            choice => choice.Value == action.Type);
        _parameter1.Text = action.Parameter1.ToString("X2", CultureInfo.InvariantCulture);
        _parameter2.Text = action.Parameter2.ToString("X2", CultureInfo.InvariantCulture);
        _parameter3.Text = action.Parameter3.ToString("X2", CultureInfo.InvariantCulture);
        UpdateParameterLabels();
        _actionSummary.Text =
            $"{ActionLabel(index)} — {action.Description}{Environment.NewLine}" +
            $"Parameters: {WebHubMacroPadActionDescriptions.DescribeParameters(action)}{Environment.NewLine}" +
            $"Raw: {Convert.ToHexString(action.ToByteArray())}" +
            (action == _loadedActions[index] ? " (loaded)" : " (staged)");
        _saveButton.IsEnabled = _session is not null;
        SetStatus($"Editing {ActionLabel(index)}.", Profile.AccentFillBrush);
    }

    private void SelectLegacyAction(int index)
    {
        var action = _legacyActions[index];
        _legacyActionType.SelectedItem = _legacyActionTypeChoices.FirstOrDefault(
            choice => choice.Value == action.Type);
        _parameter1.Text = action.Parameter1.ToString("X2", CultureInfo.InvariantCulture);
        _parameter2.Text = action.Parameter2.ToString("X2", CultureInfo.InvariantCulture);
        _parameter3.Text = "00";
        UpdateParameterLabels();
        _actionSummary.Text =
            $"{ActionLabel(index)} — {action.Description}{Environment.NewLine}" +
            $"Parameters: {KeytronLegacyActionDescriptions.DescribeParameters(action)}{Environment.NewLine}" +
            $"Raw: {Convert.ToHexString(action.ToByteArray())}" +
            (action == _loadedLegacyActions[index] ? " (loaded)" : " (staged)");
        _saveButton.IsEnabled = _legacySession is not null;
        SetStatus($"Editing {ActionLabel(index)}.", Profile.AccentFillBrush);
    }

    private bool TryReadEditor(out WebHubMacroPadAction action)
    {
        action = WebHubMacroPadAction.Disabled;
        if (_actionType.SelectedItem is not ActionTypeChoice<WebHubMacroPadActionType> choice
            || !TryReadByte(_parameter1.Text, out var parameter1)
            || !TryReadByte(_parameter2.Text, out var parameter2)
            || !TryReadByte(_parameter3.Text, out var parameter3))
        {
            SetStatus("Enter four valid action bytes.", Profile.WarningBrush);
            return false;
        }

        action = new WebHubMacroPadAction(
            choice.Value,
            parameter1,
            parameter2,
            parameter3);
        return true;
    }

    private bool TryReadLegacyEditor(out KeytronLegacyAction action)
    {
        action = KeytronLegacyAction.Disabled;
        if (_legacyActionType.SelectedItem is not ActionTypeChoice<KeytronLegacyActionType> choice
            || !TryReadByte(_parameter1.Text, out var parameter1)
            || !TryReadByte(_parameter2.Text, out var parameter2))
        {
            SetStatus("Enter three valid factory action bytes.", Profile.WarningBrush);
            return false;
        }

        action = new KeytronLegacyAction(choice.Value, parameter1, parameter2);
        return true;
    }

    private bool HasPendingChanges()
    {
        if (_legacyMode)
        {
            return _legacyActions.Length == _loadedLegacyActions.Length
                && Enumerable.Range(0, _legacyActions.Length)
                    .Any(index => _legacyActions[index] != _loadedLegacyActions[index]);
        }

        return _actions.Length == _loadedActions.Length
            && Enumerable.Range(0, _actions.Length)
                .Any(index => _actions[index] != _loadedActions[index]);
    }

    private void UpdateParameterLabels()
    {
        if (_legacyMode
            && _legacyActionType.SelectedItem
                is ActionTypeChoice<KeytronLegacyActionType> legacyChoice)
        {
            SetParameterLabels(LegacyParameterLabels(legacyChoice.Value));
            _parameter3.IsEnabled = false;
        }
        else if (_actionType.SelectedItem
            is ActionTypeChoice<WebHubMacroPadActionType> actionChoice)
        {
            SetParameterLabels(WebHubParameterLabels(actionChoice.Value));
            _parameter3.IsEnabled = true;
        }

        var legacyType = _legacyMode
            && _legacyActionType.SelectedItem
                is ActionTypeChoice<KeytronLegacyActionType> selectedLegacy
            ? selectedLegacy.Value
            : (KeytronLegacyActionType?)null;
        var actionType = !_legacyMode
            && _actionType.SelectedItem
                is ActionTypeChoice<WebHubMacroPadActionType> selectedAction
            ? selectedAction.Value
            : (WebHubMacroPadActionType?)null;
        var keyboard = legacyType is KeytronLegacyActionType.BareKeyboard
            or KeytronLegacyActionType.Keyboard
            || actionType == WebHubMacroPadActionType.Keyboard;
        var consumer = legacyType == KeytronLegacyActionType.Consumer
            || actionType == WebHubMacroPadActionType.Media;

        _modifierChoiceField.IsVisible = keyboard;
        _keyboardUsageChoiceField.IsVisible = keyboard;
        _consumerUsageChoiceField.IsVisible = consumer;
        SyncNamedInputChoices();
    }

    private void SyncNamedInputChoices()
    {
        if (_updatingInputChoices)
        {
            return;
        }

        _updatingInputChoices = true;
        try
        {
            if (!TryReadByte(_parameter1.Text, out var parameter1))
            {
                parameter1 = 0;
            }

            if (!TryReadByte(_parameter2.Text, out var parameter2))
            {
                parameter2 = 0;
            }

            SelectChoice(_modifierChoice, parameter1);
            SelectChoice(_keyboardUsageChoice, parameter2);
            SelectChoice(
                _consumerUsageChoice,
                (ushort)(parameter1 | parameter2 << 8));
        }
        finally
        {
            _updatingInputChoices = false;
        }
    }

    private static void SelectChoice<T>(ComboBox comboBox, T value)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<InputChoice<T>>()
            .FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, value));
    }

    private void ApplyNamedInputChoices()
    {
        if (_updatingInputChoices)
        {
            return;
        }

        _updatingInputChoices = true;
        try
        {
            if (_modifierChoiceField.IsVisible
                && _modifierChoice.SelectedItem is InputChoice<byte> modifier)
            {
                _parameter1.Text = modifier.Value.ToString("X2", CultureInfo.InvariantCulture);
            }

            if (_keyboardUsageChoiceField.IsVisible
                && _keyboardUsageChoice.SelectedItem is InputChoice<byte> keyboardUsage)
            {
                _parameter2.Text = keyboardUsage.Value.ToString("X2", CultureInfo.InvariantCulture);
            }

            if (_consumerUsageChoiceField.IsVisible
                && _consumerUsageChoice.SelectedItem is InputChoice<ushort> consumerUsage)
            {
                _parameter1.Text = ((byte)consumerUsage.Value).ToString(
                    "X2",
                    CultureInfo.InvariantCulture);
                _parameter2.Text = ((byte)(consumerUsage.Value >> 8)).ToString(
                    "X2",
                    CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _updatingInputChoices = false;
        }

        UpdateRawKvp();
    }

    private static IReadOnlyList<InputChoice<byte>> CreateModifierChoices()
    {
        var choices = new List<InputChoice<byte>>(256);
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            var mask = (byte)value;
            choices.Add(new(mask, DescribeModifierMask(mask)));
        }

        return choices;
    }

    private static IReadOnlyList<InputChoice<byte>> CreateKeyboardUsageChoices()
    {
        var choices = new List<InputChoice<byte>>(256);
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            var usage = (byte)value;
            choices.Add(new(usage, DescribeKeyboardUsage(usage)));
        }

        return choices;
    }

    private static IReadOnlyList<InputChoice<ushort>> CreateConsumerUsageChoices() =>
    [
        new(0x00B0, "Play"),
        new(0x00B1, "Pause"),
        new(0x00B2, "Record"),
        new(0x00B3, "Fast forward"),
        new(0x00B4, "Rewind"),
        new(0x00B5, "Next track"),
        new(0x00B6, "Previous track"),
        new(0x00B7, "Stop"),
        new(0x00B8, "Eject"),
        new(0x00B9, "Random play"),
        new(0x00BA, "Repeat"),
        new(0x00CD, "Play / pause"),
        new(0x00E2, "Mute"),
        new(0x00E9, "Volume up"),
        new(0x00EA, "Volume down"),
        new(0x006F, "Brightness up"),
        new(0x0070, "Brightness down"),
        new(0x00EF, "Bass boost"),
        new(0x00F0, "Loudness"),
        new(0x00F1, "Volume"),
        new(0x0183, "AL Consumer Control"),
        new(0x018A, "AL Email reader"),
        new(0x018B, "AL Calculator"),
        new(0x0192, "AL Audio player"),
        new(0x0194, "AL Local browser"),
        new(0x0196, "AL Internet browser"),
        new(0x0199, "AC Search"),
        new(0x019A, "AC Home"),
        new(0x019B, "AC Back"),
        new(0x019C, "AC Forward"),
        new(0x019D, "AC Stop"),
        new(0x019E, "AC Refresh"),
        new(0x019F, "AC Bookmarks"),
    ];

    private static string DescribeModifierMask(byte mask)
    {
        if (mask == 0)
        {
            return "No modifiers";
        }

        var modifiers = new List<string>(8);
        AddModifier(0x01, "Left Ctrl");
        AddModifier(0x02, "Left Shift");
        AddModifier(0x04, "Left Alt");
        AddModifier(0x08, "Left GUI / Windows");
        AddModifier(0x10, "Right Ctrl");
        AddModifier(0x20, "Right Shift");
        AddModifier(0x40, "Right Alt");
        AddModifier(0x80, "Right GUI / Windows");
        return string.Join(" + ", modifiers);

        void AddModifier(byte bit, string name)
        {
            if ((mask & bit) != 0)
            {
                modifiers.Add(name);
            }
        }
    }

    private void SetParameterLabels(
        (string First, string Second, string Third) labels)
    {
        _parameter1Label.Text = labels.First;
        _parameter2Label.Text = labels.Second;
        _parameter3Label.Text = labels.Third;
    }

    private static (string First, string Second, string Third) LegacyParameterLabels(
        KeytronLegacyActionType type) =>
        type switch
        {
            KeytronLegacyActionType.Disabled =>
                ("Unused byte 1 (hex)", "Unused byte 2 (hex)", "Not stored by factory firmware"),
            KeytronLegacyActionType.BareKeyboard or KeytronLegacyActionType.Keyboard =>
                ("Modifier mask (hex)", "Keyboard usage (hex)", "Not stored by factory firmware"),
            KeytronLegacyActionType.Consumer =>
                ("Consumer usage low byte (hex)", "Consumer usage high byte (hex)",
                    "Not stored by factory firmware"),
            KeytronLegacyActionType.Mouse =>
                ("Mouse byte 1 (hex)", "Mouse byte 2 (hex)", "Not stored by factory firmware"),
            KeytronLegacyActionType.System =>
                ("System usage low byte (hex)", "System usage high byte (hex)",
                    "Not stored by factory firmware"),
            _ =>
                ("Unknown byte 1 (hex)", "Unknown byte 2 (hex)", "Not stored by factory firmware"),
        };

    private static (string First, string Second, string Third) WebHubParameterLabels(
        WebHubMacroPadActionType type) =>
        type switch
        {
            WebHubMacroPadActionType.Disabled =>
                ("Unused byte 1 (hex)", "Unused byte 2 (hex)", "Unused byte 3 (hex)"),
            WebHubMacroPadActionType.Keyboard =>
                ("Modifier flags (hex)", "Keyboard usage (hex)", "Reserved byte (hex)"),
            WebHubMacroPadActionType.Media =>
                ("Consumer usage low byte (hex)", "Consumer usage high byte (hex)",
                    "Reserved byte (hex)"),
            WebHubMacroPadActionType.MouseButton =>
                ("Button mask low byte (hex)", "Button mask middle byte (hex)",
                    "Button mask high byte (hex)"),
            WebHubMacroPadActionType.MouseMove =>
                ("X delta (hex)", "Y delta (hex)", "Wheel delta (hex)"),
            WebHubMacroPadActionType.Power =>
                ("Power/system code (hex)", "Reserved byte (hex)", "Reserved byte (hex)"),
            WebHubMacroPadActionType.Macro =>
                ("Macro index (hex)", "Repeat count (hex)", "Extra macro byte (hex)"),
            WebHubMacroPadActionType.OpenWebsite =>
                ("URL reference byte 1 (hex)", "URL reference byte 2 (hex)",
                    "URL reference byte 3 (hex)"),
            _ =>
                ("Unknown byte 1 (hex)", "Unknown byte 2 (hex)", "Unknown byte 3 (hex)"),
        };

    private void SetActionControlsEnabled(bool enabled)
    {
        foreach (var button in _actionButtons)
        {
            button.IsEnabled = enabled;
        }

        _saveButton.IsEnabled = enabled && _selectedAction >= 0;
        _stageSafeButton.IsEnabled = enabled;
        _writeAllButton.IsEnabled = enabled && HasPendingChanges();
    }

    private void SetLightingControlsEnabled(bool readEnabled, bool writeEnabled = false)
    {
        _lightingMode.IsEnabled = readEnabled || writeEnabled;
        _lightingBrightness.IsEnabled = readEnabled || writeEnabled;
        _lightingSpeed.IsEnabled = readEnabled || writeEnabled;
        _lightingColor.IsEnabled = readEnabled || writeEnabled;
        _lightingCustom.IsEnabled = readEnabled || writeEnabled;
        _readLightingButton.IsEnabled = readEnabled;
        _writeLightingButton.IsEnabled = writeEnabled;
    }

    private void UpdateLightingEditorSummary()
    {
        if (_lightingMode.SelectedItem is not LightingModeChoice effect)
        {
            return;
        }

        var color = _lightingColor.Color;
        _lightingBrightnessValue.Text =
            $"{ToLightingByte(_lightingBrightness.Value) / 255d:P0}";
        _lightingSpeedValue.Text =
            $"{ToLightingByte(_lightingSpeed.Value) / 255d:P0}";
        _lightingSummary.Text =
            $"{effect.Label} · " +
            $"brightness {_lightingBrightnessValue.Text} · " +
            $"speed {_lightingSpeedValue.Text} · " +
            $"color {DescribeColor(color)}";
    }

    private static Slider CreateLightingSlider() =>
        new()
        {
            Minimum = 0,
            Maximum = byte.MaxValue,
            TickFrequency = 16,
            IsSnapToTickEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

    private static TextBlock CreateLightingValueLabel() =>
        new()
        {
            Foreground = Profile.ActionBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

    private static Control LightingSliderField(
        Slider slider,
        TextBlock valueLabel)
    {
        Grid.SetColumn(valueLabel, 1);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                slider,
                valueLabel,
            },
        };
    }

    private static IReadOnlyList<LightingModeChoice> CreateLightingModeChoices()
    {
        var choices = new List<LightingModeChoice>(byte.MaxValue + 1)
        {
            new(0, "Off"),
            new(1, "Solid / selected color"),
        };
        for (var value = 2; value <= byte.MaxValue; value++)
        {
            choices.Add(new((byte)value, $"Pattern {value}"));
        }

        return choices;
    }

    private static byte ToLightingByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value), 0, byte.MaxValue);

    private static string DescribeLightingSettings(
        KeytronLegacyLightingSettings settings)
    {
        var (red, green, blue) = settings.ToRgb();
        return $"{DescribeLightingEffect(settings.EffectMode)}, " +
            $"brightness {settings.MasterBrightness / 255d:P0}, " +
            $"speed {settings.AnimationSpeed / 255d:P0}, " +
            $"{(settings.UseCustomColor ? "selected color" : "effect palette")} " +
            $"{DescribeColor(Color.FromRgb(red, green, blue))}";
    }

    private static string DescribeLightingEffect(byte mode) =>
        CreateLightingModeChoices()
            .FirstOrDefault(choice => choice.Value == mode)
            ?.Label
        ?? $"Pattern {mode}";

    private static string DescribeColor(Color color) =>
        $"RGB {color.R}, {color.G}, {color.B}";

    private void UpdateActionButton(int index)
    {
        if (index < 0 || index >= _actionButtons.Count)
        {
            return;
        }

        if (_legacyMode)
        {
            _actionButtons[index].Content =
                $"{ActionLabel(index)}{Environment.NewLine}" +
                $"{_legacyActions[index].Description}{Environment.NewLine}" +
                $"{Convert.ToHexString(_legacyActions[index].ToByteArray())}" +
                (_legacyActions[index] == _loadedLegacyActions[index] ? string.Empty : "  *");
            return;
        }

        _actionButtons[index].Content =
            $"{ActionLabel(index)}{Environment.NewLine}" +
            $"{_actions[index].Description}{Environment.NewLine}" +
            $"{Convert.ToHexString(_actions[index].ToByteArray())}" +
            (_actions[index] == _loadedActions[index] ? string.Empty : "  *");
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

    private async void CopyRawKvp(object? sender, RoutedEventArgs args)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            SetStatus("Clipboard is not available.", Profile.WarningBrush);
            return;
        }

        try
        {
            var text = _rawKvp.Text ?? BuildRawKvp();
            await clipboard.SetTextAsync(text);
            SetStatus($"Copied {text.Length:N0} characters of raw KVP.", Profile.AccentFillBrush);
        }
        catch (Exception exception)
        {
            SetStatus($"Copy failed: {exception.Message}", Profile.DangerBrush);
        }
    }

    private async void CopyActivityLog(object? sender, RoutedEventArgs args)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            SetStatus("Clipboard is not available.", Profile.WarningBrush);
            return;
        }

        try
        {
            var text = _activityLog.Text ?? string.Empty;
            await clipboard.SetTextAsync(text);
            SetStatus($"Copied {text.Length:N0} characters of raw activity.", Profile.AccentFillBrush);
        }
        catch (Exception exception)
        {
            SetStatus($"Copy failed: {exception.Message}", Profile.DangerBrush);
        }
    }

    private void ClearActivityLog(object? sender, RoutedEventArgs args)
    {
        _activityLog.Text = string.Empty;
        UpdateRawKvp();
        SetStatus("Raw activity log cleared.", Profile.MutedBrush);
    }

    private void AppendActivity(string text)
    {
        if (_closing)
        {
            return;
        }

        void Append()
        {
            if (_closing)
            {
                return;
            }

            var line = $"{DateTime.Now:HH:mm:ss.fff} {text.Replace(
                Environment.NewLine,
                " | ",
                StringComparison.Ordinal)}";
            var current = _activityLog.Text;
            var combined = string.IsNullOrWhiteSpace(current)
                ? line
                : $"{current}{Environment.NewLine}{line}";
            _activityLog.Text = combined.Length <= 200_000
                ? combined
                : combined[^200_000..];
            _activityLog.CaretIndex = _activityLog.Text?.Length ?? 0;
            UpdateRawKvp();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Append();
        }
        else
        {
            Dispatcher.UIThread.Post(Append);
        }
    }

    private void UpdateRawKvp() => _rawKvp.Text = BuildRawKvp();

    private string BuildRawKvp()
    {
        var lines = new List<string>
        {
            "snapshot.version=1",
            $"status={_statusText.Text ?? string.Empty}",
            $"target.expected={WebHubMacroPad.VendorId:X4}:{WebHubMacroPad.ProductId:X4}",
            $"target.connected={_target is not null}",
        };
        Add("activityLog.present", !string.IsNullOrWhiteSpace(_activityLog.Text));
        Add("activityLog.text", (_activityLog.Text ?? string.Empty).Replace(
            Environment.NewLine,
            " | ",
            StringComparison.Ordinal));

        if (_target is null)
        {
            return string.Join(Environment.NewLine, lines);
        }

        var device = _target.Device;
        var deviceInfo = device.Descriptor;
        Add("target.hardwareId", deviceInfo.HardwareId);
        Add("target.manufacturer", deviceInfo.Manufacturer);
        Add("target.productName", deviceInfo.ProductName);
        Add("target.friendlyName", deviceInfo.FriendlyName);
        Add("target.serialNumber", deviceInfo.SerialNumber);
        Add("target.releaseNumberBcd", $"0x{deviceInfo.ReleaseNumberBcd:X4}");
        Add("target.devicePath", deviceInfo.DevicePath);
        Add("target.maxInputReportLength", deviceInfo.MaxInputReportLength);
        Add("target.maxOutputReportLength", deviceInfo.MaxOutputReportLength);
        Add("target.maxFeatureReportLength", deviceInfo.MaxFeatureReportLength);
        Add("protocol.configurationUsage", $"0x{WebHubMacroPad.ConfigurationUsage:X8}");
        Add("protocol.supportsWebHubFrames", _target.SupportsWebHubFrames);
        Add("protocol.supportsLegacyFrames", _target.SupportsLegacyFrames);
        Add("protocol.description", _target.ProtocolSupportDescription);

        try
        {
            _targetReportDescriptor ??= device.GetReportDescriptor();
            Add("hid.reportsUseId", _targetReportDescriptor.ReportsUseId);
            Add("hid.maxInputReportLength", _targetReportDescriptor.MaxInputReportLength);
            Add("hid.maxOutputReportLength", _targetReportDescriptor.MaxOutputReportLength);
            Add("hid.maxFeatureReportLength", _targetReportDescriptor.MaxFeatureReportLength);

            for (var index = 0; index < _targetReportDescriptor.Collections.Count; index++)
            {
                var collection = _targetReportDescriptor.Collections[index];
                Add($"hid.collection[{index}].type", collection.CollectionType);
                Add(
                    $"hid.collection[{index}].usages",
                    string.Join(",", collection.Usages.Select(value => $"0x{value:X8}")));
                Add(
                    $"hid.collection[{index}].inputReports",
                    string.Join(
                        ",",
                        collection.InputReports.Select(
                            report => $"id={report.ReportId}:len={report.Length}")));
                Add(
                    $"hid.collection[{index}].outputReports",
                    string.Join(
                        ",",
                        collection.OutputReports.Select(
                            report => $"id={report.ReportId}:len={report.Length}")));
            }

            Add(
                "hid.inputReports",
                string.Join(
                    ",",
                    _targetReportDescriptor.InputReports.Select(
                        report => $"id={report.ReportId}:len={report.Length}")));
            Add(
                "hid.outputReports",
                string.Join(
                    ",",
                    _targetReportDescriptor.OutputReports.Select(
                        report => $"id={report.ReportId}:len={report.Length}")));
        }
        catch (Exception exception)
        {
            Add("hid.descriptor.error", exception.Message);
        }

        try
        {
            _rawReportDescriptor ??= device.GetRawReportDescriptor();
            Add("hid.rawReportDescriptor", Convert.ToHexString(_rawReportDescriptor!));
        }
        catch (Exception exception)
        {
            Add("hid.rawReportDescriptor.error", exception.Message);
        }

        if (_probe is not null)
        {
            Add("probe.reportId", _probe.ReportId);
            Add("probe.request", Convert.ToHexString(_probe.Request.ToArray()));
            Add("probe.response", Convert.ToHexString(_probe.Response.ToArray()));
        }

        if (_deviceInfo is not null)
        {
            Add("deviceInfo.raw", Convert.ToHexString(_deviceInfo.RawData.ToArray()));
            Add("deviceInfo.buttonFunctionLength", _deviceInfo.ButtonFunctionLength);
            Add("deviceInfo.activeProfile", _deviceInfo.ActiveProfile);
        }

        if (_legacyInfo is not null)
        {
            Add("legacy.deviceInfo.raw", _legacyInfo.RawHex);
            Add("legacy.deviceInfo.command", $"0x{_legacyInfo.Command:X2}");
            if (_legacyLighting is not null)
            {
                Add("legacy.lighting.response.raw", _legacyLighting.RawHex);
                Add("legacy.lighting.response.command", $"0x{_legacyLighting.Command:X2}");
                if (_legacyLighting.TryGetSettings(out var lightingSettings))
                {
                    Add("legacy.lighting.effectMode", $"0x{lightingSettings.EffectMode:X2}");
                    Add(
                        "legacy.lighting.effect",
                        DescribeLightingEffect(lightingSettings.EffectMode));
                    Add(
                        "legacy.lighting.masterBrightness",
                        $"0x{lightingSettings.MasterBrightness:X2}");
                    Add(
                        "legacy.lighting.masterBrightnessPercent",
                        $"{lightingSettings.MasterBrightness / 255d:P0}");
                    Add(
                        "legacy.lighting.animationSpeed",
                        $"0x{lightingSettings.AnimationSpeed:X2}");
                    Add(
                        "legacy.lighting.animationSpeedPercent",
                        $"{lightingSettings.AnimationSpeed / 255d:P0}");
                    Add("legacy.lighting.useCustomColor", lightingSettings.UseCustomColor);
                    Add("legacy.lighting.colorHue", $"0x{lightingSettings.ColorHue:X2}");
                    Add(
                        "legacy.lighting.colorSaturation",
                        $"0x{lightingSettings.ColorSaturation:X2}");
                    Add("legacy.lighting.colorValue", $"0x{lightingSettings.ColorValue:X2}");
                    var rgb = lightingSettings.ToRgb();
                    Add("legacy.lighting.rgb", $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}");
                    Add(
                        "legacy.lighting.rgbNamed",
                        $"red={rgb.Red}, green={rgb.Green}, blue={rgb.Blue}");
                }
            }
            Add("legacy.actionRecordLength", 3);
            Add("legacy.actions.count", _legacyActions.Length);
            for (var index = 0; index < _legacyActions.Length; index++)
            {
                var action = _legacyActions[index];
                Add($"legacy.action[{index:D2}].control", ActionLabel(index));
                Add($"legacy.action[{index:D2}].type", $"0x{(byte)action.Type:X2}");
                Add(
                    $"legacy.action[{index:D2}].typeName",
                    KeytronLegacyActionDescriptions.DescribeType(action.Type));
                Add($"legacy.action[{index:D2}].parameter1", $"0x{action.Parameter1:X2}");
                Add($"legacy.action[{index:D2}].parameter2", $"0x{action.Parameter2:X2}");
                Add(
                    $"legacy.action[{index:D2}].parameters",
                    KeytronLegacyActionDescriptions.DescribeParameters(action));
                Add($"legacy.action[{index:D2}].raw", Convert.ToHexString(action.ToByteArray()));
                Add($"legacy.action[{index:D2}].meaning", action.Description);
                Add(
                    $"legacy.action[{index:D2}].state",
                    action == _loadedLegacyActions[index] ? "loaded" : "staged");
            }
        }
        else
        {
            Add("actions.count", _actions.Length);
            for (var index = 0; index < _actions.Length; index++)
            {
                var action = _actions[index];
                Add($"action[{index:D2}].control", ActionLabel(index));
                Add($"action[{index:D2}].type", $"0x{(byte)action.Type:X2}");
                Add(
                    $"action[{index:D2}].typeName",
                    WebHubMacroPadActionDescriptions.DescribeType(action.Type));
                Add($"action[{index:D2}].parameter1", $"0x{action.Parameter1:X2}");
                Add($"action[{index:D2}].parameter2", $"0x{action.Parameter2:X2}");
                Add($"action[{index:D2}].parameter3", $"0x{action.Parameter3:X2}");
                Add(
                    $"action[{index:D2}].parameters",
                    WebHubMacroPadActionDescriptions.DescribeParameters(action));
                Add($"action[{index:D2}].raw", Convert.ToHexString(action.ToByteArray()));
                Add($"action[{index:D2}].meaning", action.Description);
                Add(
                    $"action[{index:D2}].state",
                    action == _loadedActions[index] ? "loaded" : "staged");
            }
        }

        Add("actions.pending", HasPendingChanges());
        Add("wireLog.present", !string.IsNullOrWhiteSpace(_diagnostics.Text));
        Add("wireLog.text", (_diagnostics.Text ?? string.Empty).Replace(
            Environment.NewLine,
            " | ",
            StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines);

        void Add(string key, object? value) =>
            lines.Add($"{key}={value?.ToString() ?? "(null)"}");
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _legacySession?.Dispose();
        _session = null;
        _legacySession = null;
        _legacyMode = false;
        _saveButton.IsEnabled = false;
        _stageSafeButton.IsEnabled = false;
        _writeAllButton.IsEnabled = false;
        _profileButton.IsEnabled = false;
        SetLightingControlsEnabled(false);
    }

    private void AppendDiagnostic(string text)
    {
        _diagnostics.Text = string.IsNullOrWhiteSpace(_diagnostics.Text)
            ? text
            : $"{_diagnostics.Text}{Environment.NewLine}{Environment.NewLine}{text}";
        _diagnostics.CaretIndex = _diagnostics.Text?.Length ?? 0;
        AppendActivity($"WIRE {text}");
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
        Field(
            new TextBlock
            {
                Text = label,
                Foreground = Profile.MutedBrush,
            },
            editor);

    private static Control Field(TextBlock label, Control editor) =>
        new StackPanel
        {
            Spacing = 3,
            Children =
            {
                label,
                editor,
            },
        };

    private static TextBlock CreateParameterLabel() =>
        new()
        {
            Foreground = Profile.MutedBrush,
        };

    private static TextBox CreateHexBox(string text = "00") =>
        new()
        {
            Text = text,
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

    private sealed record InputChoice<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record LightingModeChoice(byte Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ActionTypeChoice<T>(T Value, string Label)
    {
        public override string ToString() => Label;
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
