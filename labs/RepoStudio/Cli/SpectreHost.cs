using System.Text.Json;
using Novolis.IO.Git;
using Spectre.Console;

namespace RepoStudio.Cli;

internal static class SpectreHost
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<int> RunAsync(CliOptions options)
    {
        var git = new GitRepositoryService();
        var cmd = options.Args.FirstOrDefault()?.ToLowerInvariant() ?? "status";
        var root = SafeRoot(options.Root);

        try
        {
            switch (cmd)
            {
                case "root":
                    Write(options, new { root });
                    return 0;
                case "list":
                {
                    var repos = GitWorkspace.Discover(root);
                    Write(options, repos);
                    return 0;
                }
                case "status":
                {
                    var filter = ParseFilter(options.Args.Skip(1).ToArray());
                    var matrix = GitWorkspace.GetStatusMatrix(root, git, filter, includeStashCount: false);
                    Write(options, matrix);
                    return matrix.Summary.Behind > 0 && matrix.Summary.Dirty > 0 ? 1 : 0;
                }
                case "fetch":
                {
                    var repos = SelectRepos(root, options.Args.Skip(1).ToArray());
                    var batch = new GitWorkspaceBatch(git);
                    var result = await batch.FetchAsync(repos, new BatchOptions
                    {
                        WorkspaceRoot = root,
                        Parallel = ParseParallel(options.Args),
                    });
                    Write(options, result);
                    return result.HasFailures ? 1 : 0;
                }
                case "pull":
                {
                    var repos = SelectRepos(root, options.Args.Skip(1).ToArray());
                    var batch = new GitWorkspaceBatch(git);
                    var result = await batch.PullFfOnlyAsync(repos, new BatchOptions
                    {
                        WorkspaceRoot = root,
                        SkipDirty = true,
                        Parallel = ParseParallel(options.Args),
                    });
                    Write(options, result);
                    return result.HasFailures ? 1 : 0;
                }
                case "branch":
                    return await BranchCommandAsync(git, root, options);
                case "log":
                {
                    var repo = ResolveSingleRepo(root, options.Args.Skip(1).ToArray());
                    var graph = git.GetCommitGraph(repo);
                    Write(options, graph);
                    return 0;
                }
                case "stash":
                    return StashCommand(git, root, options);
                case "diff":
                case "show":
                {
                    var repo = ResolveSingleRepo(root, options.Args.Skip(1).ToArray());
                    var sha = options.Args.Skip(1).FirstOrDefault(a => !a.StartsWith('-') && !a.Contains('\\') && a.Length >= 7);
                    var doc = git.GetDiff(repo, cmd == "show" ? sha : null);
                    Write(options, doc);
                    return 0;
                }
                default:
                    AnsiConsole.MarkupLine("[red]Unknown command[/]. Use: root|list|status|fetch|pull|branch|log|stash|diff|show");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            if (options.Json)
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message }, JsonOpts));
            else
                AnsiConsole.WriteException(ex);
            return 2;
        }
    }

    static async Task<int> BranchCommandAsync(GitRepositoryService git, string root, CliOptions options)
    {
        var sub = options.Args.Skip(1).FirstOrDefault()?.ToLowerInvariant() ?? "plan";
        var name = GetFlag(options.Args, "--name") ?? "feat/unnamed";
        var baseRef = GetFlag(options.Args, "--base") ?? "main";
        var dryRun = options.Args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        var repos = SelectRepos(root, options.Args);
        var planner = new BranchCutPlanner(git);

        if (sub is "plan" or "apply")
        {
            var plan = planner.Plan(root, name, repos, baseRef);
            if (sub == "plan" || dryRun)
            {
                var applied = await planner.ApplyAsync(plan, dryRun: true);
                Write(options, applied);
                return plan.Steps.Any(s => s.BlockReason is not null) ? 2 : 0;
            }

            var result = await planner.ApplyAsync(plan, dryRun: false);
            Write(options, result);
            return result.Ok ? 0 : 1;
        }

        if (sub == "status")
        {
            var id = GetFlag(options.Args, "--plan");
            var plan = id is null ? null : planner.GetPlan(id);
            Write(options, plan is null ? new { ok = false, error = "plan not found" } : plan);
            return plan is null ? 2 : 0;
        }

        AnsiConsole.MarkupLine("[red]branch[/] subcommands: plan|apply|status");
        return 2;
    }

    static int StashCommand(GitRepositoryService git, string root, CliOptions options)
    {
        var sub = options.Args.Skip(1).FirstOrDefault()?.ToLowerInvariant() ?? "list";
        var repo = ResolveSingleRepo(root, options.Args.Skip(1).ToArray());
        return sub switch
        {
            "list" => WriteAndExit(options, git.ListStashes(repo)),
            "push" => WriteOp(options, git.StashPush(repo, GetFlag(options.Args, "--message"))),
            "pop" => WriteOp(options, git.StashPop(repo, ParseIndex(options.Args))),
            "apply" => WriteOp(options, git.StashApply(repo, ParseIndex(options.Args))),
            "drop" => WriteOp(options, git.StashDrop(repo, ParseIndex(options.Args))),
            _ => 2,
        };
    }

    static int WriteAndExit(CliOptions options, object payload)
    {
        Write(options, payload);
        return 0;
    }

    static int WriteOp(CliOptions options, GitOperationResult r)
    {
        Write(options, r);
        return r.Ok ? 0 : 1;
    }

    static void Write(CliOptions options, object payload)
    {
        if (options.Json)
            Console.WriteLine(JsonSerializer.Serialize(payload, payload.GetType(), JsonOpts));
        else
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, payload.GetType(), JsonOpts));
    }

    static string SafeRoot(string? root) => GitWorkspace.ResolveRoot(root);

    static IReadOnlyList<RepoEntry> SelectRepos(string root, string[] args)
    {
        var filter = ParseFilter(args);
        return GitWorkspace.SelectByNames(GitWorkspace.Discover(root), filter);
    }

    static RepoFilter ParseFilter(string[] args)
    {
        var include = GetFlag(args, "--repos") ?? GetFlag(args, "--include");
        var onBranch = args.Contains("--on", StringComparer.OrdinalIgnoreCase)
            ? args.SkipWhile(a => !a.Equals("--on", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault()
            : null;
        return new RepoFilter
        {
            Include = include?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Behind = args.Any(a => a.Equals("--behind", StringComparison.OrdinalIgnoreCase)) ? true : null,
            Ahead = args.Any(a => a.Equals("--ahead", StringComparison.OrdinalIgnoreCase)) ? true : null,
            Dirty = args.Any(a => a.Equals("--dirty", StringComparison.OrdinalIgnoreCase)) ? true : null,
            OnBranch = onBranch,
        };
    }

    static string ResolveSingleRepo(string root, string[] args)
    {
        var name = GetFlag(args, "--repo")
                   ?? args.FirstOrDefault(a => a.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase));
        if (name is null)
            throw new InvalidOperationException("Pass --repo <name>.");
        var entry = GitWorkspace.Discover(root)
            .FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                                 || r.Name.Equals("novolis-" + name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new InvalidOperationException($"Repo not found: {name}");
        return entry.Path;
    }

    static string? GetFlag(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..];
        }

        return null;
    }

    static int ParseParallel(string[] args)
    {
        var p = GetFlag(args, "--parallel");
        return int.TryParse(p, out var n) ? Math.Clamp(n, 1, 32) : 6;
    }

    static int ParseIndex(string[] args)
    {
        var i = GetFlag(args, "--index");
        return int.TryParse(i, out var n) ? n : 0;
    }
}
