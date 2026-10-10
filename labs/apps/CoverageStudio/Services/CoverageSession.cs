using CoverageStudio.Models;
using Novolis.Tools.Coverage;

namespace CoverageStudio.Services;

/// <summary>Workspace discovery, typed test/coverage runs, and Cobertura / CRAP analysis.</summary>
internal sealed class CoverageSession
{
    private readonly WorkspaceRunner _runner = new();
    private CancellationTokenSource? _runCts;
    private bool _active;

    public string Root { get; private set; } = TryResolveRoot();
    public string OutputDir { get; set; }
    public bool PlatformMode { get; set; } = true;
    public bool SkipBuild { get; set; } = true;
    public bool RegenerateSlnx { get; set; }
    public double FailBelow { get; set; } = -1;
    public double CrapThreshold { get; set; } = CrapScore.DefaultThreshold;
    public int ThrottleLimit { get; set; } = 4;

    /// <summary>Per-host <c>dotnet</c> wall timeout in seconds (default 60). Use 0 to disable.</summary>
    public int HostTimeoutSeconds { get; set; } = 60;

    public CoverageSession()
    {
        OutputDir = Path.Combine(Root, "coverage");
    }

    public IReadOnlyList<RepoListItem> Repos { get; private set; } = [];
    public CoverageCollectResult? LastCollect { get; private set; }
    public CrapReport? LastCrap { get; private set; }
    public CoberturaDocument? LastDocument { get; private set; }
    public WorkRun? ActiveRun { get; private set; }
    public string? HtmlIndexPath { get; private set; }
    public bool IsBusy => _active;

    public event Action? Changed;
    public event Action? RunChanged;

    public void SetRoot(string root)
    {
        Root = Path.GetFullPath(root);
        OutputDir = Path.Combine(Root, "coverage");
        RaiseChanged();
    }

    public void Discover()
    {
        EnsureRoot();
        var excludeFile = CoverageWorkspace.DefaultExcludeFile(Root);
        var excludes = CoverageWorkspace.ReadExcludes(excludeFile, []);
        IReadOnlyList<CoverageRepo> discovered = PlatformMode
            ? TestHostDiscovery.DiscoverFromPlatformSlnx(
                Root,
                CoverageWorkspace.ResolvePlatformSlnx(Root),
                excludes,
                include: null)
            : TestHostDiscovery.DiscoverRepos(Root, excludes, include: null);

        var previous = Repos.ToDictionary(r => r.Name, r => r.IsSelected, StringComparer.OrdinalIgnoreCase);
        Repos = discovered
            .Select(r => new RepoListItem
            {
                Name = r.Name,
                Path = r.Path,
                HostCount = r.TestProjects.Count,
                Solution = r.Solution,
                IsSelected = previous.TryGetValue(r.Name, out var sel) ? sel : true,
            })
            .ToList();
        RaiseChanged();
    }

    public IReadOnlyList<string> SelectedRepoNames() =>
        Repos.Where(r => r.IsSelected).Select(r => r.Name).ToList();

    public void SelectAll(bool selected)
    {
        foreach (var r in Repos)
            r.IsSelected = selected;
        RaiseChanged();
    }

    public async Task CollectCoverageAsync(
        IProgress<WorkRun>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRoot();
        var selected = ResolveSelectedRepos();
        if (selected.Count == 0)
            throw new InvalidOperationException("Select at least one repo.");

        using var linked = LinkToken(cancellationToken);
        _active = true;
        RaiseChanged();
        try
        {
            Directory.CreateDirectory(OutputDir);
            var failBelow = FailBelow;
            if (PlatformMode && failBelow == 0)
                failBelow = 95;

            IProgress<WorkRun> bridge = new Progress<WorkRun>(run =>
            {
                ActiveRun = run;
                progress?.Report(run);
                RunChanged?.Invoke();
            });

            var (run, result) = await _runner.CollectCoverageAsync(
                selected,
                Root,
                OutputDir,
                PlatformMode,
                SkipBuild,
                RegenerateSlnx,
                configuration: "Debug",
                failBelow,
                ThrottleLimit,
                HostTimeoutSeconds,
                bridge,
                linked.Token).ConfigureAwait(false);

            ActiveRun = run;
            LastCollect = result;
            HtmlIndexPath = result.HtmlIndexPath ?? run.HtmlIndexPath;
            TryLoadMergedCobertura();
        }
        finally
        {
            _active = false;
            ClearRunToken();
            RaiseChanged();
            RunChanged?.Invoke();
        }
    }

    public async Task RunTestsAsync(
        IProgress<WorkRun>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRoot();
        var selected = ResolveSelectedRepos();
        if (selected.Count == 0)
            throw new InvalidOperationException("Select at least one repo.");

        using var linked = LinkToken(cancellationToken);
        _active = true;
        RaiseChanged();
        try
        {
            IProgress<WorkRun> bridge = new Progress<WorkRun>(run =>
            {
                ActiveRun = run;
                progress?.Report(run);
                RunChanged?.Invoke();
            });

            ActiveRun = await _runner.RunTestsAsync(
                selected,
                projectReferences: PlatformMode,
                SkipBuild,
                configuration: "Debug",
                ThrottleLimit,
                HostTimeoutSeconds,
                bridge,
                linked.Token).ConfigureAwait(false);

            // Reflect host outcomes into Coverage-style repo rows for the Results tab.
            LastCollect = new CoverageCollectResult
            {
                OutputDir = OutputDir,
                HtmlIndexPath = HtmlIndexPath,
                SummaryMarkdownPath = Path.Combine(OutputDir, "SUMMARY.md"),
                DurationSeconds = ActiveRun.ElapsedSeconds,
                Repos = ActiveRun.Hosts
                    .GroupBy(h => h.Repo, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var hosts = g.ToList();
                        var failed = hosts.Any(h => h.Phase is HostPhase.Failed);
                        return new CoverageRepoResult
                        {
                            Repo = g.Key,
                            Status = failed ? "fail" : "ok",
                            Error = hosts.FirstOrDefault(h => h.Phase is HostPhase.Failed)?.Error,
                            Seconds = Math.Round(hosts.Sum(h => h.Seconds), 1),
                            TestsTotal = hosts.Sum(h => h.TestsTotal),
                            TestsPassed = hosts.Sum(h => h.TestsPassed),
                            TestsFailed = hosts.Sum(h => h.TestsFailed),
                        };
                    })
                    .OrderBy(r => r.Repo, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
        }
        finally
        {
            _active = false;
            ClearRunToken();
            RaiseChanged();
            RunChanged?.Invoke();
        }
    }

    public void AnalyzeCrap()
    {
        EnsureRoot();
        LastCrap = CrapAnalyzer.AnalyzePlatform(new CrapAnalyzeOptions
        {
            Root = Root,
            CoverageDir = OutputDir,
            Threshold = CrapThreshold,
            Include = SelectedRepoNames().Count == Repos.Count ? [] : SelectedRepoNames(),
        });
        RaiseChanged();
    }

    public void LoadCobertura(string path)
    {
        LastDocument = CoberturaDocumentParser.Load(path);
        RaiseChanged();
    }

    public void SetHtmlIndexPath(string? path) => HtmlIndexPath = path;

    public void Cancel() => _runCts?.Cancel();

    public IReadOnlyList<CoverageRepoRow> RepoRows()
    {
        if (LastCollect is null)
            return [];
        return LastCollect.Repos.Select(r => new CoverageRepoRow
        {
            Repo = r.Repo,
            Status = r.Status,
            Seconds = r.Seconds,
            TestsTotal = r.TestsTotal,
            TestsPassed = r.TestsPassed,
            TestsFailed = r.TestsFailed,
            LinePercent = r.LinePercent,
            BranchPercent = r.BranchPercent,
            Error = r.Error,
        }).ToList();
    }

    public IReadOnlyList<PackageGapRow> GapRows(double target = 95, int take = 80)
    {
        if (LastDocument is null)
            return [];
        return CoverageAnalyzer.PackagesBelowTarget(LastDocument, target, take)
            .Select(p => new PackageGapRow
            {
                Package = p.Name,
                LinePercent = p.LinePercent,
                BranchPercent = p.BranchPercent,
                LineGap = p.LineGap,
                BranchGap = p.BranchGap,
            })
            .ToList();
    }

    public IReadOnlyList<ComplexityRow> ComplexityRows(bool flaggedOnly, int take = 500)
    {
        if (LastCrap is null)
            return [];
        var q = flaggedOnly ? LastCrap.Methods.Where(m => m.Flagged) : LastCrap.Methods.AsEnumerable();
        return q.Take(take).Select(m => new ComplexityRow
        {
            Crap = Math.Round(m.Score, 2),
            Complexity = m.Method.Complexity,
            LinePercent = m.Method.LinePercent,
            BranchPercent = m.Method.BranchPercent,
            Package = m.Method.PackageName,
            Method = m.Method.DisplayName,
            File = string.IsNullOrWhiteSpace(m.Method.FileName)
                ? null
                : Path.GetFileName(m.Method.FileName),
            Flagged = m.Flagged,
        }).ToList();
    }

    private IReadOnlyList<CoverageRepo> ResolveSelectedRepos()
    {
        var names = new HashSet<string>(SelectedRepoNames(), StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
            return [];

        var excludeFile = CoverageWorkspace.DefaultExcludeFile(Root);
        var excludes = CoverageWorkspace.ReadExcludes(excludeFile, []);
        IReadOnlyList<CoverageRepo> all = PlatformMode
            ? TestHostDiscovery.DiscoverFromPlatformSlnx(
                Root,
                CoverageWorkspace.ResolvePlatformSlnx(Root),
                excludes,
                names)
            : TestHostDiscovery.DiscoverRepos(Root, excludes, names);
        return all;
    }

    private void TryLoadMergedCobertura()
    {
        foreach (var c in new[]
                 {
                     Path.Combine(OutputDir, "Cobertura.xml"),
                     Path.Combine(OutputDir, "report", "Cobertura.xml"),
                 })
        {
            if (!File.Exists(c))
                continue;
            LastDocument = CoberturaDocumentParser.Load(c);
            return;
        }
    }

    private void EnsureRoot()
    {
        if (string.IsNullOrWhiteSpace(Root) || !Directory.Exists(Root))
            throw new InvalidOperationException("Set a valid Novolis workspace root.");
        if (string.IsNullOrWhiteSpace(OutputDir))
            OutputDir = Path.Combine(Root, "coverage");
    }

    private CancellationTokenSource LinkToken(CancellationToken external)
    {
        _runCts?.Dispose();
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(external);
        return _runCts;
    }

    private void ClearRunToken()
    {
        _runCts?.Dispose();
        _runCts = null;
    }

    private void RaiseChanged() => Changed?.Invoke();

    private static string TryResolveRoot()
    {
        try
        {
            return CoverageWorkspace.ResolveRoot();
        }
        catch
        {
            return @"d:\novolis";
        }
    }
}
