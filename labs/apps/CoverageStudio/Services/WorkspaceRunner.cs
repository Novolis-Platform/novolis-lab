using System.Diagnostics;
using CoverageStudio.Models;
using Novolis.Tools.Coverage;

namespace CoverageStudio.Services;

/// <summary>Typed, windowless orchestration of test / coverage hosts with live progress.</summary>
internal sealed class WorkspaceRunner
{
    public async Task<WorkRun> RunTestsAsync(
        IReadOnlyList<CoverageRepo> repos,
        bool projectReferences,
        bool skipBuild,
        string configuration,
        int throttleLimit,
        int hostTimeoutSeconds,
        IProgress<WorkRun>? progress,
        CancellationToken cancellationToken)
    {
        var run = CreateRun(WorkKind.Tests, "Test run", repos);
        return await ExecuteAsync(
            run,
            repos,
            projectReferences,
            skipBuild,
            configuration,
            throttleLimit,
            hostTimeoutSeconds,
            collectCoverage: false,
            outputDir: null,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<(WorkRun Run, CoverageCollectResult Result)> CollectCoverageAsync(
        IReadOnlyList<CoverageRepo> repos,
        string root,
        string outputDir,
        bool platformMode,
        bool skipBuild,
        bool regenerateSlnx,
        string configuration,
        double failBelow,
        int throttleLimit,
        int hostTimeoutSeconds,
        IProgress<WorkRun>? progress,
        CancellationToken cancellationToken)
    {
        if (platformMode && regenerateSlnx)
            await RegeneratePlatformSlnxAsync(root, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(outputDir);
        var rawDir = Path.Combine(outputDir, "raw");
        var reportDir = Path.Combine(outputDir, "report");
        var logsDir = Path.Combine(outputDir, "logs");
        Directory.CreateDirectory(rawDir);
        Directory.CreateDirectory(reportDir);
        Directory.CreateDirectory(logsDir);

        foreach (var old in Directory.EnumerateFiles(rawDir, "*.cobertura.xml", SearchOption.AllDirectories))
            File.Delete(old);

        var run = CreateRun(WorkKind.Coverage, "Coverage collect", repos);
        run = await ExecuteAsync(
            run,
            repos,
            projectReferences: platformMode,
            skipBuild,
            configuration,
            throttleLimit,
            hostTimeoutSeconds,
            collectCoverage: true,
            outputDir,
            progress,
            cancellationToken).ConfigureAwait(false);

        var repoResults = AggregateRepoResults(run);
        var allCobertura = new List<string>();
        foreach (var r in repoResults.Where(r => r.Status == "ok" && r.CoberturaFiles.Count > 0))
        {
            allCobertura.AddRange(r.CoberturaFiles);
            var repoReport = Path.Combine(reportDir, r.Repo);
            Directory.CreateDirectory(repoReport);
            ReportGeneratorInvoker.Generate(
                r.CoberturaFiles,
                repoReport,
                r.Repo,
                reportTypes: "Cobertura;TextSummary",
                assemblyFilters: CoverageWorkspace.RepoAssemblyFilter(r.Repo),
                log: TextWriter.Null);
        }

        var persisted = Directory.Exists(reportDir)
            ? Directory.EnumerateDirectories(reportDir)
                .Where(d => Path.GetFileName(d).StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
                .Select(d => Path.Combine(d, "Cobertura.xml"))
                .Where(File.Exists)
                .ToList()
            : [];
        if (persisted.Count > 0)
            allCobertura = persisted;

        for (var i = 0; i < repoResults.Count; i++)
        {
            var r = repoResults[i];
            if (r.Status != "ok" || r.CoberturaFiles.Count == 0)
                continue;
            var merged = Path.Combine(reportDir, r.Repo, "Cobertura.xml");
            var path = File.Exists(merged) ? merged : r.CoberturaFiles[0];
            try
            {
                var sum = CoberturaSummaryParser.Parse(path);
                repoResults[i] = new CoverageRepoResult
                {
                    Repo = r.Repo,
                    Status = r.Status,
                    Error = r.Error,
                    Seconds = r.Seconds,
                    CoberturaFiles = r.CoberturaFiles,
                    TestsTotal = r.TestsTotal,
                    TestsPassed = r.TestsPassed,
                    TestsFailed = r.TestsFailed,
                    LinePercent = sum.LinePercent,
                    BranchPercent = sum.BranchPercent,
                    LinesCovered = sum.LinesCovered,
                    LinesValid = sum.LinesValid,
                };
            }
            catch
            {
                // keep without percents
            }
        }

        ApplyRepoPercentsToHosts(run, repoResults);

        double? aggLine = null;
        double? aggBranch = null;
        string? htmlIndex = null;
        if (allCobertura.Count > 0)
        {
            run.Detail = "Merging Cobertura…";
            progress?.Report(run);
            var historyDir = Path.Combine(outputDir, "history");
            var exit = ReportGeneratorInvoker.Generate(
                allCobertura,
                reportDir,
                "Novolis coverage",
                historyDir: historyDir,
                log: TextWriter.Null);
            if (exit != 0)
                throw new InvalidOperationException($"reportgenerator failed (exit {exit})");

            var aggCob = Path.Combine(reportDir, "Cobertura.xml");
            if (File.Exists(aggCob))
            {
                var agg = CoberturaSummaryParser.Parse(aggCob);
                aggLine = agg.LinePercent;
                aggBranch = agg.BranchPercent;
            }

            htmlIndex = Path.Combine(reportDir, "index.html");
            if (!File.Exists(htmlIndex))
                htmlIndex = null;

            var summaryHtml = Path.Combine(reportDir, "summary.html");
            if (File.Exists(summaryHtml))
                File.Copy(summaryHtml, Path.Combine(root, "COVERAGE.html"), overwrite: true);

            // Flatten top-level report assets into outputDir
            foreach (var entry in Directory.EnumerateFileSystemEntries(reportDir))
            {
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry) && name.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dest = Path.Combine(outputDir, name);
                if (Directory.Exists(dest))
                    Directory.Delete(dest, recursive: true);
                if (File.Exists(dest))
                    File.Delete(dest);
                if (Directory.Exists(entry))
                    Directory.Move(entry, dest);
                else
                    File.Move(entry, dest, overwrite: true);
            }

            var flat = Path.Combine(outputDir, "index.html");
            if (File.Exists(flat))
                htmlIndex = flat;
        }

        var mdPath = Path.Combine(outputDir, "SUMMARY.md");
        await File.WriteAllTextAsync(mdPath, "# Coverage Studio\n", cancellationToken).ConfigureAwait(false);

        var gateFailed = false;
        string? gateMessage = null;
        if (failBelow > 0 && aggLine is not null)
        {
            var summary = new CoberturaSummary
            {
                LinePercent = aggLine.Value,
                BranchPercent = aggBranch ?? 100,
            };
            (gateFailed, gateMessage) = CoverageGate.Evaluate(summary, failBelow);
        }

        run.HtmlIndexPath = htmlIndex;
        run.Phase = cancellationToken.IsCancellationRequested
            ? WorkPhase.Cancelled
            : run.Failed > 0 ? WorkPhase.Succeeded : WorkPhase.Succeeded;
        if (aggLine is not null)
            run.Detail = $"line {aggLine:0.0}% · branch {aggBranch:0.0}% · {run.CountsLabel}";
        progress?.Report(run);

        var result = new CoverageCollectResult
        {
            OutputDir = outputDir,
            HtmlIndexPath = htmlIndex,
            SummaryMarkdownPath = mdPath,
            AggregateLinePercent = aggLine,
            AggregateBranchPercent = aggBranch,
            DurationSeconds = run.ElapsedSeconds,
            Repos = repoResults,
            GateFailed = gateFailed,
            GateMessage = gateMessage,
        };
        return (run, result);
    }

    private static WorkRun CreateRun(WorkKind kind, string title, IReadOnlyList<CoverageRepo> repos)
    {
        var run = new WorkRun
        {
            Kind = kind,
            Title = title,
            Phase = WorkPhase.Queued,
        };
        foreach (var repo in repos)
        {
            foreach (var proj in repo.TestProjects)
            {
                run.Hosts.Add(new WorkHostItem
                {
                    Id = $"{repo.Name}:{proj}",
                    Repo = repo.Name,
                    HostName = Path.GetFileNameWithoutExtension(proj),
                    ProjectPath = proj,
                    WorkingDirectory = repo.Path,
                });
            }
        }

        run.Recalculate();
        return run;
    }

    private static async Task<WorkRun> ExecuteAsync(
        WorkRun run,
        IReadOnlyList<CoverageRepo> repos,
        bool projectReferences,
        bool skipBuild,
        string configuration,
        int throttleLimit,
        int hostTimeoutSeconds,
        bool collectCoverage,
        string? outputDir,
        IProgress<WorkRun>? progress,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        run.Phase = WorkPhase.Running;
        run.Title = collectCoverage ? "Coverage collect" : "Test run";
        progress?.Report(run);

        var throttle = throttleLimit > 0
            ? throttleLimit
            : Math.Clamp(Environment.ProcessorCount - 1, 1, 4);
        var hostTimeout = hostTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(hostTimeoutSeconds)
            : Timeout.InfiniteTimeSpan;
        var projectRef = projectReferences ? "true" : "false";

        // Agent LocalIpc/HTTP hosts deadlock / thrash under massively parallel batches
        // (same policy as Novolis.Tools.Coverage.CoverageCollector).
        var parallelHosts = run.Hosts.Where(h => !IsSerialRepo(h.Repo)).ToList();
        var serialHosts = run.Hosts.Where(h => IsSerialRepo(h.Repo)).ToList();

        async Task RunOneAsync(WorkHostItem host)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await RunHostAsync(
                    host,
                    skipBuild,
                    configuration,
                    projectRef,
                    collectCoverage,
                    outputDir,
                    hostTimeout,
                    () =>
                    {
                        run.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
                        run.Recalculate();
                        progress?.Report(run);
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                host.Phase = HostPhase.Cancelled;
                host.Progress = 1;
            }
            finally
            {
                run.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
                run.Recalculate();
                progress?.Report(run);
            }
        }

        try
        {
            if (parallelHosts.Count > 0)
            {
                using var gate = new SemaphoreSlim(throttle, throttle);
                var tasks = parallelHosts.Select(async host =>
                {
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await RunOneAsync(host).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                });
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            foreach (var host in serialHosts)
                await RunOneAsync(host).ConfigureAwait(false);

            run.Phase = run.Failed > 0 && run.Completed == run.Failed
                ? WorkPhase.Failed
                : WorkPhase.Succeeded;
        }
        catch (OperationCanceledException)
        {
            run.Phase = WorkPhase.Cancelled;
        }

        run.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
        run.Recalculate();
        if (serialHosts.Count > 0)
            run.Detail = $"{run.CountsLabel} · serial: {string.Join(", ", serialHosts.Select(h => h.Repo).Distinct())}";
        progress?.Report(run);
        _ = repos;
        return run;
    }

    private static bool IsSerialRepo(string repoName) =>
        repoName.Equals("novolis-agent", StringComparison.OrdinalIgnoreCase);

    private static async Task RunHostAsync(
        WorkHostItem host,
        bool skipBuild,
        string configuration,
        string projectRef,
        bool collectCoverage,
        string? outputDir,
        TimeSpan hostTimeout,
        Action notify,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        host.Phase = HostPhase.Queued;
        host.Progress = 0.02;
        host.LiveElapsedSeconds = 0;
        host.LiveTimeoutSeconds = hostTimeout > TimeSpan.Zero && hostTimeout != Timeout.InfiniteTimeSpan
            ? hostTimeout.TotalSeconds
            : null;
        notify();

        void Tick(TimeSpan elapsed, TimeSpan? limit, double baseProgress, double span)
        {
            host.LiveElapsedSeconds = Math.Round(elapsed.TotalSeconds, 0);
            host.LiveTimeoutSeconds = limit?.TotalSeconds;
            if (limit is { } t && t > TimeSpan.Zero)
                host.Progress = baseProgress + span * Math.Clamp(elapsed.TotalSeconds / t.TotalSeconds, 0, 0.99);
            else
                host.Progress = baseProgress + Math.Min(span * 0.5, span * (elapsed.TotalSeconds / 120.0));
            notify();
        }

        try
        {
            if (!skipBuild)
            {
                host.Phase = HostPhase.Building;
                host.Progress = 0.05;
                notify();
                var build = await DotnetProcessRunner.RunAsync(
                    ["build", host.ProjectPath, "-c", configuration, "--nologo", $"-p:NovolisUseProjectReferences={projectRef}"],
                    host.WorkingDirectory,
                    hostTimeout,
                    cancellationToken,
                    (elapsed, limit) => Tick(elapsed, limit, 0.05, 0.25)).ConfigureAwait(false);
                host.ExitCode = build.ExitCode;
                if (build.TimedOut)
                    throw new TimeoutException($"build timed out after {hostTimeout.TotalSeconds:0}s");
                if (build.ExitCode != 0)
                    throw new InvalidOperationException($"build exit {build.ExitCode}");
            }

            host.Phase = HostPhase.Testing;
            host.Progress = 0.35;
            host.LiveElapsedSeconds = 0;
            notify();

            var args = new List<string>
            {
                "test",
                "--project", host.ProjectPath,
                "-c", configuration,
                $"-p:NovolisUseProjectReferences={projectRef}",
            };
            if (skipBuild)
                args.Add("--no-build");

            string? coberturaOut = null;
            if (collectCoverage)
            {
                ArgumentNullException.ThrowIfNull(outputDir);
                var repoRaw = Path.Combine(outputDir, "raw", host.Repo);
                Directory.CreateDirectory(repoRaw);
                coberturaOut = Path.Combine(repoRaw, $"{host.HostName}.cobertura.xml");
                args.Add("--");
                args.Add("--coverage");
                args.Add("--coverage-output-format");
                args.Add("cobertura");
                args.Add("--coverage-output");
                args.Add(coberturaOut);
            }

            var test = await DotnetProcessRunner.RunAsync(
                    args,
                    host.WorkingDirectory,
                    hostTimeout,
                    cancellationToken,
                    (elapsed, limit) => Tick(elapsed, limit, 0.35, 0.5))
                .ConfigureAwait(false);
            host.ExitCode = test.ExitCode;
            var counts = DotnetProcessRunner.ParseTestCounts(test.Output);
            host.TestsTotal = counts.Total;
            host.TestsPassed = counts.Passed;
            host.TestsFailed = counts.Failed;
            host.Progress = 0.85;
            notify();

            if (test.TimedOut)
                throw new TimeoutException($"timed out after {hostTimeout.TotalSeconds:0}s");
            if (test.ExitCode != 0)
                throw new InvalidOperationException($"test exit {test.ExitCode}");

            if (collectCoverage && coberturaOut is not null)
            {
                host.Phase = HostPhase.Parsing;
                host.Progress = 0.92;
                notify();
                var path = ResolveCobertura(coberturaOut);
                if (path is null)
                    throw new InvalidOperationException("coverage file missing");

                try
                {
                    var sum = CoberturaSummaryParser.Parse(path);
                    host.LinePercent = sum.LinePercent;
                    host.BranchPercent = sum.BranchPercent;
                }
                catch
                {
                    // percent optional
                }

                host.CoberturaPath = path;
            }

            host.Phase = HostPhase.Succeeded;
            host.Progress = 1;
            host.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            host.LiveElapsedSeconds = 0;
            notify();
        }
        catch (OperationCanceledException)
        {
            host.Phase = HostPhase.Cancelled;
            host.Progress = 1;
            host.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            notify();
            throw;
        }
        catch (Exception ex)
        {
            host.Phase = HostPhase.Failed;
            host.Error = ex.Message;
            host.Progress = 1;
            host.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            notify();
        }
    }

    private static string? ResolveCobertura(string outFile)
    {
        if (File.Exists(outFile))
            return outFile;
        var alt = outFile.Replace(".cobertura.xml", "", StringComparison.Ordinal);
        if (File.Exists(alt))
        {
            File.Move(alt, outFile, overwrite: true);
            return outFile;
        }

        return null;
    }

    private static List<CoverageRepoResult> AggregateRepoResults(WorkRun run)
    {
        return run.Hosts
            .GroupBy(h => h.Repo, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var hosts = g.ToList();
                var failed = hosts.Where(h => h.Phase is HostPhase.Failed).ToList();
                var cob = hosts
                    .Select(h => h.CoberturaPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p!))
                    .Cast<string>()
                    .ToList();
                return new CoverageRepoResult
                {
                    Repo = g.Key,
                    Status = failed.Count > 0 ? "fail" : "ok",
                    Error = failed.FirstOrDefault()?.Error,
                    Seconds = Math.Round(hosts.Sum(h => h.Seconds), 1),
                    CoberturaFiles = cob,
                    TestsTotal = hosts.Sum(h => h.TestsTotal),
                    TestsPassed = hosts.Sum(h => h.TestsPassed),
                    TestsFailed = hosts.Sum(h => h.TestsFailed),
                    LinePercent = hosts.Select(h => h.LinePercent).FirstOrDefault(p => p is not null),
                    BranchPercent = hosts.Select(h => h.BranchPercent).FirstOrDefault(p => p is not null),
                };
            })
            .ToList();
    }

    private static void ApplyRepoPercentsToHosts(WorkRun run, IReadOnlyList<CoverageRepoResult> repos)
    {
        var map = repos.ToDictionary(r => r.Repo, StringComparer.OrdinalIgnoreCase);
        foreach (var host in run.Hosts)
        {
            if (!map.TryGetValue(host.Repo, out var repo))
                continue;
            if (repo.LinePercent is { } line)
                host.LinePercent = line;
            if (repo.BranchPercent is { } branch)
                host.BranchPercent = branch;
        }
    }

    private static async Task RegeneratePlatformSlnxAsync(string root, CancellationToken ct)
    {
        var script = Path.Combine(root, "novolis-governance", "build", "Generate-Platform-Slnx.ps1");
        if (!File.Exists(script))
            throw new FileNotFoundException("Generate-Platform-Slnx.ps1 not found.", script);

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("-WorkspaceRoot");
        psi.ArgumentList.Add(root);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start pwsh");
        _ = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        _ = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Generate-Platform-Slnx.ps1 failed (exit {p.ExitCode})");
    }
}
