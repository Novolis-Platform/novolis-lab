using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CoverageStudio.Models;
using CoverageStudio.Services;
using CoverageStudio.Ui;
using Novolis.Avalonia.Controls;
using Novolis.Avalonia.Layout;
using Novolis.Avalonia.Studio;
using Novolis.Tools.Coverage;

namespace CoverageStudio;

internal sealed class MainWindow : Window
{
    private readonly CoverageSession _session;
    private readonly StudioChrome _chrome;
    private readonly StudioFeedback _feedback;
    private readonly RunProgressView _runView = new();

    private readonly TextBox _rootBox;
    private readonly TextBox _outBox;
    private readonly CheckBox _platformBox;
    private readonly CheckBox _skipBuildBox;
    private readonly CheckBox _regenBox;
    private readonly CheckBox _flaggedOnlyBox;
    private readonly NumericUpDown _failBelow;
    private readonly NumericUpDown _crapThreshold;
    private readonly NumericUpDown _throttle;
    private readonly NumericUpDown _timeout;
    private readonly ListBox _repoList = new();
    private readonly TextBlock _summary = CoverageTheme.Mono("—");
    private readonly SortableDataGrid _repoGrid = new();
    private readonly SortableDataGrid _gapGrid = new();
    private readonly SortableDataGrid _crapGrid = new();
    private readonly TreeDetailsView _details = new();
    private readonly TabControl _tabs;

    private bool _busyUi;

    public MainWindow(CoverageSession session)
    {
        _session = session;
        Title = "Coverage Studio";
        Width = 1380;
        Height = 900;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CoverageTheme.ApplyWindowChrome(this);

        _chrome = StudioChrome.Create();
        _feedback = _chrome.CreateFeedback();
        _chrome.StatusLine.Foreground = CoverageTheme.MutedBrush;
        _chrome.StatusLine.FontFamily = CoverageTheme.BodyFont;
        _chrome.FlashLine.FontFamily = CoverageTheme.BodyFont;

        _rootBox = CoverageTheme.MakeTextBox(_session.Root, 420);
        _outBox = CoverageTheme.MakeTextBox(_session.OutputDir, 280);
        _platformBox = CoverageTheme.MakeCheck("Platform.slnx ProjectRef", true);
        _skipBuildBox = CoverageTheme.MakeCheck("Skip build", true);
        _regenBox = CoverageTheme.MakeCheck("Regenerate slnx", false);
        _flaggedOnlyBox = CoverageTheme.MakeCheck("Flagged only", true);
        _failBelow = CoverageTheme.MakeNumeric(-1, -1, 100, 72);
        _crapThreshold = CoverageTheme.MakeNumeric((decimal)CrapScore.DefaultThreshold, 1, 500, 72);
        _throttle = CoverageTheme.MakeNumeric(4, 0, 64, 64);
        _timeout = CoverageTheme.MakeNumeric(60, 0, 3600, 72);

        ConfigureGrids();
        _tabs = BuildTabs();

        _platformBox.IsCheckedChanged += (_, _) => _session.PlatformMode = _platformBox.IsChecked == true;
        _skipBuildBox.IsCheckedChanged += (_, _) => _session.SkipBuild = _skipBuildBox.IsChecked == true;
        _regenBox.IsCheckedChanged += (_, _) => _session.RegenerateSlnx = _regenBox.IsChecked == true;
        _flaggedOnlyBox.IsCheckedChanged += (_, _) => RefreshComplexityGrid();

        _runView.CancelRequested += () => _session.Cancel();
        _runView.OpenReportRequested += OpenHtmlReport;

        _session.Changed += () => Dispatcher.UIThread.Post(RefreshFromSession);
        _session.RunChanged += () => Dispatcher.UIThread.Post(RefreshRun);

        Content = BuildLayout();
        Opened += (_, _) =>
        {
            try
            {
                SyncOptionsToSession();
                _session.Discover();
                TryLoadExistingArtifacts();
                _feedback.SetStatus("Select repos, then Run tests or Collect coverage.");
            }
            catch (Exception ex)
            {
                _feedback.FlashError(ex.Message);
            }
        };
    }

    private Control BuildLayout()
    {
        var toolbar = BuildToolbar();
        var left = BuildRepoPane();

        var center = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(_tabs, 0);
        center.Children.Add(_tabs);

        var runHost = new StackPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                CoverageTheme.Title("Run", 13),
                _runView,
            },
        };
        Grid.SetRow(runHost, 1);
        center.Children.Add(runHost);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        left.VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumn(left, 0);
        body.Children.Add(left);
        Grid.SetColumn(center, 1);
        center.Margin = new Thickness(8, 0, 0, 0);
        body.Children.Add(center);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(toolbar);
        Grid.SetRow(body, 1);
        body.Margin = new Thickness(10, 0, 10, 0);
        root.Children.Add(body);

        var statusBar = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { _chrome.FlashLine, _chrome.StatusLine },
        };
        Grid.SetRow(statusBar, 2);
        root.Children.Add(statusBar);

        var overlayHost = new Grid();
        overlayHost.Children.Add(root);
        overlayHost.Children.Add(_chrome.BusyOverlay);
        return overlayHost;
    }

    private Control BuildToolbar()
    {
        var discover = CoverageTheme.MakeButton("Discover");
        discover.Click += (_, _) => SafeUi(() =>
        {
            SyncOptionsToSession();
            _session.Discover();
            _feedback.Flash($"Found {_session.Repos.Count} repos.");
        });

        var runTests = CoverageTheme.MakeButton("Run tests");
        runTests.Click += async (_, _) => await RunSafeAsync("Running tests…", "Tests finished.", async () =>
        {
            SyncOptionsToSession();
            await _session.RunTestsAsync();
            _tabs.SelectedIndex = 0;
        });

        var collect = CoverageTheme.MakeButton("Collect coverage", primary: true);
        collect.Click += async (_, _) => await RunSafeAsync("Collecting coverage…", "Coverage collected.", async () =>
        {
            SyncOptionsToSession();
            await _session.CollectCoverageAsync();
            _session.AnalyzeCrap();
            _tabs.SelectedIndex = 0;
        });

        var crap = CoverageTheme.MakeButton("Analyze CRAP");
        crap.Click += (_, _) => SafeUi(() =>
        {
            SyncOptionsToSession();
            _session.AnalyzeCrap();
            _tabs.SelectedIndex = 2;
            _feedback.Flash($"CRAP: {_session.LastCrap?.FlaggedCount ?? 0} flagged.");
        });

        var load = CoverageTheme.MakeButton("Load Cobertura…");
        load.Click += async (_, _) => await LoadCoberturaAsync();

        var openHtml = CoverageTheme.MakeButton("Open HTML");
        openHtml.Click += (_, _) => OpenHtmlReport();

        var browseRoot = CoverageTheme.MakeButton("…");
        browseRoot.Click += async (_, _) => await BrowseRootAsync();

        var options = new WrapPanel
        {
            Margin = new Thickness(0, 6, 0, 0),
            Children =
            {
                CoverageTheme.Label("Root"),
                _rootBox,
                browseRoot,
                CoverageTheme.Label("Out"),
                _outBox,
                _platformBox,
                _skipBuildBox,
                _regenBox,
                CoverageTheme.Label("FailBelow"),
                _failBelow,
                CoverageTheme.Label("CRAP >"),
                _crapThreshold,
                CoverageTheme.Label("Throttle"),
                _throttle,
                CoverageTheme.Label("Timeout s"),
                _timeout,
            },
        };

        return new StackPanel
        {
            Margin = new Thickness(10, 10, 10, 6),
            Spacing = 4,
            Children =
            {
                CoverageTheme.Title("Coverage Studio", 20),
                CoverageTheme.Label("Coverage, complexity, and tests — typed progress, no console host.", muted: true),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { discover, runTests, collect, crap, load, openHtml },
                },
                options,
                _summary,
            },
        };
    }

    private Control BuildRepoPane()
    {
        var selectAll = CoverageTheme.MakeButton("All");
        selectAll.Click += (_, _) =>
        {
            _session.SelectAll(true);
            RebuildRepoList();
        };
        var selectNone = CoverageTheme.MakeButton("None");
        selectNone.Click += (_, _) =>
        {
            _session.SelectAll(false);
            RebuildRepoList();
        };

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { selectAll, selectNone },
        };
        DockPanel.SetDock(buttons, Dock.Right);
        header.Children.Add(buttons);
        header.Children.Add(CoverageTheme.Title("Repos", 13));

        var hint = CoverageTheme.Label("Checked repos are included in runs.", muted: true);
        hint.Margin = new Thickness(0, 0, 0, 6);

        _repoList.Background = CoverageTheme.PanelAltBrush;
        _repoList.BorderBrush = CoverageTheme.BorderBrush;
        _repoList.VerticalAlignment = VerticalAlignment.Stretch;
        ScrollViewer.SetVerticalScrollBarVisibility(_repoList, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(_repoList, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
        };
        grid.Children.Add(header);
        Grid.SetRow(hint, 1);
        grid.Children.Add(hint);
        Grid.SetRow(_repoList, 2);
        grid.Children.Add(_repoList);

        return CoverageTheme.PanelBox(grid);
    }

    private TabControl BuildTabs() => new()
    {
        Items =
        {
            new TabItem { Header = "Results", Content = CoverageTheme.PanelBox(_repoGrid, new Thickness(4)) },
            new TabItem { Header = "Gaps", Content = CoverageTheme.PanelBox(_gapGrid, new Thickness(4)) },
            new TabItem { Header = "Complexity / CRAP", Content = BuildComplexityPane() },
            new TabItem { Header = "Details", Content = CoverageTheme.PanelBox(_details, new Thickness(4)) },
        },
    };

    private Control BuildComplexityPane()
    {
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(4, 4, 4, 8),
            Children =
            {
                _flaggedOnlyBox,
                CoverageTheme.Label("CRAP descending · CC = cyclomatic complexity", muted: true),
            },
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(bar);
        Grid.SetRow(_crapGrid, 1);
        grid.Children.Add(_crapGrid);
        return CoverageTheme.PanelBox(grid, new Thickness(4));
    }

    private void ConfigureGrids()
    {
        foreach (var g in new[] { _repoGrid, _gapGrid, _crapGrid })
        {
            g.Background = CoverageTheme.PanelAltBrush;
            g.Foreground = CoverageTheme.TextBrush;
            g.BorderBrush = CoverageTheme.BorderBrush;
        }

        _repoGrid.SetColumns(
        [
            SortableDataGrid.TextColumn("Repo", nameof(CoverageRepoRow.Repo), 160),
            SortableDataGrid.TextColumn("Status", nameof(CoverageRepoRow.Status), 70),
            SortableDataGrid.TextColumn("Line", nameof(CoverageRepoRow.LineDisplay), 70),
            SortableDataGrid.TextColumn("Branch", nameof(CoverageRepoRow.BranchDisplay), 70),
            SortableDataGrid.TextColumn("Tests", nameof(CoverageRepoRow.TestsTotal), 60),
            SortableDataGrid.TextColumn("Pass", nameof(CoverageRepoRow.TestsPassed), 60),
            SortableDataGrid.TextColumn("Fail", nameof(CoverageRepoRow.TestsFailed), 50),
            SortableDataGrid.TextColumn("Sec", nameof(CoverageRepoRow.Seconds), 60),
            SortableDataGrid.TextColumn("Error", nameof(CoverageRepoRow.Error)),
        ]);

        _gapGrid.SetColumns(
        [
            SortableDataGrid.TextColumn("Package", nameof(PackageGapRow.Package), 280),
            SortableDataGrid.TextColumn("Line %", nameof(PackageGapRow.LinePercent), 70),
            SortableDataGrid.TextColumn("Branch %", nameof(PackageGapRow.BranchPercent), 80),
            SortableDataGrid.TextColumn("Line gap", nameof(PackageGapRow.LineGap), 80),
            SortableDataGrid.TextColumn("Branch gap", nameof(PackageGapRow.BranchGap), 90),
        ]);

        _crapGrid.SetColumns(
        [
            SortableDataGrid.TextColumn("CRAP", nameof(ComplexityRow.Crap), 70),
            SortableDataGrid.TextColumn("CC", nameof(ComplexityRow.Complexity), 50),
            SortableDataGrid.TextColumn("Line %", nameof(ComplexityRow.LinePercent), 70),
            SortableDataGrid.TextColumn("Branch %", nameof(ComplexityRow.BranchPercent), 80),
            SortableDataGrid.TextColumn("Package", nameof(ComplexityRow.Package), 180),
            SortableDataGrid.TextColumn("Method", nameof(ComplexityRow.Method)),
            SortableDataGrid.TextColumn("File", nameof(ComplexityRow.File), 160),
        ]);
    }

    private void SyncOptionsToSession()
    {
        _session.SetRoot(_rootBox.Text ?? _session.Root);
        _session.OutputDir = string.IsNullOrWhiteSpace(_outBox.Text)
            ? Path.Combine(_session.Root, "coverage")
            : Path.GetFullPath(_outBox.Text);
        _session.PlatformMode = _platformBox.IsChecked == true;
        _session.SkipBuild = _skipBuildBox.IsChecked == true;
        _session.RegenerateSlnx = _regenBox.IsChecked == true;
        _session.FailBelow = (double)(_failBelow.Value ?? -1);
        _session.CrapThreshold = (double)(_crapThreshold.Value ?? (decimal)CrapScore.DefaultThreshold);
        _session.ThrottleLimit = (int)(_throttle.Value ?? 0);
        _session.HostTimeoutSeconds = (int)(_timeout.Value ?? 60);
        _outBox.Text = _session.OutputDir;
        _rootBox.Text = _session.Root;
    }

    private void RefreshFromSession()
    {
        RebuildRepoList();
        _repoGrid.ItemsSource = _session.RepoRows();
        _gapGrid.ItemsSource = _session.GapRows();
        RefreshComplexityGrid();
        RefreshDetails();
        RefreshRun();

        var line = _session.LastCollect?.AggregateLinePercent;
        var branch = _session.LastCollect?.AggregateBranchPercent;
        var crap = _session.LastCrap;
        _summary.Text =
            $"Repos {_session.Repos.Count} selected {_session.SelectedRepoNames().Count} · " +
            $"coverage line {(line is null ? "—" : $"{line:0.0}%")} / branch {(branch is null ? "—" : $"{branch:0.0}%")} · " +
            $"CRAP flagged {crap?.FlaggedCount.ToString() ?? "—"} / methods {crap?.Methods.Count.ToString() ?? "—"}";
        _feedback.SetStatus(_summary.Text);
    }

    private void RefreshRun()
    {
        if (_session.ActiveRun is { } run)
        {
            if (!ReferenceEquals(_runViewTag, run))
            {
                _runView.Bind(run);
                _runViewTag = run;
            }
            else
            {
                _runView.Refresh();
            }
        }
    }

    private WorkRun? _runViewTag;

    private void RefreshComplexityGrid() =>
        _crapGrid.ItemsSource = _session.ComplexityRows(_flaggedOnlyBox.IsChecked == true);

    private void RefreshDetails()
    {
        var children = new List<DetailTreeNode>();
        if (_session.LastCollect is { } collect)
        {
            children.Add(new DetailTreeNode("Last collect",
                $"line {collect.AggregateLinePercent:0.0}% · branch {collect.AggregateBranchPercent:0.0}% · {collect.DurationSeconds:0.0}s",
                collect.Repos.Select(r => new DetailTreeNode(
                    r.Repo,
                    $"{r.Status} · line {r.LinePercent:0.0}% · tests {r.TestsPassed}/{r.TestsTotal}")).ToList()));
        }

        if (_session.LastDocument is { } doc)
        {
            var shortfall = CoverageAnalyzer.Shortfall(doc.Summary, 95);
            children.Add(new DetailTreeNode("Cobertura", doc.SourcePath,
            [
                new DetailTreeNode("Line", $"{doc.Summary.LinePercent:0.0}% ({doc.Summary.LinesCovered}/{doc.Summary.LinesValid})"),
                new DetailTreeNode("Branch", $"{doc.Summary.BranchPercent:0.0}% ({doc.Summary.BranchesCovered}/{doc.Summary.BranchesValid})"),
                new DetailTreeNode("To 95%", $"+{shortfall.LinesNeeded} lines, +{shortfall.BranchesNeeded} branches"),
                new DetailTreeNode("Packages", doc.Packages.Count.ToString()),
            ]));
        }

        if (_session.LastCrap is { } crap)
        {
            children.Add(new DetailTreeNode("CRAP",
                $"threshold {crap.Threshold} · flagged {crap.FlaggedCount} · max {crap.MaxScore:0.##}",
                crap.Methods.Where(m => m.Flagged).Take(12).Select(m =>
                    new DetailTreeNode($"{m.Score:0.#}", $"CC {m.Method.Complexity} · {m.Method.DisplayName}")).ToList()));
        }

        _details.SetRoot(new DetailTreeNode("Session", _session.Root, children));
    }

    private void RebuildRepoList()
    {
        _repoList.Items.Clear();
        foreach (var repo in _session.Repos)
        {
            var local = repo;
            var box = CoverageTheme.MakeCheck($"{repo.Name}  ({repo.HostCount} hosts)", repo.IsSelected);
            box.IsCheckedChanged += (_, _) =>
            {
                local.IsSelected = box.IsChecked == true;
                _summary.Text =
                    $"Repos {_session.Repos.Count} selected {_session.SelectedRepoNames().Count}";
            };
            _repoList.Items.Add(new ListBoxItem { Content = box, Tag = local });
        }
    }

    private void TryLoadExistingArtifacts()
    {
        var cobertura = Path.Combine(_session.OutputDir, "Cobertura.xml");
        var reportCob = Path.Combine(_session.OutputDir, "report", "Cobertura.xml");
        if (File.Exists(cobertura))
            _session.LoadCobertura(cobertura);
        else if (File.Exists(reportCob))
            _session.LoadCobertura(reportCob);

        var index = Path.Combine(_session.OutputDir, "index.html");
        if (File.Exists(index))
            _session.SetHtmlIndexPath(index);

        try { _session.AnalyzeCrap(); }
        catch { /* no cobertura yet */ }

        RefreshFromSession();
    }

    private async Task BrowseRootAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Novolis workspace root",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
            return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;
        _rootBox.Text = path;
        SyncOptionsToSession();
        _session.Discover();
    }

    private async Task LoadCoberturaAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Cobertura XML",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Cobertura") { Patterns = ["*.xml"] }],
        });
        if (files.Count == 0)
            return;
        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;
        SafeUi(() =>
        {
            _session.LoadCobertura(path);
            try { _session.AnalyzeCrap(); } catch { /* ignore */ }
            RefreshFromSession();
            _tabs.SelectedIndex = 1;
            _feedback.Flash("Cobertura loaded.");
        });
    }

    private void OpenHtmlReport()
    {
        var path = _session.HtmlIndexPath
                   ?? _session.ActiveRun?.HtmlIndexPath
                   ?? Path.Combine(_session.OutputDir, "index.html");
        if (!File.Exists(path))
        {
            _feedback.FlashError("No HTML report yet. Collect coverage first.");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _feedback.FlashError(ex.Message);
        }
    }

    private void SafeUi(Action action)
    {
        try
        {
            action();
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            _feedback.FlashError(ex.Message);
        }
    }

    private async Task RunSafeAsync(string busy, string success, Func<Task> action)
    {
        if (_busyUi)
            return;
        _busyUi = true;
        try
        {
            // Soft busy: keep the run panel interactive; avoid full-window overlay for long batches.
            _feedback.SetStatus(busy);
            await action();
            _feedback.Flash(success);
            RefreshFromSession();
        }
        catch (OperationCanceledException)
        {
            _feedback.FlashWarning("Cancelled.");
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            _feedback.FlashError(ex.Message);
            RefreshFromSession();
        }
        finally
        {
            _busyUi = false;
        }
    }
}
