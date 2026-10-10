using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CoverageStudio.Models;

namespace CoverageStudio.Ui;

/// <summary>IDE-style run panel: overall bar + compact per-host progress rows (no console host).</summary>
internal sealed class RunProgressView : Border
{
    private readonly TextBlock _title = CoverageTheme.Title("Idle", 13);
    private readonly TextBlock _status = CoverageTheme.Label("No active run", muted: true);
    private readonly TextBlock _counts = CoverageTheme.Mono("", 11);
    private readonly ProgressBar _overall = new()
    {
        Minimum = 0,
        Maximum = 1,
        Height = 6,
        MinHeight = 6,
        Margin = new Thickness(0, 6, 0, 8),
    };
    private readonly StackPanel _rows = new() { Spacing = 4 };
    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        MaxHeight = 220,
    };
    private readonly Button _cancel = CoverageTheme.MakeButton("Cancel");
    private readonly Button _open = CoverageTheme.MakeButton("Open report");
    private readonly Dictionary<string, HostRowChrome> _chrome = new(StringComparer.Ordinal);
    private WorkRun? _run;

    public RunProgressView()
    {
        Background = CoverageTheme.PanelBrush;
        BorderBrush = CoverageTheme.BorderBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(10);

        _cancel.IsEnabled = false;
        _open.IsEnabled = false;
        _cancel.Click += (_, _) => CancelRequested?.Invoke();
        _open.Click += (_, _) => OpenReportRequested?.Invoke();

        _scroll.Content = _rows;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { _cancel, _open },
        };

        var header = new DockPanel();
        DockPanel.SetDock(_status, Dock.Right);
        header.Children.Add(_status);
        header.Children.Add(_title);

        Child = new StackPanel
        {
            Children = { header, _counts, _overall, _scroll, actions },
        };
    }

    public event Action? CancelRequested;
    public event Action? OpenReportRequested;

    public void Bind(WorkRun? run)
    {
        if (_run is not null)
            _run.PropertyChanged -= OnRunChanged;

        _run = run;
        _chrome.Clear();
        _rows.Children.Clear();

        if (run is null)
        {
            _title.Text = "Idle";
            _status.Text = "No active run";
            _counts.Text = "";
            _overall.Value = 0;
            _cancel.IsEnabled = false;
            _open.IsEnabled = false;
            return;
        }

        run.PropertyChanged += OnRunChanged;
        foreach (var host in run.Hosts)
            EnsureRow(host);

        RefreshHeader();
    }

    public void Refresh()
    {
        if (_run is null)
            return;
        foreach (var host in _run.Hosts)
            EnsureRow(host);
        RefreshHeader();
    }

    private void OnRunChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        Dispatcher.UIThread.Post(RefreshHeader);

    private void RefreshHeader()
    {
        if (_run is null)
            return;

        _title.Text = _run.Title;
        _status.Text = _run.StatusLabel;
        _status.Foreground = _run.Phase switch
        {
            WorkPhase.Succeeded when _run.Failed == 0 => CoverageTheme.OkBrush,
            WorkPhase.Failed => CoverageTheme.DangerBrush,
            WorkPhase.Cancelled => CoverageTheme.WarnBrush,
            WorkPhase.Succeeded => CoverageTheme.WarnBrush,
            _ => CoverageTheme.MutedBrush,
        };
        _counts.Text = _run.CountsLabel;
        _overall.Value = _run.Progress;
        _cancel.IsEnabled = _run.Phase is WorkPhase.Running or WorkPhase.Queued;
        _open.IsEnabled = !string.IsNullOrWhiteSpace(_run.HtmlIndexPath) && File.Exists(_run.HtmlIndexPath!);

        foreach (var host in _run.Hosts)
            UpdateRow(host);
    }

    private void EnsureRow(WorkHostItem host)
    {
        if (_chrome.ContainsKey(host.Id))
            return;

        host.PropertyChanged += (_, _) => Dispatcher.UIThread.Post(() => UpdateRow(host));

        var name = CoverageTheme.Mono(host.DisplayName, 11);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var status = CoverageTheme.Label(host.StatusLabel, muted: true);
        status.Width = 52;
        var result = CoverageTheme.Label("", muted: true);
        result.FontSize = 11;
        result.TextTrimming = TextTrimming.CharacterEllipsis;
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 4,
            MinHeight = 4,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,2*") };
        Grid.SetColumn(name, 0);
        Grid.SetColumn(status, 1);
        Grid.SetColumn(result, 2);
        top.Children.Add(name);
        top.Children.Add(status);
        top.Children.Add(result);

        var block = new StackPanel
        {
            Spacing = 0,
            Children = { top, bar },
        };

        _chrome[host.Id] = new HostRowChrome(status, result, bar);
        _rows.Children.Add(block);
        UpdateRow(host);
    }

    private void UpdateRow(WorkHostItem host)
    {
        if (!_chrome.TryGetValue(host.Id, out var chrome))
            return;

        chrome.Status.Text = host.StatusLabel;
        chrome.Status.Foreground = host.Phase switch
        {
            HostPhase.Succeeded => CoverageTheme.OkBrush,
            HostPhase.Failed => CoverageTheme.DangerBrush,
            HostPhase.Cancelled => CoverageTheme.WarnBrush,
            HostPhase.Testing or HostPhase.Building or HostPhase.Parsing => CoverageTheme.AccentBrush,
            _ => CoverageTheme.MutedBrush,
        };
        chrome.Result.Text = host.ResultLabel;
        chrome.Bar.Value = host.Progress;
        chrome.Bar.IsVisible = host.Phase is not (HostPhase.Succeeded or HostPhase.Failed or HostPhase.Cancelled)
            || host.Progress < 1;
        if (host.Phase is HostPhase.Succeeded or HostPhase.Failed or HostPhase.Cancelled)
            chrome.Bar.IsVisible = false;
    }

    private sealed record HostRowChrome(TextBlock Status, TextBlock Result, ProgressBar Bar);
}
