using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Controls;
using Novolis.Avalonia.Studio;
using Novolis.IO.Processes;

namespace StudioChromeLab;

internal sealed class MainWindow : Window
{
    readonly Border _menuBar;
    readonly Border _topBar;
    readonly Border _statusBar;
    readonly TextBlock _statusText;
    readonly ListBox _navHost;
    readonly JobQueuePanel _jobs;
    readonly StudioFeedback _feedback;
    readonly ProcessJobQueue _queue = new() { MaxParallel = 1 };
    bool _focus;
    bool _dirty;

    public MainWindow()
    {
        Title = "Novolis Studio Chrome Lab";
        Width = 1100;
        Height = 720;

        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        _menuBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(10, 6),
            Child = new TextBlock { Text = "File · View · Tools (demo menu chrome)" }
        };
        _topBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(10, 6),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock { Text = "Workspace · Series · Book (demo top bar)", Opacity = 0.85 }
        };

        _statusText = new TextBlock { Text = "Clean", Foreground = Brushes.White };
        AgentProperties.SetId(_statusText, "lab.status");
        _statusBar = new Border
        {
            Padding = new Thickness(12, 5),
            Background = StudioStatusBrushes.Clean,
            Child = _statusText
        };
        AgentProperties.SetId(_statusBar, "lab.statusBar");

        var chapters = new[]
        {
            new MarkedListRow("*", "1", "Departure", "812", "ch1"),
            new MarkedListRow(null, "2", "Quiet Harbor", "420", "ch2"),
            new MarkedListRow(null, "3", "Rimward", "1055", "ch3"),
            new MarkedListRow("*", "4", "Ansible Silence", "640", "ch4"),
        };
        _navHost = MarkedListBox.Create(chapters);
        AgentProperties.SetId(_navHost, "lab.nav", AgentRoleNames.ListBox);

        _jobs = new JobQueuePanel();
        AgentProperties.SetId(_jobs, "lab.jobs");
        SeedJobs();
        _jobs.CancelRequested += row =>
        {
            if (row.Tag is ProcessJob job)
                _queue.Cancel(job);
            _feedback.Flash($"Cancel requested: {row.Title}");
            RefreshJobs();
        };
        _jobs.OpenOutputRequested += row => _feedback.Flash($"Open output: {row.Title}");

        var btnRecovery = new Button { Content = "Fake recovery…" };
        AgentProperties.SetId(btnRecovery, "lab.recovery", AgentRoleNames.Button);
        btnRecovery.Click += async (_, _) =>
        {
            var id = await ChoiceDialog.ShowAsync(this, "Recovery available",
                "A recovery snapshot is newer than the file on disk.",
                "chapter-3.md · 2 minutes ago",
                [
                    new ChoiceOption("restore", "Restore recovery"),
                    new ChoiceOption("compare", "Compare"),
                    new ChoiceOption("discard", "Discard recovery"),
                    new ChoiceOption("keep", "Keep file", IsDefault: true, IsCancel: true)
                ]);
            _feedback.Flash($"Recovery choice: {id ?? "(dismissed)"}");
        };

        var btnConflict = new Button { Content = "Fake conflict…" };
        AgentProperties.SetId(btnConflict, "lab.conflict", AgentRoleNames.Button);
        btnConflict.Click += async (_, _) =>
        {
            var id = await ChoiceDialog.ShowAsync(this, "External change",
                "The file changed on disk while you were editing.",
                null,
                [
                    new ChoiceOption("keep", "Keep local", IsDefault: true),
                    new ChoiceOption("reload", "Reload disk"),
                    new ChoiceOption("compare", "Compare later", IsCancel: true)
                ]);
            _feedback.Flash($"Conflict choice: {id ?? "(dismissed)"}");
        };

        var btnGoTo = new Button { Content = "Go to…" };
        AgentProperties.SetId(btnGoTo, "lab.goto", AgentRoleNames.Button);
        btnGoTo.Click += async (_, _) =>
        {
            var picks = chapters.Select(c => c.Primary).ToList();
            var chosen = await FilteredPickerDialog<string>.ShowAsync(this, "Go To Chapter", picks, s => s);
            _feedback.Flash(chosen is null ? "Go To cancelled" : $"Go To: {chosen}");
        };

        var btnFocus = new Button { Content = "Toggle focus (F11)" };
        AgentProperties.SetId(btnFocus, "lab.focus", AgentRoleNames.Button);
        btnFocus.Click += (_, _) => ToggleFocus();

        var btnDirty = new Button { Content = "Toggle dirty" };
        AgentProperties.SetId(btnDirty, "lab.dirty", AgentRoleNames.Button);
        btnDirty.Click += (_, _) =>
        {
            _dirty = !_dirty;
            _statusBar.Background = StudioStatusBrushes.ForDirtyState(_dirty);
            _statusText.Text = _dirty ? "Dirty *" : "Clean";
            _feedback.SetStatus(_statusText.Text);
        };

        var btnEnqueue = new Button { Content = "Enqueue dotnet --info" };
        AgentProperties.SetId(btnEnqueue, "lab.enqueue", AgentRoleNames.Button);
        btnEnqueue.Click += (_, _) =>
        {
            _queue.Enqueue(new ProcessJobSpec
            {
                FileName = "dotnet",
                Arguments = ["--info"],
                Title = "dotnet --info",
                WorkingDirectory = AppContext.BaseDirectory
            });
            RefreshJobs();
            _feedback.Flash("Job enqueued.");
        };

        _queue.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshJobs);

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(8),
            Children = { btnRecovery, btnConflict, btnGoTo, btnFocus, btnDirty, btnEnqueue }
        };
        foreach (Control child in toolbar.Children)
            if (child is Button b)
                b.Margin = new Thickness(0, 0, 8, 8);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*,320") };
        var navTitle = new TextBlock { Text = "Chapters", FontWeight = FontWeight.Bold, Margin = new Thickness(8) };
        var navDock = new DockPanel();
        DockPanel.SetDock(navTitle, Dock.Top);
        navDock.Children.Add(navTitle);
        navDock.Children.Add(_navHost);
        var navBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = navDock
        };

        var center = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "StudioChromeLab dogfoods ChoiceDialog, FilteredPickerDialog, MarkedListBox, JobQueuePanel, StudioFocusMode, StudioStatusBrushes.",
                    TextWrapping = TextWrapping.Wrap
                },
                toolbar
            }
        };

        var jobsPanel = new StackPanel
        {
            Margin = new Thickness(8),
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Jobs", FontWeight = FontWeight.Bold },
                _jobs
            }
        };

        Grid.SetColumn(navBorder, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(jobsPanel, 2);
        body.Children.Add(navBorder);
        body.Children.Add(center);
        body.Children.Add(jobsPanel);

        var flashStatus = new DockPanel();
        DockPanel.SetDock(chrome.FlashLine, Dock.Bottom);
        DockPanel.SetDock(chrome.StatusLine, Dock.Bottom);
        flashStatus.Children.Add(chrome.FlashLine);
        flashStatus.Children.Add(chrome.StatusLine);

        var root = new DockPanel();
        DockPanel.SetDock(_menuBar, Dock.Top);
        DockPanel.SetDock(_topBar, Dock.Top);
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        DockPanel.SetDock(flashStatus, Dock.Bottom);
        root.Children.Add(_menuBar);
        root.Children.Add(_topBar);
        root.Children.Add(_statusBar);
        root.Children.Add(flashStatus);
        root.Children.Add(body);
        Content = root;

        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.F11)
            {
                ToggleFocus();
                e.Handled = true;
            }
        };

        Opened += (_, _) => _feedback.Flash("Try recovery/conflict dialogs, Go To, focus mode, dirty bar, and jobs.");
    }

    void ToggleFocus()
    {
        _focus = !_focus;
        StudioFocusMode.Apply(_focus, _menuBar, _topBar, _statusBar);
        _feedback.Flash(_focus ? "Focus mode on" : "Focus mode off");
    }

    void SeedJobs()
    {
        _jobs.SetJobs(
        [
            new JobQueueRow
            {
                Title = "Build PDF (demo)",
                StatusLabel = "Succeeded",
                Detail = "out/book.pdf",
                LogTail = "Wrote out/book.pdf",
                CanCancel = false,
                CanOpenOutput = true,
                Tag = "demo-pdf"
            }
        ]);
    }

    void RefreshJobs()
    {
        var rows = new List<IJobQueueRow>
        {
            new JobQueueRow
            {
                Title = "Build PDF (demo)",
                StatusLabel = "Succeeded",
                Detail = "out/book.pdf",
                LogTail = "Wrote out/book.pdf",
                CanCancel = false,
                CanOpenOutput = true,
                Tag = "demo-pdf"
            }
        };
        foreach (var job in _queue.Jobs.ToArray().Reverse())
        {
            rows.Add(new JobQueueRow
            {
                Title = job.Title,
                StatusLabel = job.Status.ToString(),
                Detail = job.Detail,
                LogTail = job.Detail,
                CanCancel = job.CanCancel,
                CanOpenOutput = job.Status == ProcessJobStatus.Succeeded,
                Tag = job
            });
        }

        _jobs.SetJobs(rows);
    }
}
