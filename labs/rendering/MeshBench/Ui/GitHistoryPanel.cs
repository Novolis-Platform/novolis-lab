using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Timeline.Presentation.GitGraph;

namespace MeshBench.Ui;

/// <summary>Git-graph timeline with scroll and empty-state hint.</summary>
public sealed class GitHistoryPanel : Border
{
    private readonly GitGraphTimelineList _list = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _empty = new()
    {
        Text = "No snapshots yet.\n\nPress Ctrl+S to create your first commit.",
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Foreground = Brushes.LightGray,
        FontSize = 13,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(16),
    };

    private readonly TextBlock _legend = new()
    {
        FontSize = 10,
        Foreground = Brushes.LightGray,
        Margin = new Thickness(8, 4, 8, 0),
        IsVisible = false,
    };

    private readonly Panel _bodyHost = new();

    public GitHistoryPanel()
    {
        Background = new SolidColorBrush(Color.FromRgb(32, 32, 38));
        BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 65));
        BorderThickness = new Thickness(0, 0, 1, 0);
        MinHeight = 120;
        VerticalAlignment = VerticalAlignment.Stretch;

        _scroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            MinHeight = 80,
        };

        var root = new DockPanel();
        DockPanel.SetDock(_legend, Dock.Top);
        root.Children.Add(_legend);
        root.Children.Add(_bodyHost);
        Child = root;

        _list.RestoreRequested += (_, row) => RestoreRequested?.Invoke(this, row);
        ShowEmpty();
    }

    public void SetRows(IReadOnlyList<GitGraphTimelineRow> rows)
    {
        if (rows.Count == 0)
        {
            ShowEmpty();
            return;
        }

        _list.SetRows(rows);
        ShowList();

        var branchCount = rows.Select(r => r.BranchName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        _legend.IsVisible = branchCount > 1;
        if (_legend.IsVisible)
        {
            var branches = rows.Select(r => r.BranchName).Distinct(StringComparer.OrdinalIgnoreCase).Take(4);
            _legend.Text = "Branches: " + string.Join("  ", branches);
        }

        _list.SelectHeadRow(rows);
    }

    public GitGraphTimelineRow? SelectedRow => _list.SelectedGitRow;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
    {
        add => _list.SelectionChanged += value;
        remove => _list.SelectionChanged -= value;
    }

    public event EventHandler<GitGraphTimelineRow>? RestoreRequested;

    private void ShowEmpty()
    {
        _legend.IsVisible = false;
        _list.SetRows([]);
        _bodyHost.Children.Clear();
        _bodyHost.Children.Add(_empty);
    }

    private void ShowList()
    {
        _bodyHost.Children.Clear();
        _bodyHost.Children.Add(_scroll);
    }
}
