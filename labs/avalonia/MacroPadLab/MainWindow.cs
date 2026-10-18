using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
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
    private readonly TextBlock _actionSummary;
    private readonly ComboBox _actionType;
    private readonly ComboBox _legacyActionType;
    private readonly TextBox _parameter1;
    private readonly TextBox _parameter2;
    private readonly TextBox _parameter3;
    private readonly ComboBox _profileBox;
    private readonly Button _readButton;
    private readonly Button _saveButton;
    private readonly Button _writeAllButton;
    private readonly Button _stageKnobButton;
    private readonly Button _profileButton;
    private readonly TextBox _diagnostics;
    private readonly TextBox _rawKvp;
    private readonly Button _copyRawButton;
    private readonly List<Button> _actionButtons = [];
    private readonly List<ConnectedHidDevice> _devices = [];

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

        _actionType = new ComboBox
        {
            ItemsSource = Enum.GetValues<WebHubMacroPadActionType>(),
            SelectedItem = WebHubMacroPadActionType.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _legacyActionType = new ComboBox
        {
            ItemsSource = Enum.GetValues<KeytronLegacyActionType>(),
            SelectedItem = KeytronLegacyActionType.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
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
        _stageKnobButton = CreateButton("Stage useful knob map", StageUsefulKnobMap);
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
                        "firmware stores four. Writes are explicit and read back for verification.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Profile.MutedBrush,
                },
                _actionSummary,
                Field("Action type", _actionType),
                Field("Factory action type", _legacyActionType),
                Field("Parameter 1", _parameter1),
                Field("Parameter 2", _parameter2),
                Field("Parameter 3", _parameter3),
                _saveButton,
                _stageKnobButton,
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

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,*,390"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12),
        };
        AddPanel(body, devicePanel, 0);
        AddPanel(body, padPanel, 1);
        AddPanel(body, detailTabs, 2);

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
            _legacySession?.Dispose();
            _catalog.Dispose();
        };
        Opened += (_, _) => RefreshDevices();
    }

    private void RefreshDevices()
    {
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
        _actions = Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
        _loadedActions = Enumerable.Repeat(WebHubMacroPadAction.Disabled, 25).ToArray();
        _actionType.IsVisible = true;
        _legacyActionType.IsVisible = false;
        _profileBox.IsVisible = true;
        _profileButton.IsVisible = true;
        SetActionControlsEnabled(false);
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
        }
        catch (Exception exception)
        {
            SetStatus($"Device scan failed: {exception.Message}", Profile.DangerBrush);
            AppendDiagnostic(exception.ToString());
        }

        UpdateRawKvp();
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
            _stageKnobButton.IsEnabled = false;
            _writeAllButton.IsEnabled = false;
            _profileButton.IsEnabled = false;
        }
        else
        {
            SetStatus("Select the 6D7D:DCFC vendor configuration interface.");
            _readButton.IsEnabled = false;
            _saveButton.IsEnabled = false;
            _stageKnobButton.IsEnabled = false;
            _writeAllButton.IsEnabled = false;
            _profileButton.IsEnabled = false;
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

        SetActionControlsEnabled(true);
        SetStatus(
            "Factory action table loaded. Writes are flash-backed and read back for verification.",
            Profile.WarningBrush);
        AppendDiagnostic(
            $"FACTORY INFO response: {_legacyInfo.RawHex}{Environment.NewLine}" +
            $"Factory action record length: 3 bytes{Environment.NewLine}" +
            $"Factory action count: {_legacyActions.Length}");
        UpdateRawKvp();
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

    private void StageUsefulKnobMap(object? sender, RoutedEventArgs args)
    {
        if (_legacyMode)
        {
            StageUsefulLegacyKnobMap();
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
            "Staged: volume down is CCW, volume up is CW; presses now control media, navigation, or workspace.",
            Profile.WarningBrush);
        AppendDiagnostic(
            "STAGED useful knob map (not written): " +
            string.Join(
                " | ",
                knobActions.Select(
                    (action, index) =>
                        $"{ActionLabel(16 + index)}={action.Description} [{Convert.ToHexString(action.ToByteArray())}]")));
        UpdateRawKvp();
    }

    private void StageUsefulLegacyKnobMap()
    {
        if (_legacySession is null || _loadedLegacyActions.Length != 25)
        {
            SetStatus(
                "Read the factory Keytron action table before staging a knob map.",
                Profile.WarningBrush);
            return;
        }

        var preset = KeytronLegacyPresets.CreateUsefulMap();
        for (var index = 16; index < _legacyActions.Length; index++)
        {
            _legacyActions[index] = preset[index];
            UpdateActionButton(index);
        }

        _writeAllButton.IsEnabled = HasPendingChanges();
        SetStatus(
            "Staged: clockwise volume down, counter-clockwise volume up; " +
            "presses now control media, brightness, and navigation.",
            Profile.WarningBrush);
        AppendDiagnostic(
            "STAGED factory useful knob map (not written): " +
            string.Join(
                " | ",
                preset.Skip(16).Select(
                    (action, index) =>
                        $"{ActionLabel(16 + index)}={action.Description} " +
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
        _actionType.SelectedItem = action.Type;
        _parameter1.Text = action.Parameter1.ToString("X2", CultureInfo.InvariantCulture);
        _parameter2.Text = action.Parameter2.ToString("X2", CultureInfo.InvariantCulture);
        _parameter3.Text = action.Parameter3.ToString("X2", CultureInfo.InvariantCulture);
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
        _legacyActionType.SelectedItem = action.Type;
        _parameter1.Text = action.Parameter1.ToString("X2", CultureInfo.InvariantCulture);
        _parameter2.Text = action.Parameter2.ToString("X2", CultureInfo.InvariantCulture);
        _parameter3.Text = "00";
        _actionSummary.Text =
            $"{ActionLabel(index)} — {action.Description}{Environment.NewLine}" +
            $"Parameters: p1=0x{action.Parameter1:X2}, p2=0x{action.Parameter2:X2}{Environment.NewLine}" +
            $"Raw: {Convert.ToHexString(action.ToByteArray())}" +
            (action == _loadedLegacyActions[index] ? " (loaded)" : " (staged)");
        _saveButton.IsEnabled = _legacySession is not null;
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

    private bool TryReadLegacyEditor(out KeytronLegacyAction action)
    {
        action = KeytronLegacyAction.Disabled;
        if (_legacyActionType.SelectedItem is not KeytronLegacyActionType type
            || !TryReadByte(_parameter1.Text, out var parameter1)
            || !TryReadByte(_parameter2.Text, out var parameter2))
        {
            SetStatus("Enter three valid factory action bytes.", Profile.WarningBrush);
            return false;
        }

        action = new KeytronLegacyAction(type, parameter1, parameter2);
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

    private void SetActionControlsEnabled(bool enabled)
    {
        foreach (var button in _actionButtons)
        {
            button.IsEnabled = enabled;
        }

        _saveButton.IsEnabled = enabled && _selectedAction >= 0;
        _stageKnobButton.IsEnabled = enabled;
        _writeAllButton.IsEnabled = enabled && HasPendingChanges();
    }

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
            Add("legacy.actionRecordLength", 3);
            Add("legacy.actions.count", _legacyActions.Length);
            for (var index = 0; index < _legacyActions.Length; index++)
            {
                var action = _legacyActions[index];
                Add($"legacy.action[{index:D2}].control", ActionLabel(index));
                Add($"legacy.action[{index:D2}].type", $"0x{(byte)action.Type:X2}");
                Add($"legacy.action[{index:D2}].parameter1", $"0x{action.Parameter1:X2}");
                Add($"legacy.action[{index:D2}].parameter2", $"0x{action.Parameter2:X2}");
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
                Add($"action[{index:D2}].parameter1", $"0x{action.Parameter1:X2}");
                Add($"action[{index:D2}].parameter2", $"0x{action.Parameter2:X2}");
                Add($"action[{index:D2}].parameter3", $"0x{action.Parameter3:X2}");
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
        _stageKnobButton.IsEnabled = false;
        _writeAllButton.IsEnabled = false;
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
