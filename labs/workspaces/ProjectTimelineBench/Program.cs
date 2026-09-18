using System.IO.Abstractions;
using Novolis.Snapshots;
using Novolis.Timeline;
using Novolis.Timeline.FileSystem;
using Novolis.Workspaces;
using Novolis.Workspaces.FileSystem;
using Novolis.Workspaces.Projects.Timeline;
using Novolis.Workspaces.Timeline;

var fileSystem = new FileSystem();
var root = Path.Combine(Path.GetTempPath(), "Novolis", "ProjectTimelineBench", Guid.NewGuid().ToString("N"));
var workspaces = new WorkspaceFileSystemService(fileSystem);

var workspace = await workspaces.CreateAsync(root, "Bench", CancellationToken.None);
var project = await workspaces.AddProjectAsync(
    workspace,
    "Demo",
    ProjectKind.Generic,
    "demo",
    CancellationToken.None);

var snapshotsRoot = fileSystem.DirectoryInfo.New(Path.Combine(workspace.Root.FullName, ".novolis", "snapshots"));
var timelineRoot = fileSystem.DirectoryInfo.New(WorkspaceLayout.TimelinePath(workspace.Root.FullName));
var timeline = new FileSystemTimeline<ZipSnapshotRef>(fileSystem, timelineRoot);
var projectTimeline = new ProjectTimeline(fileSystem, snapshotsRoot, timeline);

await fileSystem.File.WriteAllTextAsync(
    Path.Combine(project.Root.FullName, "readme.txt"),
    "v1",
    CancellationToken.None);

var first = await projectTimeline.SavePointAsync(
    project,
    new SavePointRequest("Initial", SnapshotKinds.Manual),
    CancellationToken.None);

await fileSystem.File.WriteAllTextAsync(
    Path.Combine(project.Root.FullName, "readme.txt"),
    "v2",
    CancellationToken.None);

var second = await projectTimeline.SavePointAsync(
    project,
    new SavePointRequest("Updated", SnapshotKinds.Manual),
    CancellationToken.None);

Console.WriteLine($"Workspace: {workspace.Root.FullName}");
Console.WriteLine($"Save points: {first.Id} -> {second.Id}");
Console.WriteLine("ProjectTimeline sample OK.");
