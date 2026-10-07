using System.IO.Abstractions;
using Novolis.Snapshots;
using Novolis.Timeline;
using Novolis.Timeline.FileSystem;
using Novolis.Timeline.Presentation;
using Novolis.Workspaces;
using Novolis.Workspaces.FileSystem;
using Novolis.Workspaces.Snapshots;
using Novolis.Workspaces.Timeline;

namespace AssetStudioLab;

/// <summary>Branchable experiment history for a visual document, via Workspaces timeline.</summary>
public sealed class VisualStudioTimeline : IAsyncDisposable
{
    private readonly IFileSystem _fs;
    private readonly string _workspaceRoot;
    private readonly PhysicalProjectWorkspace _workspace;
    private readonly PhysicalProject _project;
    private readonly FileSystemTimeline<ZipSnapshotRef> _timeline;
    private readonly WorkspaceTimeline _workspaceTimeline;
    private readonly string _documentPath;

    private VisualStudioTimeline(
        IFileSystem fs,
        string workspaceRoot,
        PhysicalProjectWorkspace workspace,
        PhysicalProject project,
        FileSystemTimeline<ZipSnapshotRef> timeline,
        WorkspaceTimeline workspaceTimeline,
        string documentPath)
    {
        _fs = fs;
        _workspaceRoot = workspaceRoot;
        _workspace = workspace;
        _project = project;
        _timeline = timeline;
        _workspaceTimeline = workspaceTimeline;
        _documentPath = documentPath;
    }

    public static async Task<VisualStudioTimeline> CreateAsync(string? workspaceRoot = null)
    {
        var fs = new FileSystem();
        var root = workspaceRoot ?? Path.Combine(Path.GetTempPath(), "asset-studio-" + Guid.NewGuid().ToString("N"));
        var service = new WorkspaceFileSystemService(fs);
        var workspace = await service.CreateAsync(root, "Asset Studio");
        var project = await service.AddProjectAsync(workspace, "Visual", ProjectKind.Generic);
        var documentPath = Path.Combine(project.Root.FullName, WorkspaceLayout.DocumentsFolder, "asset.nvisual");
        Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
        await File.WriteAllTextAsync(documentPath, string.Empty);
        var snapshotsRoot = fs.DirectoryInfo.New(Path.Combine(root, ".novolis", "snapshots"));
        var timelineRoot = fs.DirectoryInfo.New(WorkspaceLayout.TimelinePath(root));
        var snapshots = new ZipWorkspaceSnapshotStore(fs, snapshotsRoot);
        var timeline = new FileSystemTimeline<ZipSnapshotRef>(fs, timelineRoot);
        var workspaceTimeline = new WorkspaceTimeline(timeline, snapshots);
        return new VisualStudioTimeline(fs, root, workspace, project, timeline, workspaceTimeline, documentPath);
    }

    public string WorkspaceRoot => _workspaceRoot;

    public async Task<TimelineNode<ZipSnapshotRef>> SaveAsync(string source, string label)
    {
        await File.WriteAllTextAsync(_documentPath, source);
        return await _workspaceTimeline.SavePointAsync(_workspace, new SavePointRequest(label, SnapshotKinds.Manual));
    }

    public async Task<Branch> BranchAsync(string name, TimelineNodeId from) =>
        await _workspaceTimeline.BranchFromAsync(new BranchName(name), from);

    public async Task RestoreAsync(TimelineNodeId nodeId)
    {
        await _workspaceTimeline.RestorePointAsync(_workspace, nodeId);
    }

    public async Task<string> ReadSourceAsync() =>
        await File.ReadAllTextAsync(_documentPath);

    public async Task<IReadOnlyList<TimelineTreeRow>> RowsAsync()
    {
        var nodes = await _timeline.GetNodesAsync();
        var branches = await _timeline.GetBranchesAsync();
        var head = await _timeline.GetHeadAsync();
        return new TimelineTreeProjector<ZipSnapshotRef>().ToRows(nodes, branches, head);
    }

    public ValueTask DisposeAsync()
    {
        _ = _fs;
        _ = _project;
        return ValueTask.CompletedTask;
    }
}
