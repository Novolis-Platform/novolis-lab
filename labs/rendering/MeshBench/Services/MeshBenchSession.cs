using System.Collections.ObjectModel;
using System.IO.Abstractions;
using MeshBench.Models;
using Novolis.Snapshots;
using Novolis.Timeline;
using Novolis.Timeline.FileSystem;
using Novolis.Timeline.Presentation;
using Novolis.Timeline.Presentation.GitGraph;
using Novolis.Workspaces;
using Novolis.Workspaces.FileSystem;
using Novolis.Workspaces.Snapshots;
using Novolis.Workspaces.Timeline;

namespace MeshBench.Services;

internal sealed class MeshBenchSession
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly WorkspaceFileSystemService _workspaces;
    private readonly MeshSceneStore _scenes;
    private readonly TimelineTreeProjector<ZipSnapshotRef> _projector = new();
    private readonly ObservableCollection<TimelineTreeRow> _timelineRowsBinding = [];
    private ITimeline<ZipSnapshotRef>? _timeline;
    private IReadOnlyList<TimelineTreeRow> _timelineRows = [];
    private IReadOnlyList<GitGraphTimelineRow> _gitGraphRows = [];

    public MeshBenchSession(IFileSystem fileSystem, MeshSceneStore scenes)
    {
        FileSystem = fileSystem;
        _workspaces = new WorkspaceFileSystemService(fileSystem);
        _scenes = scenes;
    }

    public IFileSystem FileSystem { get; }

    public ObservableCollection<TimelineTreeRow> TimelineRowsBinding => _timelineRowsBinding;

    public string DefaultWorkspaceRoot =>
        FileSystem.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "MeshBench",
            "default-workspace");

    public PhysicalWorkspace? Workspace { get; private set; }

    public IProject? Project { get; private set; }

    public MeshSceneDocument Scene { get; private set; } = new();

    public WorkspaceTimeline? Timeline { get; private set; }

    public IReadOnlyList<TimelineTreeRow> TimelineRows => _timelineRows;

    public IReadOnlyList<GitGraphTimelineRow> GitGraphRows => _gitGraphRows;

    public int SceneRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public async ValueTask OpenOrCreateDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (FileSystem.Directory.Exists(DefaultWorkspaceRoot))
            Workspace = await _workspaces.OpenAsync(DefaultWorkspaceRoot, cancellationToken).ConfigureAwait(false);
        else
            Workspace = await _workspaces.CreateAsync(DefaultWorkspaceRoot, "MeshBench", cancellationToken).ConfigureAwait(false);

        await EnsureProjectAsync(cancellationToken).ConfigureAwait(false);
        WireTimeline();

        await Task.Run(() => ReloadScene(), cancellationToken).ConfigureAwait(false);
        IsDirty = false;
        SceneRevision = 0;
        await RefreshTimelineAsync(cancellationToken).ConfigureAwait(false);

        if (_gitGraphRows.Count == 0 && Timeline is not null && Project is not null)
        {
            var label = _scenes.HasPersistedScene(Project) ? "Recovered scene" : "Initial workspace";
            await SavePointAsync(label, cancellationToken).ConfigureAwait(false);
        }
    }

    public void MarkDirty() => IsDirty = true;

    public void BumpSceneGeometry()
    {
        SceneRevision++;
        _scenes.InvalidateCompileCache();
    }

    public async ValueTask<TimelineNode<ZipSnapshotRef>> SavePointAsync(string? label, CancellationToken cancellationToken = default)
    {
        if (Timeline is null || Workspace is null || Project is null)
            throw new InvalidOperationException("Workspace is not open.");

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _scenes.Save(Project, Scene);

            var node = await Task.Run(
                () => Timeline.SavePointAsync(
                    Workspace,
                    new SavePointRequest(label ?? "Manual save", SnapshotKinds.Manual),
                    cancellationToken).AsTask(),
                cancellationToken).ConfigureAwait(false);

            await RefreshTimelineAsync(cancellationToken).ConfigureAwait(false);
            IsDirty = false;
            return node;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async ValueTask RestoreAsync(TimelineNodeId nodeId, CancellationToken cancellationToken = default)
    {
        if (Timeline is null || Workspace is null)
            throw new InvalidOperationException("Workspace is not open.");

        await Timeline.RestorePointAsync(Workspace, nodeId, moveHead: true, cancellationToken).ConfigureAwait(false);
        Workspace = await _workspaces.OpenAsync(Workspace.Root.FullName, cancellationToken).ConfigureAwait(false);
        Project = Workspace.Projects.FirstOrDefault();
        await Task.Run(() => ReloadScene(), cancellationToken).ConfigureAwait(false);
        SceneRevision++;
        _scenes.InvalidateCompileCache();
        IsDirty = false;
        await RefreshTimelineAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask BranchAsync(TimelineNodeId from, string name, CancellationToken cancellationToken = default)
    {
        if (_timeline is null)
            throw new InvalidOperationException("Workspace is not open.");

        await _timeline.BranchAsync(new BranchName(name), from, cancellationToken).ConfigureAwait(false);
        await RefreshTimelineAsync(cancellationToken).ConfigureAwait(false);
    }

    public void ApplyScene(MeshSceneDocument document)
    {
        Scene = document;
        SceneRevision++;
        _scenes.InvalidateCompileCache();
    }

    private async ValueTask EnsureProjectAsync(CancellationToken cancellationToken)
    {
        if (Workspace is null)
            return;

        if (Workspace.Projects.Count == 0)
        {
            await _workspaces.AddProjectAsync(
                Workspace,
                "Mesh CAD",
                ProjectKind.Generic,
                "mesh-cad",
                cancellationToken).ConfigureAwait(false);
            Workspace = await _workspaces.OpenAsync(Workspace.Root.FullName, cancellationToken).ConfigureAwait(false);
        }

        Project = Workspace.Projects[0];
    }

    private void WireTimeline()
    {
        if (Workspace is null)
            return;

        var snapshotsRoot = FileSystem.DirectoryInfo.New(
            FileSystem.Path.Combine(Workspace.Root.FullName, ".novolis", "snapshots"));
        var timelineRoot = FileSystem.DirectoryInfo.New(WorkspaceLayout.TimelinePath(Workspace.Root.FullName));
        var snapshots = new ZipWorkspaceSnapshotStore(FileSystem, snapshotsRoot);
        _timeline = new FileSystemTimeline<ZipSnapshotRef>(FileSystem, timelineRoot);
        Timeline = new WorkspaceTimeline(_timeline, snapshots);
    }

    private void ReloadScene()
    {
        if (Project is null)
        {
            Scene = new MeshSceneDocument();
            return;
        }

        Scene = _scenes.Load(Project);
    }

    private async ValueTask RefreshTimelineAsync(CancellationToken cancellationToken)
    {
        if (_timeline is null)
        {
            _timelineRows = [];
            _gitGraphRows = [];
            SyncTimelineBinding();
            return;
        }

        var nodes = await _timeline.GetNodesAsync(cancellationToken).ConfigureAwait(false);
        var branches = await _timeline.GetBranchesAsync(cancellationToken).ConfigureAwait(false);
        var head = await _timeline.GetHeadAsync(cancellationToken).ConfigureAwait(false);
        var tree = _projector.ToTree(nodes, branches, head);
        _timelineRows = _projector.ToRows(nodes, branches, head);
        _gitGraphRows = GitGraphTimelineBuilder.Build(tree, nodes, branches, head);
        SyncTimelineBinding();
    }

    private void SyncTimelineBinding()
    {
        if (_timelineRowsBinding.Count == _timelineRows.Count)
        {
            var unchanged = true;
            for (var i = 0; i < _timelineRows.Count; i++)
            {
                if (_timelineRowsBinding[i].Id != _timelineRows[i].Id)
                {
                    unchanged = false;
                    break;
                }
            }

            if (unchanged)
                return;
        }

        _timelineRowsBinding.Clear();
        foreach (var row in _timelineRows)
            _timelineRowsBinding.Add(row);
    }
}
