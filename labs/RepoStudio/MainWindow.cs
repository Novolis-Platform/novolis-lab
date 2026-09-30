using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.Git;
using Novolis.IO.Git;

namespace RepoStudio;

internal sealed class MainWindow : Window
{
    static readonly IBrush PaneBg = new SolidColorBrush(Color.FromRgb(32, 34, 40));
    static readonly IBrush PaneBorder = new SolidColorBrush(Color.FromRgb(55, 58, 66));
    static readonly IBrush ShellBg = new SolidColorBrush(Color.FromRgb(22, 24, 28));
    static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(56, 140, 160));

    readonly GitRepositoryService _git = new();
    readonly GitRepoVisualizer _repos = new();
    readonly GitBranchNavigator _branches = new();
    readonly GitStashPanel _stashes = new();
    readonly GitCommitGraphView _graph = new();
    readonly GitCommitDetailView _detail = new();
    readonly GitDiffView _diff = new();
    readonly GitWorkingTreeView _working = new();
    readonly GitActionBar _actions = new();
    readonly GitFetchAgeLabel _fetchAge = new();
    TabControl? _detailTabs;
    readonly TextBlock _openLabel = new()
    {
        Text = "No repo open",
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(12, 0, 0, 0),
        Opacity = 0.85,
    };
    readonly TextBlock _status = new()
    {
        Text = "Ready",
        Margin = new Thickness(10, 4),
        VerticalAlignment = VerticalAlignment.Center,
    };

    string _root = "";
    string? _openRepoPath;
    string? _openRepoName;
    string? _openBranch;
    string? _openUpstream;
    int _openAhead;
    bool _openDirty;
    int _openChangeCount;
    FetchScheduler? _scheduler;
    int _matrixGen;
    int _openGen;
    bool _busy;

    public MainWindow()
    {
        Title = "Repo Studio";
        Width = 1440;
        Height = 920;
        MinWidth = 1100;
        MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ShellBg;
        Foreground = Brushes.WhiteSmoke;

        _repos.RepoOpenRequested += (_, e) => _ = OpenRepoAsync(e.Repo);
        _actions.CommandRequested += (_, e) => _ = OnCommandAsync(e);
        _stashes.CommandRequested += (_, e) => _ = OnCommandAsync(e);
        _branches.RefActivated += (_, e) => _ = CheckoutAsync(e.Tip.Name);
        _graph.CommitSelected += (_, e) => _ = ShowCommitAsync(e.Node);

        Content = BuildLayout();
        Opened += (_, _) => _ = OnOpenedAsync();
        Closed += (_, _) => _scheduler?.Stop();
    }

    async Task OnOpenedAsync()
    {
        try
        {
            _root = await Task.Run(() => GitWorkspace.ResolveRoot()).ConfigureAwait(true);
            Title = $"Repo Studio — {_root}";
            Flash("Loading workspace…");
            _ = RefreshMatrixAsync(includeStashCount: false);

            _scheduler = new FetchScheduler(_git);
            _scheduler.CycleCompleted += (_, _) =>
                Dispatcher.UIThread.Post(() => _ = RefreshMatrixAsync(includeStashCount: false));
            _scheduler.Start(_root, TimeSpan.FromMinutes(10), delayBeforeFirst: true);
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
    }

    Control BuildLayout()
    {
        var tabs = new TabControl
        {
            MinHeight = 200,
            Items =
            {
                new TabItem { Header = "Working tree", Content = _working },
                new TabItem { Header = "Diff", Content = _diff },
                new TabItem { Header = "Detail", Content = _detail },
            },
        };
        _detailTabs = tabs;

        var left = Pane(
            "Repositories",
            _repos,
            headerRight: _fetchAge);

        var mid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,170"),
            MinWidth = 200,
        };
        mid.Children.Add(Pane("Branches", _branches));
        var stashPane = Pane("Stashes", _stashes);
        Grid.SetRow(stashPane, 1);
        mid.Children.Add(stashPane);

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("*,240"),
            MinWidth = 420,
        };
        right.Children.Add(Pane("History", _graph));
        var tabPane = Pane(null, tabs);
        Grid.SetRow(tabPane, 1);
        right.Children.Add(tabPane);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,8,220,8,*"),
            Margin = new Thickness(8),
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(mid, 2);
        Grid.SetColumn(right, 4);
        body.Children.Add(left);
        body.Children.Add(mid);
        body.Children.Add(right);
        body.Children.Add(VSplit(1));
        body.Children.Add(VSplit(3));

        var toolbar = new Border
        {
            Background = PaneBg,
            BorderBrush = PaneBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    _openLabel,
                    _actions,
                },
            },
        };
        DockPanel.SetDock(_openLabel, Dock.Right);

        var statusBar = new Border
        {
            Background = PaneBg,
            BorderBrush = PaneBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _status,
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(statusBar, 2);
        root.Children.Add(toolbar);
        root.Children.Add(body);
        root.Children.Add(statusBar);
        return root;
    }

    static Control VSplit(int column)
    {
        var split = new GridSplitter
        {
            Width = 8,
            ResizeDirection = GridResizeDirection.Columns,
            Background = ShellBg,
        };
        Grid.SetColumn(split, column);
        return split;
    }

    static Border Pane(string? title, Control body, Control? headerRight = null)
    {
        body.HorizontalAlignment = HorizontalAlignment.Stretch;
        body.VerticalAlignment = VerticalAlignment.Stretch;

        var content = new DockPanel { LastChildFill = true, Margin = new Thickness(4) };
        if (!string.IsNullOrEmpty(title) || headerRight is not null)
        {
            var header = new DockPanel { Margin = new Thickness(6, 4, 6, 6) };
            if (headerRight is not null)
            {
                header.Children.Add(headerRight);
                DockPanel.SetDock(headerRight, Dock.Right);
            }

            if (!string.IsNullOrEmpty(title))
            {
                header.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            content.Children.Add(header);
            DockPanel.SetDock(header, Dock.Top);
        }

        content.Children.Add(body);

        return new Border
        {
            Background = PaneBg,
            BorderBrush = PaneBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            Child = content,
        };
    }

    async Task RefreshMatrixAsync(bool includeStashCount = false)
    {
        if (string.IsNullOrEmpty(_root))
            return;

        var gen = Interlocked.Increment(ref _matrixGen);
        try
        {
            var matrix = await GitWorkspace.GetStatusMatrixAsync(
                _root,
                _git,
                includeStashCount: includeStashCount,
                parallel: 8,
                liteStatus: true).ConfigureAwait(false);

            if (gen != _matrixGen)
                return;

            var shouldAutoOpen = _openRepoPath is null && matrix.Repos.Count > 0;
            var autoRepo = shouldAutoOpen
                ? matrix.Repos.FirstOrDefault(r => r.Status?.Dirty == true)?.Repo
                  ?? matrix.Repos[0].Repo
                : null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _repos.SetMatrix(matrix);
                var stamps = matrix.Repos
                    .Select(r => r.LastFetchAt)
                    .Where(t => t.HasValue)
                    .Select(t => t!.Value)
                    .ToArray();
                _fetchAge.SetLastFetch(stamps.Length == 0 ? null : stamps.Max());
                Flash($"{matrix.Summary.Git} repos · dirty {matrix.Summary.Dirty} · behind {matrix.Summary.Behind}");
            });

            if (autoRepo is not null)
                await OpenRepoAsync(autoRepo).ConfigureAwait(false);

            if (!includeStashCount)
                _ = RefreshMatrixAsync(includeStashCount: true);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Flash(ex.Message));
        }
    }

    async Task OpenRepoAsync(RepoEntry repo)
    {
        if (string.Equals(_openRepoPath, repo.Path, StringComparison.OrdinalIgnoreCase)
            && _openGen > 0)
        {
            // Same repo re-selected after matrix refresh — keep panes, light refresh.
            await RefreshOpenRepoAsync().ConfigureAwait(false);
            return;
        }

        var gen = Interlocked.Increment(ref _openGen);
        _openRepoPath = repo.Path;
        _openRepoName = repo.Name;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _repos.SelectRepo(repo.Path);
            _openLabel.Text = repo.Name;
            _openLabel.Foreground = Accent;
            _branches.ShowPlaceholder("Loading…");
            _graph.ShowPlaceholder("Loading history…");
            _working.ShowPlaceholder("Loading…");
            Flash($"Opening {repo.Name}…");
        });

        await RefreshOpenRepoAsync(gen).ConfigureAwait(false);
    }

    async Task RefreshOpenRepoAsync(int? expectedGen = null)
    {
        if (_openRepoPath is null)
            return;

        var path = _openRepoPath;
        var name = _openRepoName ?? System.IO.Path.GetFileName(path);
        var gen = expectedGen ?? _openGen;
        try
        {
            var snapshot = await Task.Run(() =>
            {
                var branches = _git.ListBranches(path);
                var stashes = _git.ListStashes(path);
                var graph = _git.GetCommitGraph(path, new CommitGraphOptions { MaxCount = 120 });
                var wt = _git.GetWorkingTree(path);
                var status = _git.GetStatus(path, lite: true);
                return (branches, stashes, graph, wt, status);
            }).ConfigureAwait(false);

            if (gen != _openGen)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _branches.SetBranches(snapshot.branches);
                _stashes.SetStashes(snapshot.stashes);
                _graph.SetGraph(snapshot.graph);
                _working.SetWorkingTree(snapshot.wt);
                _openBranch = snapshot.status.Branch;
                _openUpstream = snapshot.status.Upstream;
                _openAhead = snapshot.status.Ahead;
                _openDirty = snapshot.status.Dirty;
                _openChangeCount = snapshot.wt.Staged.Count + snapshot.wt.Unstaged.Count + snapshot.wt.Untracked.Count;
                Flash($"Opened {name}");
            });
        }
        catch (Exception ex)
        {
            if (gen != _openGen)
                return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _branches.ShowPlaceholder(ex.Message);
                _graph.ShowPlaceholder(ex.Message);
                Flash(ex.Message);
            });
        }
    }

    async Task ShowCommitAsync(CommitNode? node)
    {
        if (_openRepoPath is null || node is null)
            return;
        var path = _openRepoPath;
        var sha = node.Sha;
        try
        {
            var (detail, diff) = await Task.Run(() =>
                (_git.GetCommitDetail(path, sha), _git.GetDiff(path, sha))).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _detail.SetDetail(detail);
                _diff.SetDiff(diff);
                if (_detailTabs is not null)
                    _detailTabs.SelectedIndex = 1; // Diff
            });
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
    }

    async Task CheckoutAsync(string tipName)
    {
        if (_openRepoPath is null)
            return;

        var dirtyNote = _openDirty
            ? $"Working tree has {_openChangeCount} uncommitted change(s). Checkout may fail or carry changes."
            : "Working tree is clean.";
        var ok = await ConfirmAsync(new GitConfirmRequest
        {
            Title = "Checkout ref",
            Severity = _openDirty ? GitConfirmSeverity.Warning : GitConfirmSeverity.Info,
            Summary = $"Checkout “{tipName}” in {_openRepoName}?",
            Detail = $"{dirtyNote}\nCurrent branch: {_openBranch ?? "(unknown)"}",
            ConfirmLabel = "Checkout",
        }).ConfigureAwait(true);
        if (!ok)
        {
            Flash("Checkout cancelled.");
            return;
        }

        var path = _openRepoPath;
        Flash($"Checkout {tipName}…");
        var result = await Task.Run(() => _git.Checkout(path, tipName)).ConfigureAwait(false);
        Flash(result.Message);
        if (result.Ok)
            await RefreshOpenRepoAsync().ConfigureAwait(false);
    }

    async Task OnCommandAsync(GitChromeCommandEventArgs e)
    {
        if (_busy && e.Command is not GitChromeCommand.Refresh)
        {
            Flash("Busy…");
            return;
        }

        _busy = true;
        try
        {
            switch (e.Command)
            {
                case GitChromeCommand.Refresh:
                    Flash("Refreshing…");
                    await RefreshMatrixAsync(includeStashCount: true).ConfigureAwait(false);
                    await RefreshOpenRepoAsync().ConfigureAwait(false);
                    break;
                case GitChromeCommand.Fetch:
                {
                    Flash("Fetching…");
                    var sel = _repos.GetSelection();
                    var repos = await Task.Run(() =>
                    {
                        if (sel.Selected.Count > 0)
                            return sel.Selected;
                        return GitWorkspace.SelectByNames(GitWorkspace.Discover(_root), null);
                    }).ConfigureAwait(false);
                    var batch = new GitWorkspaceBatch(_git);
                    var result = await batch.FetchAsync(repos, new BatchOptions { WorkspaceRoot = _root })
                        .ConfigureAwait(false);
                    Flash($"Fetch ok={result.Ok}");
                    _ = RefreshMatrixAsync(includeStashCount: false);
                    await RefreshOpenRepoAsync().ConfigureAwait(false);
                    break;
                }
                case GitChromeCommand.Pull:
                {
                    var sel = _repos.GetSelection();
                    if (sel.Selected.Count == 0)
                    {
                        Flash("Select repos to pull.");
                        break;
                    }

                    var names = string.Join('\n', sel.Selected.Take(12).Select(r => $"• {r.Name}"));
                    if (sel.Selected.Count > 12)
                        names += $"\n… +{sel.Selected.Count - 12} more";

                    var pullOk = await ConfirmAsync(new GitConfirmRequest
                    {
                        Title = "Pull (fast-forward only)",
                        Severity = GitConfirmSeverity.Warning,
                        Summary = $"Fast-forward pull in {sel.Selected.Count} selected repo(s)? Non–fast-forward updates will fail safely (no merge/rebase).",
                        Detail = names,
                        ConfirmLabel = "Pull ff-only",
                    }).ConfigureAwait(true);
                    if (!pullOk)
                    {
                        Flash("Pull cancelled.");
                        break;
                    }

                    Flash("Pulling…");
                    var batch = new GitWorkspaceBatch(_git);
                    var result = await batch.PullFfOnlyAsync(sel.Selected, new BatchOptions { WorkspaceRoot = _root })
                        .ConfigureAwait(false);
                    Flash($"Pull failures={result.Results.Count(r => r.Outcome == "failed")}");
                    _ = RefreshMatrixAsync(includeStashCount: false);
                    await RefreshOpenRepoAsync().ConfigureAwait(false);
                    break;
                }
                case GitChromeCommand.Push:
                    if (_openRepoPath is null) { Flash("Open a repo first."); break; }
                    {
                        var pushOk = await ConfirmAsync(new GitConfirmRequest
                        {
                            Title = "Push",
                            Severity = _openAhead > 0 ? GitConfirmSeverity.Info : GitConfirmSeverity.Warning,
                            Summary = $"Push {_openRepoName} without force?",
                            Detail =
                                $"Branch: {_openBranch ?? "(unknown)"}\n" +
                                $"Upstream: {_openUpstream ?? "(none — may need set-upstream)"}\n" +
                                $"Ahead: {_openAhead} commit(s)\n" +
                                "Force push is never offered from this UI.",
                            ConfirmLabel = "Push",
                        }).ConfigureAwait(true);
                        if (!pushOk)
                        {
                            Flash("Push cancelled.");
                            break;
                        }

                        var path = _openRepoPath;
                        var r = await Task.Run(() => _git.Push(path, new PushOptions { Force = false }))
                            .ConfigureAwait(false);
                        Flash(r.Message);
                    }
                    break;
                case GitChromeCommand.StashPush:
                case GitChromeCommand.StashApply:
                case GitChromeCommand.StashPop:
                case GitChromeCommand.StashDrop:
                    await StashAsync(e).ConfigureAwait(false);
                    break;
                case GitChromeCommand.CreateBranch:
                    await CreateBranchAsync().ConfigureAwait(false);
                    break;
                case GitChromeCommand.BranchCut:
                    await BranchCutAsync().ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    async Task StashAsync(GitChromeCommandEventArgs e)
    {
        if (_openRepoPath is null)
        {
            Flash("Open a repo first.");
            return;
        }

        var path = _openRepoPath;
        var idx = e.StashIndex ?? 0;
        var stashLabel = $"stash@{{{idx}}}  {e.Detail ?? ""}".Trim();

        if (e.Command is GitChromeCommand.StashDrop)
        {
            var dropOk = await ConfirmAsync(new GitConfirmRequest
            {
                Title = "Drop stash permanently",
                Severity = GitConfirmSeverity.Danger,
                Summary = "This permanently deletes the stash entry. It cannot be undone from Repo Studio.",
                Detail = $"{_openRepoName}\n{stashLabel}",
                ConfirmLabel = "Drop stash",
                RequireTypedPhrase = "drop",
                TypedPhraseHint = "Type drop to enable the Drop stash button.",
            }).ConfigureAwait(true);
            if (!dropOk)
            {
                Flash("Stash drop cancelled.");
                return;
            }
        }
        else if (e.Command is GitChromeCommand.StashPop)
        {
            var popOk = await ConfirmAsync(new GitConfirmRequest
            {
                Title = "Pop stash",
                Severity = GitConfirmSeverity.Warning,
                Summary = "Apply the stash, then remove it from the stash list.",
                Detail = $"{_openRepoName}\n{stashLabel}" +
                         (_openDirty ? $"\nWorking tree already dirty ({_openChangeCount} change(s)) — conflicts possible." : ""),
                ConfirmLabel = "Pop stash",
            }).ConfigureAwait(true);
            if (!popOk)
            {
                Flash("Stash pop cancelled.");
                return;
            }
        }
        else if (e.Command is GitChromeCommand.StashApply && _openDirty)
        {
            var applyOk = await ConfirmAsync(new GitConfirmRequest
            {
                Title = "Apply stash onto dirty tree",
                Severity = GitConfirmSeverity.Warning,
                Summary = "Working tree already has local changes. Applying a stash may conflict.",
                Detail = $"{_openRepoName}\n{stashLabel}\nDirty files: {_openChangeCount}",
                ConfirmLabel = "Apply anyway",
            }).ConfigureAwait(true);
            if (!applyOk)
            {
                Flash("Stash apply cancelled.");
                return;
            }
        }
        else if (e.Command is GitChromeCommand.StashPush && !_openDirty)
        {
            Flash("Nothing to stash (working tree clean).");
            return;
        }

        var r = await Task.Run(() => e.Command switch
        {
            GitChromeCommand.StashPush => _git.StashPush(path),
            GitChromeCommand.StashApply => _git.StashApply(path, idx),
            GitChromeCommand.StashPop => _git.StashPop(path, idx),
            _ => _git.StashDrop(path, idx),
        }).ConfigureAwait(false);
        Flash(r.Message);
        await RefreshOpenRepoAsync().ConfigureAwait(false);
    }

    async Task CreateBranchAsync()
    {
        if (_openRepoPath is null)
        {
            Flash("Open a repo first.");
            return;
        }

        var body = new GitCreateBranchDialog();
        var ok = await ShowDialogAsync("Create branch", body).ConfigureAwait(true);
        if (!ok)
            return;
        var opts = body.TryRead();
        if (opts is null)
        {
            Flash("Branch name required.");
            return;
        }

        var path = _openRepoPath;
        var r = await Task.Run(() => _git.CreateBranch(path, opts)).ConfigureAwait(false);
        Flash(r.Message);
        await RefreshOpenRepoAsync().ConfigureAwait(false);
    }

    async Task BranchCutAsync()
    {
        var sel = _repos.GetSelection();
        if (sel.Selected.Count == 0)
        {
            Flash("Select repos for branch cut.");
            return;
        }

        var body = new GitBranchCutDialog();
        var ok = await ShowDialogAsync("Branch cut", body).ConfigureAwait(true);
        if (!ok || string.IsNullOrWhiteSpace(body.BranchName))
            return;

        var planner = new BranchCutPlanner(_git);
        var plan = await Task.Run(() =>
            planner.Plan(_root, body.BranchName, sel.Selected, body.BaseRef)).ConfigureAwait(false);
        body.SetPreview(plan);
        var preview = await ShowDialogAsync("Review dry-run", body, okLabel: "Continue…").ConfigureAwait(true);
        if (!preview)
        {
            Flash("Branch cut cancelled.");
            return;
        }

        var runnable = plan.Steps.Count(s => s.BlockReason is null);
        if (runnable == 0)
        {
            Flash("Branch cut: nothing to apply (all repos blocked).");
            return;
        }

        var apply = await ConfirmAsync(new GitConfirmRequest
        {
            Title = "Apply branch cut",
            Severity = GitConfirmSeverity.Danger,
            Summary = $"Create/checkout “{body.BranchName}” in {runnable} repo(s) (of {plan.Steps.Count} planned).",
            Detail = string.Join('\n', plan.Steps.Select(s =>
                s.BlockReason is null
                    ? $"APPLY  {s.Repo.Name}"
                    : $"SKIP   {s.Repo.Name} — {s.BlockReason}")),
            ConfirmLabel = "Apply branch cut",
            RequireTypedPhrase = body.BranchName,
            TypedPhraseHint = $"Type the branch name ({body.BranchName}) to enable apply.",
        }).ConfigureAwait(true);
        if (!apply)
        {
            Flash("Branch cut cancelled.");
            return;
        }

        Flash("Applying branch cut…");
        var applied = await planner.ApplyAsync(plan, dryRun: false).ConfigureAwait(false);
        Flash($"Branch cut ok={applied.Ok}");
        _ = RefreshMatrixAsync(includeStashCount: false);
    }

    Task<bool> ConfirmAsync(GitConfirmRequest request) =>
        GitConfirmDialog.ShowAsync(this, request);

    async Task<bool> ShowDialogAsync(string title, Control body, string okLabel = "OK")
    {
        var tcs = new TaskCompletionSource<bool>();
        Window? dlg = null;
        Button MakeButton(string label, bool result)
        {
            var b = new Button { Content = label, MinWidth = 80 };
            b.Click += (_, _) =>
            {
                tcs.TrySetResult(result);
                dlg?.Close();
            };
            return b;
        }

        dlg = new Window
        {
            Title = title,
            Width = 520,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ShellBg,
            Foreground = Brushes.WhiteSmoke,
            Content = new DockPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(8),
                        [DockPanel.DockProperty] = Dock.Bottom,
                        Children =
                        {
                            MakeButton("Cancel", false),
                            MakeButton(okLabel, true),
                        },
                    },
                    body,
                },
            },
        };

        await dlg.ShowDialog(this).ConfigureAwait(true);
        return await tcs.Task.ConfigureAwait(true);
    }

    void Flash(string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Flash(message));
            return;
        }

        _status.Text = message;
    }
}
