using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CalypsoCad.Export;
using CalypsoCad.Model;
using CalypsoCad.Session;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Explorer;

/// <summary>
/// Hierarchical object browser driven by <see cref="ShipPartRegistry"/>.
/// Visibility / isolate are viewport filters — they do not mutate ownership.
/// </summary>
internal sealed class CalypsoObjectBrowser : UserControl
{
    private readonly CalypsoViewportController _controller;
    private readonly TreeView _tree;
    private readonly TextBox _search;
    private readonly TextBlock _inspector;
    private readonly Dictionary<string, TreeViewItem> _itemsById = new(StringComparer.OrdinalIgnoreCase);
    private string _filter = "";

    public CalypsoObjectBrowser(CalypsoViewportController controller)
    {
        _controller = controller;

        _search = new TextBox
        {
            PlaceholderText = "Find object…",
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4),
        };
        _search.TextChanged += (_, _) =>
        {
            _filter = _search.Text?.Trim() ?? "";
            RebuildTree();
        };

        _tree = new TreeView
        {
            FontSize = 11,
            MaxHeight = 320,
        };
        _tree.SelectionChanged += OnTreeSelectionChanged;

        _inspector = new TextBlock
        {
            FontFamily = GraphicalProfile.MonoFont,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        GraphicalProfileBinding.Bind(_inspector, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                ActionBtn("Hide", HideSelected),
                ActionBtn("Show", ShowSelected),
                ActionBtn("Isolate", IsolateSelected),
                ActionBtn("Clear", ClearIsolateAndHide),
                ActionBtn("Focus", FocusSelected),
            },
        };

        var isolates = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        isolates.Children.Add(ActionBtn("Frames", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectRingFrames)));
        isolates.Children.Add(ActionBtn("Bulkheads", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectBulkheads)));
        isolates.Children.Add(ActionBtn("Knuckles", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectKnuckles)));
        isolates.Children.Add(ActionBtn("Primary L", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectPrimaryLongitudinal)));
        isolates.Children.Add(ActionBtn("Deck 0", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectDeck0Supports)));
        isolates.Children.Add(ActionBtn("Cargo fl", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectCargoFloorSupports)));
        isolates.Children.Add(ActionBtn("All long.", () => IsolateCategory(HeadlessStructureInspectionExporter.CollectAllLongitudinalLoadBearing)));
        isolates.Children.Add(ActionBtn("Int.supp", () => IsolateCategory(HeadlessFrameInteriorInspectionExporter.CollectInteriorSupports)));

        var root = new StackPanel { Spacing = 2 };
        root.Children.Add(Header("Object browser"));
        root.Children.Add(_search);
        root.Children.Add(_tree);
        root.Children.Add(actions);
        root.Children.Add(Hint("Isolate (registry ownership). All long. = virtual filter."));
        root.Children.Add(isolates);
        root.Children.Add(Header("Inspector"));
        root.Children.Add(_inspector);
        Content = root;

        RebuildTree();
    }

    public void RebuildTree()
    {
        var registry = _controller.Session.Registry;
        if (registry.Assemblies.Count == 0 && _controller.Session.Document.Entities.Count > 0)
            _controller.Session.RebuildSemanticRegistry();

        var tree = ShipHierarchyBuilder.BuildExplorerTree(registry);
        _itemsById.Clear();
        _tree.Items.Clear();
        var rootItem = BuildItem(tree);
        if (rootItem is not null)
            _tree.Items.Add(rootItem);
    }

    private TreeViewItem? BuildItem(ShipExplorerNode node)
    {
        if (!PassesFilter(node))
            return null;

        var item = new TreeViewItem
        {
            Header = node.Title,
            Tag = node,
            IsExpanded = node.Kind is ShipExplorerNodeKind.Root or ShipExplorerNodeKind.Assembly
                         && (node.AssemblyId?.Value.StartsWith("PHYS-", StringComparison.OrdinalIgnoreCase) == true
                             || node.Kind == ShipExplorerNodeKind.Root
                             || string.Equals(node.Id, ShipHierarchyIds.KnuckleStringers, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(node.Id, ShipHierarchyIds.PhysicalStructure, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(node.Id, ShipHierarchyIds.Hull, StringComparison.OrdinalIgnoreCase)),
        };
        GraphicalProfileBinding.Bind(item, ForegroundProperty, GraphicalProfile.TextResourceKey);
        _itemsById[node.Id] = item;

        foreach (var child in node.Children)
        {
            var childItem = BuildItem(child);
            if (childItem is not null)
                item.Items.Add(childItem);
        }

        // Keep empty organizational nodes that match filter ancestors.
        if (item.Items.Count == 0 && node.Children.Count > 0 && !string.IsNullOrEmpty(_filter))
            return null;

        return item;
    }

    private bool PassesFilter(ShipExplorerNode node)
    {
        if (string.IsNullOrEmpty(_filter))
            return true;
        if (node.Id.Contains(_filter, StringComparison.OrdinalIgnoreCase)
            || node.Title.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return true;
        return node.Children.Any(PassesFilter);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_tree.SelectedItem is not TreeViewItem { Tag: ShipExplorerNode node })
            return;

        var session = _controller.Session;
        session.SelectedAssemblyId = node.AssemblyId;
        session.SelectedPartId = node.PartId;
        session.SelectedReferenceId = node.ReferenceId;
        session.InspectorText = FormatInspector(node);
        _inspector.Text = session.InspectorText;
        session.StatusText = $"select:{node.Id}";
    }

    private string FormatInspector(ShipExplorerNode node)
    {
        var reg = _controller.Session.Registry;
        if (node.PartId is { } pid && reg.TryGetPart(pid, out var part))
        {
            return
                $"ID          {part.Id.Value}\n" +
                $"Name        {part.DisplayName}\n" +
                $"System      {part.System}\n" +
                $"Kind        {part.Kind}\n" +
                $"Assembly    {part.Assembly.Value}\n" +
                $"Functions   {part.Functions}\n" +
                $"Station Z   {Fmt(part.StationZStem)}\n" +
                $"Deck up     {Fmt(part.DeckUp)}\n" +
                $"Fab part    {part.FabPartId ?? "unresolved"}\n" +
                $"Entity      {part.EntityId?.ToString("N") ?? "unresolved"}\n" +
                $"Classified  {part.Classified}";
        }

        if (node.AssemblyId is { } aid && reg.TryGetAssembly(aid, out var asm))
        {
            var childCount = reg.ChildAssemblies(aid).Count();
            var partCount = reg.PartsOf(aid).Count();
            return
                $"Assembly    {asm.Id.Value}\n" +
                $"Name        {asm.DisplayName}\n" +
                $"System      {asm.System}\n" +
                $"Kind        {asm.Kind}\n" +
                $"Parent      {asm.ParentAssemblyId?.Value ?? "(root)"}\n" +
                $"Children    {childCount}\n" +
                $"Parts       {partCount}\n" +
                $"Functions   {asm.Functions}\n" +
                $"Station Z   {Fmt(asm.StationZStem)}\n" +
                $"Extent      {Fmt(asm.ExtentZ0)} → {Fmt(asm.ExtentZ1)}";
        }

        if (node.ReferenceId is { } rid && reg.TryGetReference(rid, out var reference))
        {
            return
                $"Reference   {reference.Id}\n" +
                $"Kind        {reference.Kind}\n" +
                $"Name        {reference.DisplayName}\n" +
                $"Parent      {reference.ParentAssemblyId?.Value ?? "unresolved"}\n" +
                $"Compartment {reference.CompartmentId ?? "unresolved"}\n" +
                $"Entity      {reference.EntityId?.ToString("N") ?? "unresolved"}";
        }

        return node.Title;
    }

    private void HideSelected()
    {
        foreach (var id in SelectedEntityIds())
            _controller.Session.HiddenEntityIds.Add(id);
        _controller.Session.StatusText = $"hide:{_controller.Session.HiddenEntityIds.Count}";
    }

    private void ShowSelected()
    {
        foreach (var id in SelectedEntityIds())
            _controller.Session.HiddenEntityIds.Remove(id);
        _controller.Session.StatusText = "show:selection";
    }

    private void IsolateSelected()
    {
        var ids = SelectedEntityIds();
        _controller.Session.RevealEntityIds = ids.Count == 0 ? null : ids;
        _controller.Session.StatusText = $"isolate:{ids.Count}";
    }

    private void IsolateCategory(Func<ShipPartRegistry, HashSet<Guid>> collect)
    {
        EnsureRegistry();
        var ids = collect(_controller.Session.Registry);
        _controller.Session.RevealEntityIds = ids.Count == 0 ? null : ids;
        _controller.Session.VisualLayers = CalypsoVisualLayers.ShipSkeleton | CalypsoVisualLayers.OuterHull;
        _controller.Session.StatusText = $"isolate-category:{ids.Count}";
    }

    private void EnsureRegistry()
    {
        if (_controller.Session.Registry.Assemblies.Count == 0
            && _controller.Session.Document.Entities.Count > 0)
            _controller.Session.RebuildSemanticRegistry();
    }

    private void ClearIsolateAndHide()
    {
        _controller.Session.RevealEntityIds = null;
        _controller.Session.HiddenEntityIds.Clear();
        _controller.Session.StatusText = "visibility:cleared";
    }

    private void FocusSelected()
    {
        var center = ComputeSelectionCenter();
        if (center is null)
        {
            _controller.Session.StatusText = "focus:no-geometry";
            return;
        }

        _controller.Renderer.SetOrbitPose(center.Value, 42f, 0.95f, 0.35f);
        _controller.SetView(CalypsoViewMode.Orbit);
        _controller.Session.StatusText = $"focus:{center.Value.X:0.#},{center.Value.Y:0.#},{center.Value.Z:0.#}";
    }

    private HashSet<Guid> SelectedEntityIds()
    {
        var session = _controller.Session;
        var reg = session.Registry;
        if (session.SelectedPartId is { } pid)
            return reg.CollectEntityIds(pid);
        if (session.SelectedAssemblyId is { } aid)
            return reg.CollectEntityIds(aid);
        if (session.SelectedReferenceId is { } rid
            && reg.TryGetReference(rid, out var reference)
            && reference.EntityId is { } eid)
            return [eid];
        return [];
    }

    private System.Numerics.Vector3? ComputeSelectionCenter()
    {
        var ids = SelectedEntityIds();
        if (ids.Count == 0)
            return null;
        float sx = 0, sy = 0, sz = 0;
        var n = 0;
        foreach (var e in _controller.Session.Document.Entities)
        {
            if (!ids.Contains(e.Id) || e.Points is not { Count: >= 1 })
                continue;
            var p = e.Points[0];
            if (p.Length < 3)
                continue;
            sx += p[0];
            sy += p[1];
            sz += p[2];
            n++;
        }

        return n == 0 ? null : new System.Numerics.Vector3(sx / n, sy / n, sz / n);
    }

    private static string Fmt(float? v) => v is { } x ? x.ToString("0.###") : "unresolved";

    private static TextBlock Hint(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0),
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);
        return t;
    }

    private static TextBlock Header(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
            FontSize = 12,
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);
        return t;
    }

    private static Button ActionBtn(string text, Action act)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(6, 2),
            FontSize = 10,
            MinWidth = 48,
        };
        b.Click += (_, _) => act();
        return b;
    }
}
