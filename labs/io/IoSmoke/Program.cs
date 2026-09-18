using Novolis.IO.Git;
using Novolis.IO.Paths;
using Novolis.IO.Processes;
using Novolis.IO.Recovery;
using Novolis.IO.Watching;

var start = AppContext.BaseDirectory;
if (!RootFinder.TryFind(start, ["nuget.config", "Directory.Packages.props"], out var dogfoodRoot))
{
    Console.Error.WriteLine("RootFinder: could not locate lab repo markers from " + start);
    return 1;
}

Console.WriteLine($"Paths: repo root = {dogfoodRoot}");

var recoveryDir = Path.Combine(Path.GetTempPath(), "novolis-io-smoke-recovery");
Directory.CreateDirectory(recoveryDir);
var recovery = new ContentRecoveryStore(recoveryDir, maxSnapshotsPerDocument: 3);
const string docKey = "chapter-1";
recovery.WriteSnapshot(docKey, "draft one");
recovery.WriteSnapshot(docKey, "draft two — recovered");
var latest = recovery.GetLatest(docKey);
if (latest is null || !latest.Content.Contains("recovered", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Recovery: round-trip failed");
    return 1;
}

Console.WriteLine($"Recovery: {latest.DocumentKey} @ {latest.TimestampUtc:u} ({latest.Content.Length} chars)");

var watchFile = Path.Combine(Path.GetTempPath(), "novolis-io-smoke-watch.txt");
File.WriteAllText(watchFile, "v1");
using var watcher = new DebouncedFileWatcher(debounceMilliseconds: 80);
var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
watcher.FileChanged += path => changed.TrySetResult(path);
watcher.Watch(watchFile);
File.WriteAllText(watchFile, "v2");
var completed = await Task.WhenAny(changed.Task, Task.Delay(3000));
if (completed != changed.Task)
{
    Console.Error.WriteLine("Watching: timed out waiting for FileChanged");
    return 1;
}

Console.WriteLine($"Watching: change observed on {Path.GetFileName(await changed.Task)}");

var queue = new ProcessJobQueue { MaxParallel = 1 };
var job = queue.Enqueue(new ProcessJobSpec
{
    FileName = "dotnet",
    Arguments = ["--version"],
    Title = "dotnet --version",
    WorkingDirectory = dogfoodRoot
});

var deadline = DateTime.UtcNow.AddSeconds(30);
while (job.Status is ProcessJobStatus.Queued or ProcessJobStatus.Running
       && DateTime.UtcNow < deadline)
    await Task.Delay(50);

Console.WriteLine($"Processes: {job.Title} → {job.Status} (exit={job.ExitCode}) {job.Detail}");
if (job.Status != ProcessJobStatus.Succeeded)
{
    Console.Error.WriteLine("Processes: expected Succeeded");
    return 1;
}

try
{
    var git = new GitRepositoryService();
    var status = git.GetStatus(dogfoodRoot);
    Console.WriteLine(
        $"Git: branch={status.Branch} dirty={status.Dirty} ahead={status.Ahead} behind={status.Behind} last={status.LastCommitSha} {status.LastCommitMessage}");
}
catch (Exception ex)
{
    Console.WriteLine($"Git: skipped ({ex.Message})");
}

Console.WriteLine("IoSmoke OK");
return 0;
