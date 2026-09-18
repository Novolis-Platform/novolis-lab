using System.IO.Abstractions;
using Novolis.Snapshots;
using Novolis.Timeline;
using Novolis.Timeline.FileSystem;
using Novolis.Timeline.Presentation;
using Novolis.Workspaces;
using Novolis.Workspaces.FileSystem;
using Novolis.Workspaces.Snapshots;
using Novolis.Workspaces.Timeline;

var fs = new FileSystem();
var workspaceRoot = Path.Combine(Path.GetTempPath(), "novolis-minimal-" + Guid.NewGuid().ToString("N"));

var workspaceService = new WorkspaceFileSystemService(fs);
var workspace = await workspaceService.CreateAsync(workspaceRoot, "Minimal Demo");
var project = await workspaceService.AddProjectAsync(workspace, "Demo Project", ProjectKind.Generic);

var docPath = Path.Combine(project.Root.FullName, WorkspaceLayout.DocumentsFolder, "hello.txt");
Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
await File.WriteAllTextAsync(docPath, "save-point-1");

var snapshotsRoot = fs.DirectoryInfo.New(Path.Combine(workspaceRoot, ".novolis", "snapshots"));
var timelineRoot = fs.DirectoryInfo.New(WorkspaceLayout.TimelinePath(workspaceRoot));
var snapshots = new ZipWorkspaceSnapshotStore(fs, snapshotsRoot);
var timeline = new FileSystemTimeline<ZipSnapshotRef>(fs, timelineRoot);
var workspaceTimeline = new WorkspaceTimeline(timeline, snapshots);

await workspaceTimeline.SavePointAsync(workspace, new SavePointRequest("First", SnapshotKinds.Manual));
await File.WriteAllTextAsync(docPath, "save-point-2");
var second = await workspaceTimeline.SavePointAsync(workspace, new SavePointRequest("Second", SnapshotKinds.Manual));
await workspaceTimeline.BranchFromAsync(new BranchName("experiment"), second.Id);

var nodes = await timeline.GetNodesAsync();
var branches = await timeline.GetBranchesAsync();
var head = await timeline.GetHeadAsync();
var projector = new TimelineTreeProjector<ZipSnapshotRef>();
var rows = projector.ToRows(nodes, branches, head);

Console.WriteLine($"Workspace: {workspaceRoot}");
Console.WriteLine($"Save points: {nodes.Count}, branches: {branches.Count}");
foreach (var row in rows)
    Console.WriteLine($"  [{row.Branch}] depth={row.Depth} {row.Label} head={row.IsHead}");
